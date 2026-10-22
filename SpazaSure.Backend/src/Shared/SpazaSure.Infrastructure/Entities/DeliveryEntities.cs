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
