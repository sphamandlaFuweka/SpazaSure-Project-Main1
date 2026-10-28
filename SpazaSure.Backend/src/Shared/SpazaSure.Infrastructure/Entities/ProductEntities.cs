using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? IconUrl { get; set; }
    public short SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public Category? Parent { get; set; }
    public ICollection<Product> Products { get; set; } = [];
}

public class Product : BaseEntity
{
    public Guid SupplierId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public int MinOrderQty { get; set; } = 1;
    public int StockQty { get; set; } = 0;
    /// <summary>Per-branch quantities; empty when the supplier doesn't track stock per branch.</summary>
    public ICollection<ProductBranchStock> BranchStock { get; set; } = [];
    public string Unit { get; set; } = "unit";
    public string Images { get; set; } = "[]";            // JSON array of URLs
    public bool IsAvailable { get; set; } = true;
    public bool IsApproved { get; set; } = false;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsFoodItem { get; set; } = false;

    /// <summary>JSON array of allergen tags, e.g. ["peanuts","dairy"] — used by the Customer app's allergen warning feature.</summary>
    public string Allergens { get; set; } = "[]";

    // ── Product information shown to shops and customers (like a retail product page) ──
    public string? Brand { get; set; }
    /// <summary>Pack size as printed, e.g. "10 kg" or "6 x 500 ml".</summary>
    public string? PackSize { get; set; }
    public string? CountryOfOrigin { get; set; }
    /// <summary>Manufacturer or importer named on the pack.</summary>
    public string? Manufacturer { get; set; }
    public string? Ingredients { get; set; }
    /// <summary>e.g. "100 g" or "1 cup (250 ml)".</summary>
    public string? ServingSize { get; set; }
    public string? ServingsPerPack { get; set; }
    /// <summary>JSON array of {nutrient, per100, perServing}, e.g. Energy 1490 kJ.</summary>
    public string NutritionInfo { get; set; } = "[]";
    /// <summary>Preparation, usage and serving suggestions.</summary>
    public string? UsageInstructions { get; set; }
    public string? StorageInstructions { get; set; }
    /// <summary>Safety warnings, e.g. "Keep out of reach of children".</summary>
    public string? Warnings { get; set; }
    /// <summary>JSON array of {label, value}, e.g. Shelf life / 12 months.</summary>
    public string Specifications { get; set; } = "[]";
    /// <summary>JSON array of supplier tips for telling the genuine product from a counterfeit.</summary>
    public string AuthenticityTips { get; set; } = "[]";

    public Supplier Supplier { get; set; } = null!;
    public Category? Category { get; set; }
    public ICollection<ProductQrCode> QrCodes { get; set; } = [];
}

public class ProductQrCode : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? OrderItemId { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsScanned { get; set; } = false;
    public DateTime? ScannedAt { get; set; }
    public bool IsRecalled { get; set; } = false;

    public Product Product { get; set; } = null!;
}

/// <summary>
/// One physical item. The outer Code is printed openly (barcode/QR); the inner PIN sits under a
/// scratch panel and only its hash is stored. Scratching and verifying the PIN consumes the unit.
/// </summary>
public class ProductUnitCode : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>active, consumed, recalled or compromised.</summary>
    public string Status { get; set; } = "active";
    public int FailedPinAttempts { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public double? ConsumedLatitude { get; set; }
    public double? ConsumedLongitude { get; set; }

    public Product Product { get; set; } = null!;
}

public class SupplierDocument : BaseEntity, ICheckedDocument
{
    public Guid SupplierId { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string DocUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? RejectionNote { get; set; }

    // ── Automatic check against the profile (see DocumentChecks) ──
    /// <summary>not_checked, passed, warning, failed or unavailable.</summary>
    public string CheckStatus { get; set; } = "not_checked";
    /// <summary>One line for the uploader and admin, e.g. "The name on the ID is not the owner's".</summary>
    public string? CheckSummary { get; set; }
    /// <summary>JSON: each finding, and what was read off the document.</summary>
    public string? CheckDetails { get; set; }
    public DateTime? CheckedAt { get; set; }
    /// <summary>ID or registration number read off the document, to spot one used by two accounts.</summary>
    public string? DocumentNumber { get; set; }

    public Supplier Supplier { get; set; } = null!;
}

public class ShopDocument : BaseEntity, ICheckedDocument
{
    public Guid ShopId { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string? DocUrl { get; set; }
    public string Status { get; set; } = "missing";
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? RejectionNote { get; set; }
    public string? GuidanceUrl { get; set; }

    // ── Automatic check against the profile (see DocumentChecks) ──
    /// <summary>not_checked, passed, warning, failed or unavailable.</summary>
    public string CheckStatus { get; set; } = "not_checked";
    /// <summary>One line for the uploader and admin, e.g. "The name on the ID is not the owner's".</summary>
    public string? CheckSummary { get; set; }
    /// <summary>JSON: each finding, and what was read off the document.</summary>
    public string? CheckDetails { get; set; }
    public DateTime? CheckedAt { get; set; }
    /// <summary>ID or registration number read off the document, to spot one used by two accounts.</summary>
    public string? DocumentNumber { get; set; }

    public SpazaShop Shop { get; set; } = null!;
}
