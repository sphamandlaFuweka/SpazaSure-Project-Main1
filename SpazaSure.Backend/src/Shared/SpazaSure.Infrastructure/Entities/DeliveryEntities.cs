using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>
/// A driver SpazaSure delivers with. Admins register drivers and allocate them to orders; the
/// driver shares their location from a private link, which shops see on the tracking map.
/// </summary>
public class Driver : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    /// <summary>e.g. "Bakkie", "Truck", "Motorbike".</summary>
    public string VehicleType { get; set; } = string.Empty;
    public string VehicleRegistration { get; set; } = string.Empty;
    /// <summary>Province or city the driver works in, to help admins allocate nearby drivers.</summary>
    public string? Area { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Secret in the driver's link (/driver/{token}); replaced when the admin resets the link.</summary>
    public string AccessToken { get; set; } = string.Empty;
    /// <summary>Short code shops can check with the verification service, shown on the driver page.</summary>
    public string BadgeCode { get; set; } = string.Empty;
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastSeenAt { get; set; }

    public ICollection<DriverDocument> Documents { get; set; } = [];
    public ICollection<DriverPushSubscription> PushSubscriptions { get; set; } = [];
}

/// <summary>
/// A phone the driver turned delivery alerts on for, from their driver page (standard web push).
/// New deliveries pop up on it even when the page is closed.
/// </summary>
public class DriverPushSubscription : BaseEntity
{
    public Guid DriverId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public Driver Driver { get; set; } = null!;
}

/// <summary>
/// A driver's document, uploaded by an admin: driving licence, professional driving permit
/// (PrDP, needed to carry goods for reward) or ID. Private, like shop and supplier documents.
/// </summary>
public class DriverDocument : BaseEntity, ICheckedDocument
{
    public const string Licence = "licence";
    public const string Prdp = "prdp";
    public const string IdDocument = "id_document";
    public static readonly string[] Types = [Licence, Prdp, IdDocument];

    public Guid DriverId { get; set; }
    /// <summary>licence, prdp or id_document.</summary>
    public string DocType { get; set; } = string.Empty;
    public string DocUrl { get; set; } = string.Empty;
    /// <summary>When the licence or PrDP stops being valid; IDs don't expire.</summary>
    public DateOnly? ExpiryDate { get; set; }

    // ── Automatic check against the profile (see DocumentChecks) ──
    /// <summary>not_checked, passed, warning, failed or unavailable.</summary>
    public string CheckStatus { get; set; } = "not_checked";
    /// <summary>One line for the uploader and admin, e.g. "The name on the ID is not the owner's".</summary>
    public string? CheckSummary { get; set; }
    /// <summary>JSON: each finding, and what was read off the document.</summary>
    public string? CheckDetails { get; set; }
    public DateTime? CheckedAt { get; set; }
    /// <summary>ID or registration number read off the document, to spot one used by two accounts.</summary>
    public string? DocumentNumber { get; set; }

    public Driver Driver { get; set; } = null!;
}

/// <summary>
/// The invoice for the goods on one order, issued in the supplier's name. Numbers run in
/// sequence per supplier and are never reused, as SARS requires for tax invoices.
/// </summary>
public class TaxInvoice : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid SupplierId { get; set; }
    public int SequenceNumber { get; set; }
    /// <summary>e.g. "INV-ACME-000123".</summary>
    public string InvoiceNumber { get; set; } = string.Empty;
    /// <summary>True when the supplier was VAT registered at issue, so this is a "Tax Invoice".</summary>
    public bool IsTaxInvoice { get; set; }
    public string? SupplierVatNumber { get; set; }
    public decimal GoodsTotalInclVat { get; set; }
    public decimal VatAmount { get; set; }
    /// <summary>Printed as a QR code; the verification service confirms the invoice with it.</summary>
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
}
