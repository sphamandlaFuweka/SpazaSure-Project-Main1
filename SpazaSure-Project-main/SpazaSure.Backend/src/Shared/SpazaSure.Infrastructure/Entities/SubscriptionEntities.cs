using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>Subscription plan available for suppliers (Basic free, Bronze/Silver/Gold paid).</summary>
public class SubscriptionPlan : BaseEntity
{
    public string Tier { get; set; } = "basic";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; } = 0m;
    public decimal AnnualPrice { get; set; } = 0m;
    public decimal CommissionRate { get; set; } = 5m;
    public int MaxListings { get; set; } = 10;
    public int MaxOrders { get; set; } = 50;
    public bool HasAnalytics { get; set; } = false;
    public bool HasPrioritySupport { get; set; } = false;
    public bool HasBulkPricing { get; set; } = false;
    public bool HasApiAccess { get; set; } = false;
    public bool HasCustomBranding { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public ICollection<SupplierSubscription> Subscriptions { get; set; } = [];
}

/// <summary>Tracks a supplier's active or historical subscription.</summary>
public class SupplierSubscription : BaseEntity
{
    public Guid SupplierId { get; set; }
    public Guid PlanId { get; set; }
    public string BillingCycle { get; set; } = "monthly";
    public string Status { get; set; } = "active";
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public decimal AmountPaid { get; set; } = 0m;
    public string? PaymentReference { get; set; }
    public string? PaymentMethod { get; set; }
    public string? CancelledReason { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
}