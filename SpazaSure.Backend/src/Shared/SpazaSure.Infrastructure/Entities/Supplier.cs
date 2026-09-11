using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class Supplier : BaseEntity
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
    public string? LogoUrl { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<SupplierDocument> Documents { get; set; } = [];
}
