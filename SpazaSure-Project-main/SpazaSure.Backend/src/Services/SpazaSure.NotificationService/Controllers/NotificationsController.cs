using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.NotificationService.Data;

namespace SpazaSure.NotificationService.Controllers;

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

    private string GetSupplierId() =>
        User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var supplierId = GetSupplierId();
        var query = _db.Notifications
            .Where(n => n.SupplierId == supplierId)
            .OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { data = items, total, page, pageSize });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var supplierId = GetSupplierId();
        var count = await _db.Notifications
            .CountAsync(n => n.SupplierId == supplierId && !n.IsRead);
        return Ok(new { count });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var supplierId = GetSupplierId();
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.SupplierId == supplierId);

        if (notification == null) return NotFound();

        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Marked as read" });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var supplierId = GetSupplierId();
        await _db.Notifications
            .Where(n => n.SupplierId == supplierId && !n.IsRead)
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

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var query = _db.Notifications
            .Where(n => n.SupplierId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
        var userId = GetUserId();
        var count = await _db.Notifications
            .CountAsync(n => n.SupplierId == userId && !n.IsRead);
        return Ok(new { success = true, data = new { count } });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserId();
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
        var userId = GetUserId();
        await _db.Notifications
            .Where(n => n.SupplierId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return Ok(new { success = true, message = "All marked as read" });
    }
}
