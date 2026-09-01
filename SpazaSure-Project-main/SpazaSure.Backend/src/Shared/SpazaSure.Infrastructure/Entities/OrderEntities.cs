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
    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }

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
