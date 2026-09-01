using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class ShopOnboardingPayment : BaseEntity
{
    public Guid ShopId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "pending";
    public string? PayFastPaymentId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public SpazaShop Shop { get; set; } = null!;
}
