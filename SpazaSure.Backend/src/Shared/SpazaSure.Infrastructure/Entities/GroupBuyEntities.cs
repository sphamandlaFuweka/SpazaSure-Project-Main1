using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>A group buy campaign where multiple shops pool orders for bulk discounts.</summary>
public class GroupBuy : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ProductId { get; set; }
    public Guid SupplierId { get; set; }
    public int TargetQty { get; set; }
    public int CurrentQty { get; set; } = 0;
    public decimal OriginalPrice { get; set; }
    public decimal DiscountPrice { get; set; }
    public int DiscountPct { get; set; }
    public DateTime ExpiresAt { get; set; }
    /// <summary>Status: pending_approval, active, completed, expired, cancelled, rejected</summary>
    public string Status { get; set; } = "pending_approval";

    /// <summary>Only set for campaigns that existed before shops lost the ability to create them.</summary>
    public Guid? CreatedByShopId { get; set; }

    /// <summary>supplier or admin.</summary>
    public string CreatedByRole { get; set; } = "supplier";
    public Guid? CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionNote { get; set; }

    public Product Product { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public SpazaShop? CreatedByShop { get; set; }
    public ICollection<GroupBuyProduct> Products { get; set; } = [];
    public ICollection<GroupBuyParticipant> Participants { get; set; } = [];
}

/// <summary>A product offered as part of a group buy.</summary>
public class GroupBuyProduct : BaseEntity
{
    public Guid GroupBuyId { get; set; }
    public Guid ProductId { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountPrice { get; set; }
    public int DiscountPct { get; set; }
    public int TargetQty { get; set; }
    public int CurrentQty { get; set; } = 0;
    /// <summary>Status: active, qualified, approved, completed, expired, cancelled</summary>
    public string Status { get; set; } = "active";

    public GroupBuy GroupBuy { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<GroupBuyParticipantItem> Items { get; set; } = [];
}

/// <summary>A shop's participation in a group buy.</summary>
public class GroupBuyParticipant : BaseEntity
{
    public Guid GroupBuyId { get; set; }
    public Guid ShopId { get; set; }
    public int Quantity { get; set; }
    /// <summary>Status: joined, confirmed, cancelled</summary>
    public string Status { get; set; } = "joined";

    public GroupBuy GroupBuy { get; set; } = null!;
    public SpazaShop Shop { get; set; } = null!;
    public ICollection<GroupBuyParticipantItem> Items { get; set; } = [];
}

/// <summary>A participant's quantity for one product in a group buy.</summary>
public class GroupBuyParticipantItem : BaseEntity
{
    public Guid ParticipantId { get; set; }
    public Guid GroupBuyProductId { get; set; }
    public int Quantity { get; set; }
    /// <summary>Status: joined, confirmed, cancelled</summary>
    public string Status { get; set; } = "joined";

    public GroupBuyParticipant Participant { get; set; } = null!;
    public GroupBuyProduct GroupBuyProduct { get; set; } = null!;
}
