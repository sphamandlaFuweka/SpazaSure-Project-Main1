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
/// Didit identity checks. A shop owner verifies their identity (needed for the green sticker) and
/// a supplier verifies their business; each gets a link to Didit's hosted check, and the result
/// arrives on the webhook. Admins see the result next to the compliance documents.
/// </summary>
[ApiController]
[Route("api/compliance/identity")]
public class IdentityChecksController(
    SpazaSureDbContext db,
    DiditService didit,
    EventPublisher events,
    ILogger<IdentityChecksController> logger) : ControllerBase
{
    private const string ShopVendorPrefix = "shop:";
    private const string SupplierVendorPrefix = "supplier:";
    private static readonly HashSet<string> FinalStatuses = ["Approved", "Declined"];

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("shop")]
    [Authorize(Roles = "spaza_owner")]
    public async Task<IActionResult> GetShop(CancellationToken ct)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        await RefreshAsync(shop, ct);
        return Ok(ApiResponse<object>.Ok(Describe(shop)));
    }

    [HttpPost("shop/session")]
    [Authorize(Roles = "spaza_owner")]
    public async Task<IActionResult> StartShop(CancellationToken ct)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var expected = string.IsNullOrWhiteSpace(shop.OwnerIdNumber)
            ? null
            : new { identification_number = shop.OwnerIdNumber.Trim(), id_country = "ZAF" };
        return await StartAsync(shop, didit.ShopWorkflowId, ShopVendorPrefix + shop.Id, didit.ShopCallbackUrl, expected, ct);
    }

    [HttpGet("supplier")]
    [Authorize(Roles = "supplier")]
    public async Task<IActionResult> GetSupplier(CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        await RefreshAsync(supplier, ct);
        return Ok(ApiResponse<object>.Ok(Describe(supplier)));
    }

    [HttpPost("supplier/session")]
    [Authorize(Roles = "supplier")]
    public async Task<IActionResult> StartSupplier(CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var expected = new Dictionary<string, string> { ["company_name"] = supplier.CompanyName, ["registry_country"] = "ZA" };
        if (!string.IsNullOrWhiteSpace(supplier.RegistrationNumber))
            expected["registration_number"] = supplier.RegistrationNumber.Trim();
        return await StartAsync(supplier, didit.SupplierWorkflowId, SupplierVendorPrefix + supplier.Id, didit.SupplierCallbackUrl, expected, ct);
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

            var notes = root.TryGetProperty("decision", out var decision) ? DiditService.ExtractNotes(decision) : null;
            await ApplyAsync(subject, sessionId, status, notes, ct);
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
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { url = session.Url, status = session.Status }));
    }

    /// <summary>Asks Didit for the latest result when it's still open, in case a webhook was missed.</summary>
    private async Task RefreshAsync(IHasIdentityCheck subject, CancellationToken ct)
    {
        if (subject.IdentityCheckSessionId is null || !didit.IsConfigured ||
            (subject.IdentityCheckStatus is { } current && FinalStatuses.Contains(current)) ||
            subject.IdentityCheckUpdatedAt > DateTime.UtcNow.AddSeconds(-30))
            return;
        try
        {
            var decision = await didit.GetDecisionAsync(subject.IdentityCheckSessionId, ct);
            await ApplyAsync(subject, subject.IdentityCheckSessionId, decision.Status, decision.Notes, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not refresh Didit session {SessionId}", subject.IdentityCheckSessionId);
        }
    }

    private async Task ApplyAsync(IHasIdentityCheck subject, string sessionId, string status, string? notes, CancellationToken ct)
    {
        // Ignore results for an older session the user has since replaced with a new one.
        if (subject.IdentityCheckSessionId is { } currentSession && currentSession != sessionId) return;

        var changed = subject.IdentityCheckStatus != status;
        subject.IdentityCheckSessionId = sessionId;
        subject.IdentityCheckStatus = status;
        if (notes is not null || FinalStatuses.Contains(status)) subject.IdentityCheckNotes = notes;
        subject.IdentityCheckUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (!changed || !FinalStatuses.Contains(status)) return;
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
                : "Your check didn't pass. Open your compliance page to try again, or contact SpazaSure support.",
            priority: approved ? "normal" : "high",
            referenceId: sessionId,
            routingKey: approved ? "compliance.approved" : "compliance.rejected");
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

    private static object Describe(IHasIdentityCheck subject) => new
    {
        status = subject.IdentityCheckStatus ?? "Not Started",
        verified = subject.IdentityCheckStatus == "Approved",
        canStart = subject.IdentityCheckStatus != "Approved",
        updatedAt = subject.IdentityCheckUpdatedAt,
    };
}
