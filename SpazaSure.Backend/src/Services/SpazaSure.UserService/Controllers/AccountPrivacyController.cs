using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// POPIA rights for shops, suppliers and customers: download a copy of their personal information
/// (s23) and ask for their account to be deleted (s24). Deletion requests are approved by a master
/// admin (AdminDeletionRequestsController), which anonymises the account.
/// </summary>
[ApiController]
[Route("api/shop/profile/privacy")]
[Route("api/supplier/profile/privacy")]
[Route("api/customer/profile/privacy")]
[Authorize(Roles = "spaza_owner,supplier,customer")]
public class AccountPrivacyController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "";

    /// <summary>
    /// Whether the user has accepted the current privacy notice. People who registered before it
    /// existed, or before it last changed, are asked again after signing in.
    /// </summary>
    [HttpGet("consent")]
    public async Task<IActionResult> GetConsent(CancellationToken ct)
    {
        var current = CurrentNoticeConsentEvent;
        var accepted = await db.AuthAuditLogs.AnyAsync(a => a.UserId == UserId && a.Event == current, ct);
        return Ok(ApiResponse<object>.Ok(new { accepted, version = PrivacyNotice.Version, url = PrivacyNotice.Url }));
    }

    [HttpPost("consent")]
    public async Task<IActionResult> AcceptConsent(CancellationToken ct)
    {
        var current = CurrentNoticeConsentEvent;
        if (!await db.AuthAuditLogs.AnyAsync(a => a.UserId == UserId && a.Event == current, ct))
        {
            Consents.Record(db, UserId, Consents.PrivacyNotice, ClientIp);
            await db.SaveChangesAsync(ct);
        }
        return Ok(ApiResponse.Ok("Thank you. Your consent has been recorded."));
    }

    private static string CurrentNoticeConsentEvent => $"consent_{Consents.PrivacyNotice}:{PrivacyNotice.Version}";

    [HttpGet("my-data")]
    public async Task<IActionResult> MyData(CancellationToken ct)
    {
        db.AuthAuditLogs.Add(new AuthAuditLog { UserId = UserId, Event = "data_export", IpAddress = ClientIp });
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(await PersonalData.ExportAsync(db, UserId, ct)));
    }

    [HttpGet("deletion-request")]
    public async Task<IActionResult> GetDeletionRequest(CancellationToken ct)
    {
        var request = await db.AccountDeletionRequests.AsNoTracking()
            .Where(r => r.UserId == UserId).OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync(ct);
        return Ok(ApiResponse<object?>.Ok(request is null ? null : new
        {
            request.Id, request.Status, request.Reason, request.DecisionNote, request.CreatedAt, request.DecidedAt,
        }));
    }

    [HttpPost("deletion-request")]
    public async Task<IActionResult> RequestDeletion([FromBody] DeletionRequestBody body, CancellationToken ct)
    {
        if (await db.AccountDeletionRequests.AnyAsync(r => r.UserId == UserId && r.Status == "pending", ct))
            return Conflict(ApiResponse.Fail("You already asked for your account to be deleted. We'll let you know when it's done."));

        var request = new AccountDeletionRequest
        {
            UserId = UserId,
            Role = Role,
            Reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim()[..Math.Min(body.Reason.Trim().Length, 1000)],
        };
        db.AccountDeletionRequests.Add(request);
        db.AuthAuditLogs.Add(new AuthAuditLog { UserId = UserId, Event = "deletion_requested", IpAddress = ClientIp });
        await db.SaveChangesAsync(ct);
        await AdminAlerts.NotifyAdminsAsync(db, events, "account", "Account deletion requested",
            $"A {RoleLabel(Role)} asked for their account and personal information to be deleted.", request.Id.ToString());

        var blocker = await PersonalData.DeletionBlockerAsync(db, UserId, ct);
        return Ok(ApiResponse<object>.Ok(new { request.Id, request.Status, blocker },
            blocker is null
                ? "We received your request. Your account will be deleted within 30 days, and we'll let you know when it's done."
                : $"We received your request. Before we can delete your account: {blocker}"));
    }

    [HttpDelete("deletion-request")]
    public async Task<IActionResult> CancelDeletion(CancellationToken ct)
    {
        var request = await db.AccountDeletionRequests.FirstOrDefaultAsync(r => r.UserId == UserId && r.Status == "pending", ct);
        if (request is null) return NotFound(ApiResponse.Fail("There is no deletion request to cancel."));
        request.Status = "cancelled";
        request.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("Your deletion request has been cancelled."));
    }

    private string? ClientIp => HttpContext?.Connection.RemoteIpAddress?.ToString();

    internal static string RoleLabel(string role) => role switch
    {
        "spaza_owner" => "shop owner",
        "supplier" => "supplier",
        "customer" => "customer",
        _ => "user",
    };
}

public record DeletionRequestBody(string? Reason);
