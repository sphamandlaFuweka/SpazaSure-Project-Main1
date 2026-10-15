using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>An in-app chat between a customer and a spaza shop (one per customer and shop).</summary>
public class ShopConversation : BaseEntity
{
    public Guid ShopId { get; set; }
    public Guid CustomerUserId { get; set; }
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public string? LastMessagePreview { get; set; }
    /// <summary>Messages the customer hasn't read yet.</summary>
    public int CustomerUnread { get; set; }
    /// <summary>Messages the shop hasn't read yet.</summary>
    public int ShopUnread { get; set; }

    public SpazaShop Shop { get; set; } = null!;
    public User CustomerUser { get; set; } = null!;
    public ICollection<ShopMessage> Messages { get; set; } = [];
}

public class ShopMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid SenderUserId { get; set; }
    /// <summary>"customer" or "shop"</summary>
    public string SenderRole { get; set; } = "customer";
    public string Body { get; set; } = string.Empty;

    public ShopConversation Conversation { get; set; } = null!;
}
