namespace SpazaSure.NotificationService.Models;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SupplierId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // order, compliance, product, system
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal"; // high, normal
    public bool IsRead { get; set; } = false;
    public string? ReferenceId { get; set; } // e.g. orderId, productId
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class NotificationEvent
{
    public string SupplierId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string? ReferenceId { get; set; }
}

/// <summary>A phone registered for push notifications (Firebase Cloud Messaging token).</summary>
public class DeviceToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>The signed-in user the phone belongs to (same id as Notification.SupplierId).</summary>
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
