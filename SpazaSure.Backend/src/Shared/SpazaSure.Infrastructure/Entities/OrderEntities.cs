using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid ShopId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? GroupBuyId { get; set; }
    public Guid? GroupBuyParticipantId { get; set; }
    public string Status { get; set; } = "draft";
    public string DeliveryType { get; set; } = "standard";
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLatitude { get; set; }
    public double? DeliveryLongitude { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal DeliveryMarkup { get; set; } = 50;
    public decimal? DeliveryDistanceKm { get; set; }
    public decimal PlatformCommission { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "invoice";
    public string PaymentStatus { get; set; } = "pending";
    public Guid? PaymentCheckoutId { get; set; }
    public string? PaymentProviderReference { get; set; }
    /// <summary>Identifies orders funded through a Paystack split rather than a later supplier transfer.</summary>
    public string? PaystackSplitCode { get; set; }
    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }
    /// <summary>Cash on delivery only: created at dispatch and entered by the shop to confirm delivery.</summary>
    public string? DeliveryCode { get; set; }
    /// <summary>When the order reached "delivered"; payouts become available PayoutDelayDays later.</summary>
    public DateTime? DeliveredAt { get; set; }
    /// <summary>When the supplier share was paid or allocated to their Paystack subaccount.</summary>
    public DateTime? PaidOutAt { get; set; }
    public string? PayoutReference { get; set; }
    /// <summary>When an automatic Paystack payout to the supplier was started; PaidOutAt is set once it succeeds.</summary>
    public DateTime? PayoutInitiatedAt { get; set; }

    public SpazaShop Shop { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public GroupBuy? GroupBuy { get; set; }
    public GroupBuyParticipant? GroupBuyParticipant { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal OriginalUnitPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal LineTotal { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
