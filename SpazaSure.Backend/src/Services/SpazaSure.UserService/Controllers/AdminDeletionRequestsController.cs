using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using SpazaSure.Shared.Storage;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// Master admins approve or reject users' requests to delete their account (POPIA s24).
/// Approving anonymises the account and deletes its documents; it can't be undone.
/// </summary>
[ApiController]
[Route("api/admin/deletion-requests")]
[Authorize(Roles = "admin")]
public class AdminDeletionRequestsController(
    SpazaSureDbContext db, IFileStorageService storage, EventPublisher events) : ControllerBase
{
    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var requests = await db.AccountDeletionRequests.AsNoTracking()
            .OrderByDescending(r => r.Status == "pending").ThenByDescending(r => r.CreatedAt).Take(200)
            .ToListAsync(ct);
        var userIds = requests.Select(r => r.UserId).ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id, u.Email, u.Phone, u.Status,
                Name = u.SpazaShop != null ? u.SpazaShop.ShopName
                    : u.Supplier != null ? u.Supplier.CompanyName
                    : u.CustomerProfile != null ? u.CustomerProfile.FirstName + " " + u.CustomerProfile.LastName
                    : u.DisplayName,
            })
            .ToDictionaryAsync(u => u.Id, ct);

        var items = new List<object>();
        foreach (var r in requests)
        {
            users.TryGetValue(r.UserId, out var u);
            items.Add(new
            {
                r.Id, r.UserId, r.Role, r.Reason, r.Status, r.DecisionNote, r.CreatedAt, r.DecidedAt,
                name = u?.Name, email = u?.Email, phone = u?.Phone,
                // What still has to happen before the account can be deleted
                blocker = r.Status == "pending" ? await PersonalData.DeletionBlockerAsync(db, r.UserId, ct) : null,
            });
        }
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var request = await db.AccountDeletionRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return NotFound(ApiResponse.Fail("Request not found."));
        if (request.Status != "pending") return Conflict(ApiResponse.Fail($"This request is already {request.Status}."));
        if (await PersonalData.DeletionBlockerAsync(db, request.UserId, ct) is { } blocker)
            return Conflict(ApiResponse.Fail(blocker));

        await PersonalData.AnonymiseAsync(db, storage, request.UserId, ct);
        request.Status = "approved";
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedByUserId = AdminId;
        request.Reason = null; // the reason can describe the person; it isn't needed once deleted
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("The account was deleted and its personal information erased."));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectDeletionBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Note))
            return BadRequest(ApiResponse.Fail("Tell the user why their account can't be deleted."));
        var request = await db.AccountDeletionRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return NotFound(ApiResponse.Fail("Request not found."));
        if (request.Status != "pending") return Conflict(ApiResponse.Fail($"This request is already {request.Status}."));

        request.Status = "rejected";
        request.DecisionNote = body.Note.Trim();
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedByUserId = AdminId;
        await db.SaveChangesAsync(ct);
        events.PublishNotification(request.UserId.ToString(), "account", "Account deletion request",
            $"We couldn't delete your account yet: {request.DecisionNote}", priority: "high",
            referenceId: request.Id.ToString(), routingKey: "account.deletion");
        return Ok(ApiResponse.Ok("The request was rejected and the user was told why."));
    }
}

public record RejectDeletionBody(string? Note);
