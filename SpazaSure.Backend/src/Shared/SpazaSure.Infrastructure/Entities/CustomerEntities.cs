using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class CustomerProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int? Age { get; set; }

    /// <summary>JSON array of declared allergies, e.g. ["peanuts","dairy"].</summary>
    public string Allergies { get; set; } = "[]";

    public User User { get; set; } = null!;
}

/// <summary>
/// A suspicious-product report filed by a customer (or retailer) — the
/// starting point for the admin escalation-to-health-authority flow.
/// ProductId is nullable: a report can be filed against a barcode SpazaSure
/// doesn't recognize at all.
/// </summary>
public class Report : BaseEntity
{
    public Guid ReporterUserId { get; set; }
    public Guid? ProductId { get; set; }
    public string? Barcode { get; set; }

    /// <summary>e.g. "counterfeit", "expired", "pricing", "hygiene", "other" — free-text to stay flexible across the customer and retailer report flows.</summary>
    public string? ReportType { get; set; }

    /// <summary>Set when the report concerns a specific shop's conduct (the retailer app's report flow) rather than a scanned product.</summary>
    public string? ShopName { get; set; }
    public bool IsAnonymous { get; set; } = false;

    public string Description { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? PurchaseLocation { get; set; }
    public string? SupplierName { get; set; }

    /// <summary>submitted -&gt; under_review -&gt; escalated -&gt; resolved (or dismissed).</summary>
    public string Status { get; set; } = "submitted";
    public string? EscalatedTo { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public User Reporter { get; set; } = null!;
    public Product? Product { get; set; }
}
