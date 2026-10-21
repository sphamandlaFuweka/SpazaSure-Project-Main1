using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class Supplier : BaseEntity, IHasIdentityCheck
{
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? VatNumber { get; set; }
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Tier { get; set; } = "basic";            // basic, bronze, silver, gold
    public string Status { get; set; } = "pending";
    public decimal CommissionRate { get; set; } = 5.00m;
    public string? BankName { get; set; }
    public string? BankAccount { get; set; }
    public string? BankBranchCode { get; set; }
    /// <summary>Paystack transfer recipient for these bank details; cleared when they change.</summary>
    public string? PaystackRecipientCode { get; set; }
    /// <summary>Paystack split subaccount for these bank details; cleared when they change.</summary>
    public string? PaystackSubaccountCode { get; set; }
    public string? LogoUrl { get; set; }
    /// <summary>Business check with Didit (CIPC registration, directors, sanctions).</summary>
    public string? IdentityCheckSessionId { get; set; }
    /// <summary>Didit session status, e.g. "Approved", "Declined", "In Review", "In Progress".</summary>
    public string? IdentityCheckStatus { get; set; }
    /// <summary>Didit's warnings for the admin, one per line.</summary>
    public string? IdentityCheckNotes { get; set; }
    public DateTime? IdentityCheckUpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<SupplierDocument> Documents { get; set; } = [];
}
