using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>A group buy campaign where multiple shops pool orders for bulk discounts.</summary>
public class GroupBuy : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Poster shown on the group buy banner/card; uploaded when the campaign is created.</summary>
    public string? BannerImageUrl { get; set; }
    public Guid ProductId { get; set; }
    public Guid SupplierId { get; set; }
    public int TargetQty { get; set; }
    public int CurrentQty { get; set; } = 0;
    public decimal OriginalPrice { get; set; }
    public decimal DiscountPrice { get; set; }
    public int DiscountPct { get; set; }
    public DateTime ExpiresAt { get; set; }
    /// <summary>Times the end date was pushed back a week because the target wasn't reached (once at most).</summary>
    public int ExtensionCount { get; set; }
    /// <summary>When members were told the campaign ends in 2 days; cleared when it is extended.</summary>
    public DateTime? EndingReminderSentAt { get; set; }
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

    /// <summary>
    /// The branch (warehouse) that supplies this campaign; null when it ships from the head office.
    /// The area below is copied from it when the campaign is created, so editing or removing the
    /// branch later doesn't move a running campaign.
    /// </summary>
    public Guid? SupplierBranchId { get; set; }
    /// <summary>Where shops must be to join: the city the campaign is advertised for, e.g. "Durban".</summary>
    public string? AreaCity { get; set; }
    public string? AreaProvince { get; set; }
    /// <summary>Centre of the area, normally the supplying branch.</summary>
    public double? AreaLatitude { get; set; }
    public double? AreaLongitude { get; set; }
    /// <summary>Shops further than this from the centre can't see or join the campaign.</summary>
    public decimal? AreaRadiusKm { get; set; }

    public Product Product { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public SpazaShop? CreatedByShop { get; set; }
    public SupplierBranch? SupplierBranch { get; set; }
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
    /// <summary>Status: pending_payment (card checkout open), joined, confirmed, cancelled</summary>
    public string Status { get; set; } = "joined";

    // Shops pay when they join (see GroupBuyPayments). Null for shops that joined before that.
    /// <summary>wallet or paystack.</summary>
    public string? PaymentMethod { get; set; }
    /// <summary>pending (card checkout open) or paid.</summary>
    public string? PaymentStatus { get; set; }
    /// <summary>The Paystack reference of the card checkout.</summary>
    public string? PaymentReference { get; set; }
    /// <summary>
    /// Names this payment in the wallet ledger. New for every attempt, so a shop whose earlier
    /// payment was refunded can join again without the two payments mixing.
    /// </summary>
    public Guid PaymentId { get; set; }
    /// <summary>Items plus delivery, as charged when joining.</summary>
    public decimal AmountPaid { get; set; }
    public decimal DeliveryFee { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>A card checkout holds its quantities until then, so no other shop takes them meanwhile.</summary>
    public DateTime? ReservedUntil { get; set; }

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
    /// <summary>Status: reserved (card checkout open), joined, confirmed, cancelled</summary>
    public string Status { get; set; } = "joined";
    /// <summary>The price per unit the shop paid when joining (includes SpazaSure's fee).</summary>
    public decimal ShopUnitPrice { get; set; }

    public GroupBuyParticipant Participant { get; set; } = null!;
    public GroupBuyProduct GroupBuyProduct { get; set; } = null!;
}
