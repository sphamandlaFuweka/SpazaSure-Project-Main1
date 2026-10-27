using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// The waitlist form on the SpazaSure website posts here. The notification service then emails
/// a confirmation link (double opt-in), so only real, willing addresses end up on the list.
/// </summary>
[ApiController]
[Route("api/public/waitlist")]
[AllowAnonymous]
public class WaitlistController(SpazaSureDbContext db) : ControllerBase
{
    public static readonly string[] Interests = ["customer", "spaza_owner", "supplier"];

    public sealed record JoinWaitlistRequest(
        string? Email, string? Name, string? Phone, string? Interest, string? Area,
        string? Source, bool MarketingConsent, string? Website);

    [HttpPost]
    public async Task<IActionResult> Join(JoinWaitlistRequest req, CancellationToken ct)
    {
        const string thanks = "Thanks! Check your inbox and click the link to confirm your spot.";

        // Hidden form field that people never fill in but bots do
        if (!string.IsNullOrEmpty(req.Website)) return Ok(ApiResponse.Ok(thanks));

        var email = req.Email?.Trim().ToLowerInvariant() ?? "";
        if (email.Length is 0 or > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email
            || !email.Contains('.', StringComparison.Ordinal))
            return BadRequest(ApiResponse.Fail("Please enter a valid email address."));

        var interest = req.Interest?.Trim().ToLowerInvariant() ?? "customer";
        if (!Interests.Contains(interest))
            return BadRequest(ApiResponse.Fail("Choose whether you're a shopper, a spaza shop owner or a supplier."));

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(req.Phone))
        {
            phone = SaValidation.NormalizePhone(req.Phone);
            if (phone is null) return BadRequest(ApiResponse.Fail("Please enter a valid South African phone number."));
        }

        var existing = await db.WaitlistSignups.FirstOrDefaultAsync(w => w.Email == email, ct);
        if (existing is not null)
        {
            // Signing up again: refresh their details and, if they never confirmed, send the link again
            // (at most once an hour). The reply is the same either way, so the form can't be used to
            // find out who is on the list.
            existing.Name = Clip(req.Name, 120) ?? existing.Name;
            existing.Phone = phone ?? existing.Phone;
            existing.Interest = interest;
            existing.Area = Clip(req.Area, 120) ?? existing.Area;
            existing.MarketingConsent = existing.MarketingConsent || req.MarketingConsent;
            existing.UnsubscribedAt = null;
            if (existing.ConfirmedAt is null && existing.ConfirmationSentAt < DateTime.UtcNow.AddHours(-1))
                existing.ConfirmationSentAt = null;
            await db.SaveChangesAsync(ct);
            return Ok(ApiResponse.Ok(existing.ConfirmedAt is null ? thanks : "You're already on the list. We'll be in touch!"));
        }

        db.WaitlistSignups.Add(new WaitlistSignup
        {
            Email = email,
            Name = Clip(req.Name, 120),
            Phone = phone,
            Interest = interest,
            Area = Clip(req.Area, 120),
            Source = Clip(req.Source, 80),
            MarketingConsent = req.MarketingConsent,
            IpAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                        ?? HttpContext.Connection.RemoteIpAddress?.ToString(),
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two submits of the same address at once: the first one won
        }
        return Ok(ApiResponse.Ok(thanks));
    }

    private static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }
}

[ApiController]
[Route("api/admin/waitlist")]
[Authorize(Roles = "admin")]
public class AdminWaitlistController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? interest, [FromQuery] bool? confirmed,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = Filter(interest, confirmed);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)
            .Select(w => new
            {
                w.Id, w.Email, w.Name, w.Phone, w.Interest, w.Area, w.Source, w.MarketingConsent,
                w.CreatedAt, w.ConfirmedAt, w.UnsubscribedAt,
            })
            .ToListAsync(ct);

        var all = db.WaitlistSignups.AsNoTracking();
        return Ok(ApiResponse<object>.Ok(new
        {
            items, total, page, pageSize,
            summary = new
            {
                total = await all.CountAsync(ct),
                confirmed = await all.CountAsync(w => w.ConfirmedAt != null, ct),
                lastSevenDays = await all.CountAsync(w => w.CreatedAt >= DateTime.UtcNow.AddDays(-7), ct),
                byInterest = await all.GroupBy(w => w.Interest)
                    .Select(g => new { interest = g.Key, count = g.Count() }).ToListAsync(ct),
            },
        }));
    }

    /// <summary>The list as a spreadsheet, e.g. to invite people when access opens up.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? interest, [FromQuery] bool? confirmed, CancellationToken ct)
    {
        var rows = await Filter(interest, confirmed).OrderBy(w => w.CreatedAt).ToListAsync(ct);
        var csv = new StringBuilder("email,name,phone,interest,area,source,marketing_consent,joined,confirmed,unsubscribed\n");
        foreach (var w in rows)
            csv.AppendJoin(',', Cell(w.Email), Cell(w.Name), Cell(w.Phone), Cell(w.Interest), Cell(w.Area), Cell(w.Source),
                    w.MarketingConsent ? "yes" : "no", w.CreatedAt.ToString("yyyy-MM-dd"),
                    w.ConfirmedAt?.ToString("yyyy-MM-dd") ?? "", w.UnsubscribedAt?.ToString("yyyy-MM-dd") ?? "")
               .Append('\n');
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv", $"spazasure-waitlist-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var deleted = await db.WaitlistSignups.Where(w => w.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound(ApiResponse.Fail("Not on the waitlist.")) : Ok(ApiResponse.Ok("Removed from the waitlist."));
    }

    private IQueryable<WaitlistSignup> Filter(string? interest, bool? confirmed)
    {
        var query = db.WaitlistSignups.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(interest)) query = query.Where(w => w.Interest == interest);
        if (confirmed == true) query = query.Where(w => w.ConfirmedAt != null);
        if (confirmed == false) query = query.Where(w => w.ConfirmedAt == null);
        return query;
    }

    /// <summary>A CSV cell, quoted when needed. A leading =, +, - or @ is defused so Excel won't run it as a formula.</summary>
    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if ("=+-@\t\r".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
