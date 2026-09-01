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
