using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

// Shop Academy: the shop's own business records (stock book, cash book, business details) and
// its progress through the practical lessons. Kept so a shop can run itself as a proper business
// and later show funders a track record of how it operates.

/// <summary>How the shop is set up as a business. One row per shop.</summary>
public class ShopBusinessProfile : BaseEntity
{
    public Guid ShopId { get; set; }
    /// <summary>sole_proprietor, partnership, pty_ltd, cooperative</summary>
    public string LegalStructure { get; set; } = "sole_proprietor";
    public string? CipcRegistrationNumber { get; set; }
    public string? SarsTaxNumber { get; set; }
    public string? VatNumber { get; set; }
    public string? TradingLicenceNumber { get; set; }
    public DateOnly? StartedTradingOn { get; set; }
    /// <summary>owned, rented, home</summary>
    public string PremisesType { get; set; } = "rented";
    public decimal? MonthlyRent { get; set; }
    public int FullTimeStaff { get; set; }
    public int PartTimeStaff { get; set; }
    public string? TradingHours { get; set; }
    public int TradingDaysPerWeek { get; set; } = 7;
    public string? BankName { get; set; }
    /// <summary>True when the shop's money is kept in a bank account separate from the owner's own.</summary>
    public bool HasBusinessBankAccount { get; set; }
    public bool AcceptsCardPayments { get; set; }
    public string? MainSuppliers { get; set; }
    public string? Goals { get; set; }

    public SpazaShop Shop { get; set; } = null!;
}

/// <summary>A product the shop keeps on its shelves.</summary>
public class ShopStockItem : BaseEntity
{
    public Guid ShopId { get; set; }
    /// <summary>The SpazaSure catalogue product, when the item was bought through SpazaSure.</summary>
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Unit { get; set; } = "unit";
    public string? Barcode { get; set; }
    /// <summary>What the shop paid for one unit (latest).</summary>
    public decimal CostPrice { get; set; }
    /// <summary>What the shop sells one unit for.</summary>
    public decimal SellingPrice { get; set; }
    public int Quantity { get; set; }
    /// <summary>Warn the owner when stock drops to this level.</summary>
    public int ReorderLevel { get; set; } = 5;
    public DateOnly? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;

    public SpazaShop Shop { get; set; } = null!;
    public ICollection<ShopStockMovement> Movements { get; set; } = [];
}

/// <summary>
/// A change to a stock item's quantity: stock received, sold, lost or counted. Quantity is signed
/// (received is positive, sold and losses are negative).
/// </summary>
public class ShopStockMovement : BaseEntity
{
    public Guid ShopId { get; set; }
    public Guid StockItemId { get; set; }
    /// <summary>received, sold, spoiled, stolen, own_use, count</summary>
    public string Type { get; set; } = "received";
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }
    public DateOnly OccurredOn { get; set; }
    /// <summary>The SpazaSure order this stock arrived with, so an order is only booked in once.</summary>
    public Guid? SourceOrderId { get; set; }

    public ShopStockItem StockItem { get; set; } = null!;
}

/// <summary>A cash book line: money that came in (takings) or went out (expenses).</summary>
public class ShopCashEntry : BaseEntity
{
    public Guid ShopId { get; set; }
    /// <summary>income or expense</summary>
    public string Direction { get; set; } = "income";
    /// <summary>e.g. cash_sales, card_sales, stock_purchase, rent, electricity, wages</summary>
    public string Category { get; set; } = "cash_sales";
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public DateOnly OccurredOn { get; set; }

    public SpazaShop Shop { get; set; } = null!;
}

/// <summary>A Shop Academy lesson the shop has finished.</summary>
public class ShopLessonProgress : BaseEntity
{
    public Guid ShopId { get; set; }
    public string LessonKey { get; set; } = string.Empty;
    /// <summary>Quiz answers right, out of the lesson's questions.</summary>
    public int QuizScore { get; set; }
    public int QuizTotal { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public SpazaShop Shop { get; set; } = null!;
}
