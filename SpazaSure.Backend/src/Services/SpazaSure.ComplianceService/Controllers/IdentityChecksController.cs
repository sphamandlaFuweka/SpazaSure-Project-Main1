using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.ComplianceService.Services;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.ComplianceService.Controllers;

/// <summary>
/// Didit identity checks. A shop owner verifies their identity in the app (needed for the green
/// sticker) on Didit's hosted check, whose result arrives on the webhook. A supplier's business is
/// checked entirely on the server (CIPC register and sanctions screening), with no link, and admins
/// can run checks that complete on the server too. Admins see each result next to the documents.
/// </summary>
[ApiController]
[Route("api/compliance/identity")]
public class IdentityChecksController(
    SpazaSureDbContext db,
    DiditService didit,
    EventPublisher events,
    IServiceScopeFactory scopeFactory,
    ILogger<IdentityChecksController> logger) : ControllerBase
{
    private const string ShopVendorPrefix = "shop:";
    private const string SupplierVendorPrefix = "supplier:";
    private static readonly HashSet<string> FinalStatuses = ["Approved", "Declined"];
    /// <summary>Session ids of checks run on the server (not a Didit hosted session).</summary>
    private const string ServerPrefix = "server:";
    private static readonly TimeSpan ServerCheckTimeout = TimeSpan.FromMinutes(4);
    /// <summary>When admins were last told Didit is out of credit, so they get one alert an hour.</summary>
    private static long _lastCreditAlertTicks;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("shop")]
    [Authorize(Roles = "spaza_owner")]
    public async Task<IActionResult> GetShop(CancellationToken ct)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        await RefreshAsync(shop, ct);
        return Ok(ApiResponse<object>.Ok(Describe(shop, Missing(
            ("id_number", SaValidation.IsValidIdNumber(shop.OwnerIdNumber)),
            ("address", shop.Latitude is not null && shop.Longitude is not null)))));
    }

    [HttpPost("shop/session")]
    [Authorize(Roles = "spaza_owner")]
    public async Task<IActionResult> StartShop(CancellationToken ct)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        if (!SaValidation.IsValidIdNumber(shop.OwnerIdNumber))
            return BadRequest(ApiResponse.Fail("Add your valid 13-digit South African ID number to your profile first."));
        if (shop.Latitude is null || shop.Longitude is null)
            return BadRequest(ApiResponse.Fail("Confirm your shop's address on the map in your profile first."));

        var expected = new { identification_number = shop.OwnerIdNumber, id_country = "ZAF" };
        return await StartAsync(shop, didit.ShopWorkflowId, ShopVendorPrefix + shop.Id, didit.ShopCallbackUrl, expected, ct);
    }

    [HttpGet("supplier")]
    [Authorize(Roles = "supplier")]
    public async Task<IActionResult> GetSupplier(CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        await RefreshAsync(supplier, ct);
        return Ok(ApiResponse<object>.Ok(Describe(supplier, Missing(
            ("registration_number", SaValidation.IsValidCompanyRegistration(supplier.RegistrationNumber)),
            ("address", supplier.Latitude is not null && supplier.Longitude is not null)))));
    }

    [HttpPost("supplier/session")]
    [Authorize(Roles = "supplier")]
    public async Task<IActionResult> StartSupplier(CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        if (!SaValidation.IsValidCompanyRegistration(supplier.RegistrationNumber))
            return BadRequest(ApiResponse.Fail("Add your CIPC registration number (e.g. 2020/123456/07) under Company Information first."));
        if (supplier.Latitude is null || supplier.Longitude is null)
            return BadRequest(ApiResponse.Fail("Confirm your business address under Company Information first."));

        // Checked on the server (CIPC register + sanctions screening): nothing for the supplier to open.
        return await StartServerCheckAsync(supplier, adminName: null, ct);
    }

    /// <summary>
    /// Admin-run check that completes on the server, with no link to open. Suppliers: CIPC register
    /// and sanctions screening. Shops: the admin confirms they have seen the owner's original ID in
    /// person, then the owner is screened against sanctions lists.
    /// </summary>
    [HttpPost("admin/{subjectType}/{id:guid}/check")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CheckForAdmin(string subjectType, Guid id, [FromBody] AdminCheckBody? body, CancellationToken ct)
    {
        var admin = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId, ct);
        var adminName = admin?.DisplayName ?? admin?.Email ?? "a SpazaSure admin";

        if (subjectType.Equals("shop", StringComparison.OrdinalIgnoreCase))
        {
            var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
            if (!SaValidation.IsValidIdNumber(shop.OwnerIdNumber))
                return BadRequest(ApiResponse.Fail("Add a valid 13-digit South African ID number to the shop profile first."));
            if (body?.IdSeenInPerson != true)
                return BadRequest(ApiResponse.Fail("Confirm that you have seen the owner's original ID document in person and that it matches the profile."));
            return await StartServerCheckAsync(shop, adminName, ct);
        }

        if (subjectType.Equals("supplier", StringComparison.OrdinalIgnoreCase))
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
            if (!SaValidation.IsValidCompanyRegistration(supplier.RegistrationNumber))
                return BadRequest(ApiResponse.Fail("Add a valid CIPC registration number to the supplier profile first."));
            return await StartServerCheckAsync(supplier, adminName, ct);
        }

        return BadRequest(ApiResponse.Fail("Choose a shop or supplier check."));
    }

    /// <summary>The current check status of a shop or supplier, for the admin console to follow a running check.</summary>
    [HttpGet("admin/{subjectType}/{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetForAdmin(string subjectType, Guid id, CancellationToken ct)
    {
        IHasIdentityCheck? subject = subjectType.Equals("shop", StringComparison.OrdinalIgnoreCase)
            ? await db.SpazaShops.FirstOrDefaultAsync(s => s.Id == id, ct)
            : await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subject is null) return NotFound(ApiResponse.Fail("Not found."));
        await RefreshAsync(subject, ct);
        return Ok(ApiResponse<object>.Ok(Describe(subject, [])));
    }

    [HttpPost("didit/webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        if (!didit.IsConfigured) return StatusCode(StatusCodes.Status503ServiceUnavailable);

        await using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        var payload = body.ToArray();
        if (!didit.IsValidWebhook(payload, Request.Headers["X-Signature"], Request.Headers["X-Timestamp"], DateTimeOffset.UtcNow))
            return Unauthorized();

        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException) { return BadRequest(); }

        using (document)
        {
            var root = document.RootElement;
            var sessionId = root.TryGetProperty("session_id", out var s) ? s.GetString() : null;
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            var vendorData = root.TryGetProperty("vendor_data", out var v) ? v.GetString() : null;
            // Entity, transaction and other events don't carry a session status; acknowledge and skip them.
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(status)) return Ok();

            IHasIdentityCheck? subject = await FindSubjectAsync(sessionId, vendorData, ct);
            if (subject is null)
            {
                logger.LogWarning("Didit webhook for unknown session {SessionId}", sessionId);
                return Ok(); // nothing to update; a non-2xx would only make Didit retry
            }

            JsonElement? decision = root.TryGetProperty("decision", out var d) && d.ValueKind == JsonValueKind.Object ? d : null;
            var sandbox = root.TryGetProperty("environment", out var env) && env.ValueKind == JsonValueKind.String
                ? env.GetString() == "sandbox"
                : didit.IsSandbox;
            await ApplyAsync(subject, sessionId, status, decision, sandbox, ct);
        }
        return Ok();
    }

    private async Task<IActionResult> StartAsync(
        IHasIdentityCheck subject, string workflowId, string vendorData, string? callbackUrl, object? expected, CancellationToken ct)
    {
        if (subject.IdentityCheckStatus == "Approved")
            return Conflict(ApiResponse.Fail("You have already been verified."));
        if (!didit.IsConfigured || string.IsNullOrWhiteSpace(workflowId))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail("Identity checks are not available right now. Please try again later."));

        DiditService.Session session;
        try
        {
            session = await didit.CreateSessionAsync(workflowId, vendorData, callbackUrl, expected, ct);
        }
        catch (HttpRequestException ex) when (DiditService.IsOutOfCredit(ex))
        {
            logger.LogError(ex, "Didit is out of credit; could not start a session for {VendorData}", vendorData);
            if (Interlocked.Exchange(ref _lastCreditAlertTicks, DateTime.UtcNow.Ticks) < DateTime.UtcNow.AddHours(-1).Ticks)
                await AdminAlerts.NotifyAdminsAsync(db, events, "compliance", "Didit is out of credit",
                    "Identity and business checks can't start until the Didit account is topped up at business.didit.me.", null);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail("Identity checks are temporarily unavailable. SpazaSure has been notified; please try again later."));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not start a Didit session for {VendorData}", vendorData);
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse.Fail("We couldn't start the check. Please try again in a few minutes."));
        }

        // Didit returns the same unfinished session for the same vendor data, so a retry resumes it.
        subject.IdentityCheckSessionId = session.SessionId;
        subject.IdentityCheckStatus = session.Status;
        subject.IdentityCheckNotes = null;
        subject.IdentityCheckUpdatedAt = DateTime.UtcNow;
        // Starting the check is the consent shown on the button (Didit, Home Affairs/CIPC, sanctions).
        Consents.Record(db, subject.UserId, Consents.IdentityCheck, HttpContext?.Connection.RemoteIpAddress?.ToString());
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { url = session.Url, status = session.Status }));
    }

    /// <summary>Asks Didit for the latest result when it's still open, in case a webhook was missed.</summary>
    private async Task RefreshAsync(IHasIdentityCheck subject, CancellationToken ct)
    {
        if (subject.IdentityCheckSessionId?.StartsWith(ServerPrefix) == true)
        {
            // A server check that never finished (e.g. the service restarted): let it be started again.
            if (subject.IdentityCheckStatus == "In Progress" && subject.IdentityCheckUpdatedAt < DateTime.UtcNow - ServerCheckTimeout)
            {
                subject.IdentityCheckStatus = null;
                subject.IdentityCheckNotes = "The check didn't finish. Please start it again.";
                subject.IdentityCheckUpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return;
        }
        if (subject.IdentityCheckSessionId is null || !didit.IsConfigured ||
            (subject.IdentityCheckStatus is { } current && FinalStatuses.Contains(current)) ||
            subject.IdentityCheckUpdatedAt > DateTime.UtcNow.AddSeconds(-30))
            return;
        try
        {
            var decision = await didit.GetDecisionAsync(subject.IdentityCheckSessionId, ct);
            await ApplyAsync(subject, subject.IdentityCheckSessionId, decision.Status, decision.Details, didit.IsSandbox, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not refresh Didit session {SessionId}", subject.IdentityCheckSessionId);
        }
    }

    private async Task ApplyAsync(
        IHasIdentityCheck subject, string sessionId, string status, JsonElement? decision, bool sandbox, CancellationToken ct)
    {
        // Ignore results for an older session the user has since replaced with a new one.
        if (subject.IdentityCheckSessionId is { } currentSession && currentSession != sessionId) return;

        var notes = decision is { } details ? DiditService.ExtractNotes(details) : null;
        if (status == "Approved" && !sandbox)
        {
            // Didit passing isn't enough: the document or company it checked must be the one on the profile.
            // (Skipped in sandbox, whose made-up data never matches a real profile.)
            var feature = subject is SpazaShop ? "id_verifications" : "registry_checks";
            if (decision is not { } full || !full.TryGetProperty(feature, out var results) || results.ValueKind != JsonValueKind.Array)
            {
                try { full = (await didit.GetDecisionAsync(sessionId, ct)).Details; }
                catch (HttpRequestException ex)
                {
                    // Leave the status open; the next status check fetches the decision again.
                    logger.LogWarning(ex, "Could not fetch the Didit decision for approved session {SessionId}", sessionId);
                    return;
                }
                notes = DiditService.ExtractNotes(full);
            }
            var mismatch = subject switch
            {
                SpazaShop shop => DiditService.IdNumberMismatch(full, shop.OwnerIdNumber),
                Supplier supplier => DiditService.RegistrationMismatch(full, supplier.RegistrationNumber),
                _ => null,
            };
            if (mismatch is not null)
            {
                logger.LogWarning("Didit approved session {SessionId} but it doesn't match the profile: {Reason}", sessionId, mismatch);
                status = "Declined";
                notes = notes is null ? mismatch : $"{mismatch}\n{notes}";
            }
        }

        var changed = subject.IdentityCheckStatus != status;
        subject.IdentityCheckSessionId = sessionId;
        subject.IdentityCheckStatus = status;
        if (notes is not null || FinalStatuses.Contains(status)) subject.IdentityCheckNotes = notes;
        subject.IdentityCheckUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (!changed || !FinalStatuses.Contains(status)) return;
        NotifyResult(events, subject, sessionId);
    }

    /// <summary>Tells the shop owner or supplier their check passed or didn't.</summary>
    private static void NotifyResult(EventPublisher events, IHasIdentityCheck subject, string sessionId)
    {
        var status = subject.IdentityCheckStatus;
        if (status is null || !FinalStatuses.Contains(status)) return;
        var isShop = subject is SpazaShop;
        var approved = status == "Approved";
        events.PublishNotification(
            supplierId: subject.UserId.ToString(),
            type: "compliance",
            title: approved
                ? (isShop ? "Identity verified ✓" : "Business verified ✓")
                : (isShop ? "Identity check not passed" : "Business check not passed"),
            message: approved
                ? (isShop ? "Your identity check passed. It counts towards your SpazaSure Verified sticker." : "Your business check passed.")
                : $"Your check didn't pass. {FirstLine(subject.IdentityCheckNotes)}Open your compliance page to try again, or contact SpazaSure support.",
            priority: approved ? "normal" : "high",
            referenceId: sessionId,
            routingKey: approved ? "compliance.approved" : "compliance.rejected");
    }

    /// <summary>
    /// Starts a check that runs on the server and finishes by itself (usually within a minute;
    /// the CIPC search can take up to about 90 seconds). The status is "In Progress" until then.
    /// </summary>
    private async Task<IActionResult> StartServerCheckAsync(IHasIdentityCheck subject, string? adminName, CancellationToken ct)
    {
        if (subject.IdentityCheckStatus == "Approved")
            return Conflict(ApiResponse.Fail("Already verified."));
        if (!didit.HasApiKey)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail("Checks are not available right now. Please try again later."));
        if (subject.IdentityCheckStatus == "In Progress" && subject.IdentityCheckSessionId?.StartsWith(ServerPrefix) == true &&
            subject.IdentityCheckUpdatedAt > DateTime.UtcNow - ServerCheckTimeout)
            return Ok(ApiResponse<object>.Ok(new { status = "In Progress", url = (string?)null }, "The check is already running."));

        var sessionId = ServerPrefix + Guid.NewGuid().ToString("N");
        subject.IdentityCheckSessionId = sessionId;
        subject.IdentityCheckStatus = "In Progress";
        subject.IdentityCheckNotes = null;
        subject.IdentityCheckUpdatedAt = DateTime.UtcNow;
        if (adminName is null)
            // The supplier pressing "Verify my business" is the consent shown on the button.
            Consents.Record(db, subject.UserId, Consents.IdentityCheck, HttpContext?.Connection.RemoteIpAddress?.ToString());
        else
            db.AuthAuditLogs.Add(new AuthAuditLog { UserId = UserId, Event = $"identity_check_by_admin:{sessionId}" });
        await db.SaveChangesAsync(ct);

        var isShop = subject is SpazaShop;
        var subjectId = subject is SpazaShop shop ? shop.Id : ((Supplier)subject).Id;
        _ = Task.Run(() => RunServerCheckAsync(scopeFactory, isShop, subjectId, sessionId, adminName));

        return Ok(ApiResponse<object>.Ok(new { status = "In Progress", url = (string?)null },
            "Checking now. The result appears here within a minute or two."));
    }

    private static async Task RunServerCheckAsync(
        IServiceScopeFactory scopeFactory, bool isShop, Guid subjectId, string sessionId, string? adminName)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<SpazaSureDbContext>();
        var didit = services.GetRequiredService<DiditService>();
        var events = services.GetRequiredService<EventPublisher>();
        var logger = services.GetRequiredService<ILogger<IdentityChecksController>>();
        using var timeout = new CancellationTokenSource(ServerCheckTimeout);
        var ct = timeout.Token;

        IHasIdentityCheck? subject = isShop
            ? await db.SpazaShops.FirstOrDefaultAsync(s => s.Id == subjectId, ct)
            : await db.Suppliers.FirstOrDefaultAsync(s => s.Id == subjectId, ct);
        if (subject is null || subject.IdentityCheckSessionId != sessionId) return;

        try
        {
            var outcome = subject switch
            {
                SpazaShop shop => await ServerChecks.CheckOwnerInPersonAsync(didit, shop, adminName ?? "a SpazaSure admin", ct),
                Supplier supplier => await ServerChecks.CheckBusinessAsync(didit, supplier, didit.IsSandbox, ct),
                _ => throw new InvalidOperationException("Unknown check."),
            };
            subject.IdentityCheckStatus = outcome.Status;
            subject.IdentityCheckNotes = outcome.Notes.Length <= 2000 ? outcome.Notes : outcome.Notes[..2000];
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException)
        {
            var outOfCredit = ex is HttpRequestException http && DiditService.IsOutOfCredit(http);
            logger.LogError(ex, "Server check {SessionId} failed", sessionId);
            if (outOfCredit && Interlocked.Exchange(ref _lastCreditAlertTicks, DateTime.UtcNow.Ticks) < DateTime.UtcNow.AddHours(-1).Ticks)
                await AdminAlerts.NotifyAdminsAsync(db, events, "compliance", "Didit is out of credit",
                    "Identity and business checks can't run until the Didit account is topped up at business.didit.me.", null);
            subject.IdentityCheckStatus = null;
            subject.IdentityCheckNotes = outOfCredit
                ? "Checks are temporarily unavailable. SpazaSure has been notified; please try again later."
                : "The check couldn't be completed. Please try again in a few minutes.";
        }
        subject.IdentityCheckUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);
        NotifyResult(events, subject, sessionId);
    }

    private async Task<IHasIdentityCheck?> FindSubjectAsync(string sessionId, string? vendorData, CancellationToken ct)
    {
        IHasIdentityCheck? subject = await db.SpazaShops.FirstOrDefaultAsync(x => x.IdentityCheckSessionId == sessionId, ct);
        subject ??= await db.Suppliers.FirstOrDefaultAsync(x => x.IdentityCheckSessionId == sessionId, ct);
        if (subject is not null || vendorData is null) return subject;

        if (vendorData.StartsWith(ShopVendorPrefix) && Guid.TryParse(vendorData[ShopVendorPrefix.Length..], out var shopId))
            return await db.SpazaShops.FirstOrDefaultAsync(x => x.Id == shopId, ct);
        if (vendorData.StartsWith(SupplierVendorPrefix) && Guid.TryParse(vendorData[SupplierVendorPrefix.Length..], out var supplierId))
            return await db.Suppliers.FirstOrDefaultAsync(x => x.Id == supplierId, ct);
        return null;
    }

    private static string FirstLine(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? "" : notes.Split('\n')[0].TrimEnd('.') + ". ";

    /// <summary>Profile details that must be right before the check can start.</summary>
    private static string[] Missing(params (string Name, bool Ok)[] requirements) =>
        requirements.Where(r => !r.Ok).Select(r => r.Name).ToArray();

    private static object Describe(IHasIdentityCheck subject, string[] missing) => new
    {
        missing,
        status = subject.IdentityCheckStatus ?? "Not Started",
        verified = subject.IdentityCheckStatus == "Approved",
        canStart = subject.IdentityCheckStatus != "Approved",
        updatedAt = subject.IdentityCheckUpdatedAt,
        // The main reason a check didn't pass (e.g. the ID number doesn't match the profile).
        reason = subject.IdentityCheckStatus is "Declined" or null && !string.IsNullOrWhiteSpace(subject.IdentityCheckNotes)
            ? subject.IdentityCheckNotes.Split('\n')[0]
            : null,
    };
}

/// <summary>The admin confirms they have seen the shop owner's original ID document in person.</summary>
public record AdminCheckBody(bool IdSeenInPerson);
