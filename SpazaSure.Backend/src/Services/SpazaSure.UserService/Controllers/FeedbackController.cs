using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using SpazaSure.Shared.Storage;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// "Send feedback" from the mobile app and the supplier portal while SpazaSure is being tested:
/// bugs, ideas, questions and praise, with an optional screenshot. The team reads and sorts it in
/// the admin console (AdminFeedbackController).
/// </summary>
[ApiController]
[Route("api/shop/profile/feedback")]
[Route("api/supplier/profile/feedback")]
[Route("api/customer/profile/feedback")]
[Authorize(Roles = "spaza_owner,supplier,customer")]
public class FeedbackController(SpazaSureDbContext db, IFileStorageService storage, EventPublisher events) : ControllerBase
{
    public static readonly string[] Types = ["bug", "idea", "question", "praise"];
    public static readonly string[] Sources = ["mobile_app", "supplier_portal"];
    private static readonly string[] ScreenshotExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxScreenshotBytes = 5 * 1024 * 1024;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "";

    public sealed class FeedbackForm
    {
        public string? Type { get; set; }
        public string? Message { get; set; }
        public short? Rating { get; set; }
        public string? Screen { get; set; }
        public string? Source { get; set; }
        public string? AppVersion { get; set; }
        public string? Platform { get; set; }
        public IFormFile? Screenshot { get; set; }
    }

    /// <summary>Sends feedback. Multipart form, so a screenshot can come along.</summary>
    [HttpPost]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> Send([FromForm] FeedbackForm form, CancellationToken ct)
    {
        var message = form.Message?.Trim() ?? "";
        if (message.Length < 3) return BadRequest(ApiResponse.Fail("Please tell us a little more."));
        if (message.Length > 2000) return BadRequest(ApiResponse.Fail("Please keep it under 2,000 characters."));
        var type = form.Type?.Trim().ToLowerInvariant() ?? "idea";
        if (!Types.Contains(type)) return BadRequest(ApiResponse.Fail("Choose bug, idea, question or praise."));
        if (form.Rating is < 1 or > 5) return BadRequest(ApiResponse.Fail("The rating must be from 1 to 5."));

        // A burst of identical sends is a double tap, not two pieces of feedback
        var since = DateTime.UtcNow.AddMinutes(-2);
        if (await db.FeedbackItems.AnyAsync(f => f.UserId == UserId && f.Message == message && f.CreatedAt >= since, ct))
            return Ok(ApiResponse.Ok("Thanks, we already have this one."));
        if (await db.FeedbackItems.CountAsync(f => f.UserId == UserId && f.CreatedAt >= DateTime.UtcNow.AddHours(-1), ct) >= 20)
            return StatusCode(429, ApiResponse.Fail("You've sent a lot of feedback this hour. Please try again later."));

        var item = new FeedbackItem
        {
            UserId = UserId,
            Role = Role,
            Type = type,
            Message = message,
            Rating = form.Rating,
            Screen = Clip(form.Screen, 120),
            Source = Sources.Contains(form.Source) ? form.Source! : "mobile_app",
            AppVersion = Clip(form.AppVersion, 30),
            Platform = Clip(form.Platform, 60),
        };

        if (form.Screenshot is { Length: > 0 } file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (file.Length > MaxScreenshotBytes) return BadRequest(ApiResponse.Fail("The screenshot must be under 5MB."));
            if (!ScreenshotExtensions.Contains(ext)) return BadRequest(ApiResponse.Fail("Only JPG, PNG and WEBP screenshots are allowed."));
            await using var stream = file.OpenReadStream();
            // A private folder: screenshots can show personal details, so only signed links open them
            item.ScreenshotUrl = await storage.SaveAsync(stream, $"feedback-screenshots/{item.Id}{ext}", file.ContentType, ct);
        }

        db.FeedbackItems.Add(item);
        await db.SaveChangesAsync(ct);

        if (type == "bug")
            await AdminAlerts.NotifyAdminsAsync(db, events, "system", "New bug report from a tester",
                $"{RoleLabel(Role)}: {(message.Length > 140 ? message[..140] + "…" : message)}", item.Id.ToString(), ct: ct);

        return Ok(ApiResponse<object>.Ok(new { item.Id }, "Thank you! Your feedback helps us make SpazaSure better."));
    }

    /// <summary>What this person has sent, newest first, with where each item stands.</summary>
    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var items = await db.FeedbackItems.AsNoTracking()
            .Where(f => f.UserId == UserId)
            .OrderByDescending(f => f.CreatedAt).Take(50)
            .Select(f => new { f.Id, f.Type, f.Message, f.Status, f.CreatedAt })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(items));
    }

    internal static string RoleLabel(string role) => role switch
    {
        "spaza_owner" => "Spaza shop",
        "supplier" => "Supplier",
        "customer" => "Customer",
        _ => role,
    };

    private static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }
}

/// <summary>The team's view of tester feedback in the admin console.</summary>
[ApiController]
[Route("api/admin/feedback")]
[Authorize(Roles = "admin")]
public class AdminFeedbackController(SpazaSureDbContext db) : ControllerBase
{
    public static readonly string[] Statuses = ["new", "planned", "in_progress", "done", "wont_fix"];

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? type, [FromQuery] string? role,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.FeedbackItems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(f => f.Type == type);
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(f => f.Role == role);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)
            .Select(f => new
            {
                f.Id, f.Type, f.Message, f.Rating, f.Screen, f.Source, f.AppVersion, f.Platform,
                f.Status, f.AdminNote, f.Role, f.CreatedAt, f.ScreenshotUrl,
                senderEmail = f.User.Email, senderPhone = f.User.Phone,
                senderName = f.User.SpazaShop != null ? f.User.SpazaShop.ShopName
                    : f.User.Supplier != null ? f.User.Supplier.CompanyName
                    : f.User.CustomerProfile != null ? f.User.CustomerProfile.FirstName + " " + f.User.CustomerProfile.LastName : null,
            })
            .ToListAsync(ct);

        var counts = await db.FeedbackItems.AsNoTracking()
            .GroupBy(f => f.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        var ratings = await db.FeedbackItems.AsNoTracking().Where(f => f.Rating != null)
            .Select(f => (double)f.Rating!.Value).ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            items = rows.Select(r => new
            {
                r.Id, r.Type, r.Message, r.Rating, r.Screen, r.Source, r.AppVersion, r.Platform,
                r.Status, r.AdminNote, r.Role, r.CreatedAt, r.senderEmail, r.senderPhone, r.senderName,
                screenshotUrl = PrivateFiles.Link(r.ScreenshotUrl),
            }),
            total, page, pageSize,
            counts,
            averageRating = ratings.Count == 0 ? (double?)null : Math.Round(ratings.Average(), 1),
            ratingCount = ratings.Count,
        }));
    }

    public sealed record UpdateFeedbackRequest(string? Status, string? AdminNote);

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateFeedbackRequest req, CancellationToken ct)
    {
        var item = await db.FeedbackItems.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (item is null) return NotFound(ApiResponse.Fail("Feedback not found."));
        if (req.Status is not null)
        {
            if (!Statuses.Contains(req.Status)) return BadRequest(ApiResponse.Fail("Unknown status."));
            item.Status = req.Status;
        }
        if (req.AdminNote is not null)
            item.AdminNote = req.AdminNote.Trim() is { Length: > 0 } note ? (note.Length > 1000 ? note[..1000] : note) : null;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("Feedback updated."));
    }
}
