using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>
/// A shop owner's or customer's billing details: who invoices are made out to, and (shops only)
/// the bank account their wallet withdrawals are paid into. One row per user.
/// </summary>
public class BillingProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public string? BillingName { get; set; }
    public string? CompanyName { get; set; }
    public string? VatNumber { get; set; }
    public string? BillingEmail { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }

    // Payout bank account
    public string? BankName { get; set; }
    public string? AccountHolder { get; set; }
    public string? AccountNumber { get; set; }
    public string? BranchCode { get; set; }
    /// <summary>"cheque" or "savings".</summary>
    public string? AccountType { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>
/// A card saved through Paystack. We only keep Paystack's reusable authorization and what the
/// card looks like (brand, last four digits, expiry), never the card number. A row is "pending"
/// while its R1 check is at Paystack and "active" once Paystack confirms it.
/// </summary>
public class SavedCard : BaseEntity
{
    public Guid UserId { get; set; }
    public string Status { get; set; } = "pending";
    /// <summary>The R1 check's Paystack reference.</summary>
    public string Reference { get; set; } = "";
    /// <summary>The email the card was saved under; Paystack only charges it with the same email.</summary>
    public string Email { get; set; } = "";
    /// <summary>Paystack's reusable authorization. Secret: it can charge the card, so it never leaves the server.</summary>
    public string? AuthorizationCode { get; set; }
    /// <summary>Paystack's fingerprint of the card, so the same card isn't saved twice.</summary>
    public string? Signature { get; set; }
    public string? Brand { get; set; }
    public string? Last4 { get; set; }
    public string? ExpMonth { get; set; }
    public string? ExpYear { get; set; }
    public string? Bank { get; set; }
    public bool IsDefault { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public User User { get; set; } = null!;
}
