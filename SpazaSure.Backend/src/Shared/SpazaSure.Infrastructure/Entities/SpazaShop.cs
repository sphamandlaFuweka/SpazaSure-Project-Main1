using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class SpazaShop : BaseEntity, IHasIdentityCheck
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
    public string? RegistrationDetails { get; set; }
    public string Status { get; set; } = "pending";
    public string ComplianceStatus { get; set; } = "incomplete";  // green, orange, red
    /// <summary>Public shop number (e.g. SS482913) printed on the SpazaSure sticker and QR code.</summary>
    public string? ShopCode { get; set; }
    public bool OnboardingFeePaid { get; set; } = false;
    public string? OnboardingFeeRef { get; set; }
    public decimal RatingAvg { get; set; } = 0;
    public int RatingCount { get; set; } = 0;
    /// <summary>Owner identity check with Didit (ID document, selfie, Home Affairs, sanctions).</summary>
    public string? IdentityCheckSessionId { get; set; }
    /// <summary>Didit session status, e.g. "Approved", "Declined", "In Review", "In Progress".</summary>
    public string? IdentityCheckStatus { get; set; }
    /// <summary>Didit's warnings for the admin, one per line.</summary>
    public string? IdentityCheckNotes { get; set; }
    public DateTime? IdentityCheckUpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<ShopDocument> Documents { get; set; } = [];
    public ICollection<ShopOnboardingPayment> OnboardingPayments { get; set; } = [];
}
