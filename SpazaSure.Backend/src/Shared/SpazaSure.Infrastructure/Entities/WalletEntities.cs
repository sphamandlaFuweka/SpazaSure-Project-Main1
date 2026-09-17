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
    public SpazaShop Shop { get; set; } = null!;
}