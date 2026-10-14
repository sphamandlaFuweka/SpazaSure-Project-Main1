using Microsoft.EntityFrameworkCore;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>Notifies every admin account about something waiting for their action.</summary>
public static class AdminAlerts
{
    public static async Task NotifyAdminsAsync(
        SpazaSureDbContext db,
        EventPublisher events,
        string type,
        string title,
        string message,
        string? referenceId = null,
        string priority = "normal",
        CancellationToken ct = default)
    {
        var adminIds = await db.Users
            .Where(u => u.Role.Name == "admin")
            .Select(u => u.Id.ToString())
            .ToListAsync(ct);
        events.PublishNotificationToMany(adminIds, type, title, message, priority, referenceId, "notification.admin");
    }
}
