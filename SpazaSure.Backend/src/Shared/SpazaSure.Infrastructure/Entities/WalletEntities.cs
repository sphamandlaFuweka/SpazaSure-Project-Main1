using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class ShopWalletTransaction : BaseEntity
{
    public Guid ShopId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "top_up";
    public string Status { get; set; } = "pending";
    public string Method { get; set; } = "eft";
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime? ApprovedAt { get; set; }
    // Where a withdrawal is paid to (withdrawals only)
    public string? BankName { get; set; }
    public string? AccountHolder { get; set; }
    public string? AccountNumber { get; set; }
    public string? BranchCode { get; set; }
    public SpazaShop Shop { get; set; } = null!;
}

/// <summary>
/// One movement on a shop's wallet. Amount is signed (credits positive, debits negative) and the
/// shop's balance is the sum of its entries. SourceId is the top-up, order or withdrawal it belongs to.
/// </summary>
public class ShopWalletLedgerEntry : BaseEntity
{
    public Guid ShopId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public Guid? SourceId { get; set; }
    public string Description { get; set; } = "";
    public string? Reference { get; set; }
    public SpazaShop Shop { get; set; } = null!;
}