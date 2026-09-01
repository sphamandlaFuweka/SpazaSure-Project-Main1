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
    public string Unit { get; set; } = "unit";
    public string Images { get; set; } = "[]";            // JSON array of URLs
    public bool IsAvailable { get; set; } = true;
    public bool IsApproved { get; set; } = false;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsFoodItem { get; set; } = false;

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

public class SupplierDocument : BaseEntity
{
    public Guid SupplierId { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string DocUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? RejectionNote { get; set; }

    public Supplier Supplier { get; set; } = null!;
}

public class ShopDocument : BaseEntity
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

    public SpazaShop Shop { get; set; } = null!;
}
