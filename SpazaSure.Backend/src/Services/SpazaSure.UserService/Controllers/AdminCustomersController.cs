using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// The customers who use the SpazaSure customer app, listed for admins like shops and suppliers:
/// who they are, their area, plan and activity, and suspending or reinstating them.
/// </summary>
[ApiController]
[Route("api/admin/customers")]
[Authorize(Roles = "admin")]
public class AdminCustomersController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Users.AsNoTracking().Where(u => u.Role.Name == "customer");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                (u.Phone != null && u.Phone.Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.CustomerProfile != null && (
                    (u.CustomerProfile.FirstName + " " + u.CustomerProfile.LastName).ToLower().Contains(term) ||
                    (u.CustomerProfile.City != null && u.CustomerProfile.City.ToLower().Contains(term)))));
        }
        query = status switch
        {
            "suspended" => query.Where(u => u.Status == "suspended"),
            "active" => query.Where(u => u.Status != "suspended"),
            _ => query,
        };

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.Id, u.Phone, u.Email, u.Status, u.CreatedAt, u.LastLoginAt, u.ProfilePhotoUrl,
                Profile = u.CustomerProfile,
            })
            .ToListAsync();

        var ids = users.Select(u => u.Id).ToList();
        var scans = await db.CustomerScanEvents.Where(s => ids.Contains(s.CustomerUserId))
            .GroupBy(s => s.CustomerUserId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var reports = await db.Reports.Where(r => ids.Contains(r.ReporterUserId))
            .GroupBy(r => r.ReporterUserId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var points = await db.CustomerRewardTransactions.Where(t => ids.Contains(t.CustomerUserId))
            .GroupBy(t => t.CustomerUserId).Select(g => new { g.Key, Points = g.Sum(t => t.Points) })
            .ToDictionaryAsync(x => x.Key, x => x.Points);
        var plans = await db.CustomerSubscriptions.Where(s => ids.Contains(s.CustomerUserId) && s.Status == "active")
            .Select(s => new { s.CustomerUserId, s.PlanKey })
            .ToListAsync();

        var items = users.Select(u => new
        {
            u.Id,
            FullName = u.Profile is null ? "" : $"{u.Profile.FirstName} {u.Profile.LastName}".Trim(),
            u.Profile?.PreferredName,
            Phone = u.Phone ?? "",
            Email = u.Email ?? "",
            u.Profile?.Age,
            u.Profile?.Province,
            u.Profile?.City,
            u.Profile?.Suburb,
            PreferredLanguage = u.Profile?.PreferredLanguage ?? "English",
            Allergies = ParseList(u.Profile?.Allergies),
            MarketingOptIn = u.Profile?.MarketingOptIn ?? false,
            Plan = plans.FirstOrDefault(p => p.CustomerUserId == u.Id)?.PlanKey ?? "free",
            ScanCount = scans.GetValueOrDefault(u.Id),
            ReportCount = reports.GetValueOrDefault(u.Id),
            RewardPoints = points.GetValueOrDefault(u.Id),
            IsSuspended = u.Status == "suspended",
            u.Status,
            ProfilePhotoUrl = u.ProfilePhotoUrl,
            JoinedAt = u.CreatedAt,
            u.LastLoginAt,
        });

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    /// <summary>Signs the customer out everywhere and stops them signing in again until reinstated.</summary>
    [HttpPatch("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role.Name == "customer");
        if (user is null) return NotFound(ApiResponse.Fail("Customer not found."));
        user.Status = "suspended";
        var tokens = await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync();
        foreach (var token in tokens) token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Customer suspended."));
    }

    [HttpPatch("{id:guid}/reinstate")]
    public async Task<IActionResult> Reinstate(Guid id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role.Name == "customer");
        if (user is null) return NotFound(ApiResponse.Fail("Customer not found."));
        if (user.Status != "suspended") return BadRequest(ApiResponse.Fail("This customer isn't suspended."));
        user.Status = "active";
        await db.SaveChangesAsync();
        events.PublishNotification(user.Id.ToString(), "account", "Your account is active again",
            "SpazaSure has reinstated your account. You can sign in again.", routingKey: "account.reinstated");
        return Ok(ApiResponse.Ok("Customer reinstated."));
    }

    private static List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
