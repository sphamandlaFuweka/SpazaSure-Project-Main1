using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class SpazaShop : BaseEntity
{
    public Guid UserId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerIdNumber { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Status { get; set; } = "pending";
    public string ComplianceStatus { get; set; } = "incomplete";  // green, orange, red
    public bool OnboardingFeePaid { get; set; } = false;
    public string? OnboardingFeeRef { get; set; }
    public decimal RatingAvg { get; set; } = 0;
    public int RatingCount { get; set; } = 0;

    public User User { get; set; } = null!;
    public ICollection<ShopDocument> Documents { get; set; } = [];
    public ICollection<ShopOnboardingPayment> OnboardingPayments { get; set; } = [];
}
