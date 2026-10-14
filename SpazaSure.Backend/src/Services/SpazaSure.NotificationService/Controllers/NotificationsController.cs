using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.NotificationService.Data;
using SpazaSure.NotificationService.Models;

namespace SpazaSure.NotificationService.Controllers;

internal static class NotificationUser
{
    /// <summary>
    /// The signed-in user's id. JWT "sub" is mapped to ClaimTypes.NameIdentifier when the token is
    /// read, so look there first (reading only "sub" returned an empty id and an empty inbox).
    /// </summary>
    public static string Get(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? user.FindFirstValue("id") ?? "";

    public static IQueryable<Notification> Page(IQueryable<Notification> query, int page, int pageSize) =>
        query.Skip((Math.Max(page, 1) - 1) * Math.Clamp(pageSize, 1, 100)).Take(Math.Clamp(pageSize, 1, 100));
}

/// <summary>Notifications for the dashboard (suppliers and admins).</summary>
[ApiController]
[Route("api/supplier/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationDbContext _db;

    public NotificationsController(NotificationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = NotificationUser.Get(User);
        if (userId.Length == 0) return Unauthorized();
        var query = _db.Notifications
            .Where(n => n.SupplierId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync();
        var items = await NotificationUser.Page(query, page, pageSize).ToListAsync();

        return Ok(new { data = items, total, page, pageSize });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = NotificationUser.Get(User);
        if (userId.Length == 0) return Unauthorized();
        var count = await _db.Notifications.CountAsync(n => n.SupplierId == userId && !n.IsRead);
        return Ok(new { count });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = NotificationUser.Get(User);
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.SupplierId == userId);
        if (notification == null) return NotFound();

        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Marked as read" });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = NotificationUser.Get(User);
        if (userId.Length == 0) return Unauthorized();
        await _db.Notifications
            .Where(n => n.SupplierId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return Ok(new { message = "All marked as read" });
    }
}

/// <summary>
/// Shop-facing notifications endpoint for the mobile app.
/// Same logic as supplier — the SupplierId field stores the user's ID regardless of role.
/// </summary>
[ApiController]
[Route("api/shop/notifications")]
[Authorize]
public class ShopNotificationsController : ControllerBase
{
    private readonly NotificationDbContext _db;

    public ShopNotificationsController(NotificationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = NotificationUser.Get(User);
        if (userId.Length == 0) return Unauthorized();
        var query = _db.Notifications
            .Where(n => n.SupplierId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync();
        var items = await NotificationUser.Page(query, page, pageSize)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.Priority,
                n.IsRead,
                n.ReferenceId,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(new { success = true, data = items, total, page, pageSize });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = NotificationUser.Get(User);
        if (userId.Length == 0) return Unauthorized();
        var count = await _db.Notifications.CountAsync(n => n.SupplierId == userId && !n.IsRead);
        return Ok(new { success = true, data = new { count } });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = NotificationUser.Get(User);
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.SupplierId == userId);
        if (notification == null) return NotFound();
        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = "Marked as read" });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = NotificationUser.Get(User);
        if (userId.Length == 0) return Unauthorized();
        await _db.Notifications
            .Where(n => n.SupplierId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return Ok(new { success = true, message = "All marked as read" });
    }
}
