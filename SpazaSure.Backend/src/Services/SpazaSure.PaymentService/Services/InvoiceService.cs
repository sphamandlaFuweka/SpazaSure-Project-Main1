using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.PaymentService.Services;

/// <summary>
/// Issues the invoice for an order's goods in the supplier's name. VAT-registered suppliers get a
/// SARS tax invoice (prices include 15% VAT); others get a plain invoice. Delivery and the
/// SpazaSure service fee are SpazaSure's charges, so they are listed separately and not invoiced
/// in the supplier's name.
/// </summary>
public sealed class InvoiceService(SpazaSureDbContext db, IConfiguration config)
{
    public const decimal VatRate = 0.15m;

    /// <summary>Order statuses an invoice can be issued for: the sale has been accepted.</summary>
    public static readonly string[] InvoiceableStatuses = ["confirmed", "processing", "ready", "dispatched", "delivered"];

    public sealed record Issued(TaxInvoice Invoice, Order Order);

    /// <summary>The order's invoice, issued now if it doesn't exist yet. Null with a reason when it can't be.</summary>
    public async Task<(Issued? Result, string? Error)> GetOrIssueAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order is null) return (null, "Order not found.");

        var existing = await db.TaxInvoices.FirstOrDefaultAsync(i => i.OrderId == orderId, ct);
        if (existing is not null) return (new Issued(existing, order), null);
        if (!InvoiceableStatuses.Contains(order.Status))
            return (null, order.Status == "cancelled"
                ? "Cancelled orders don't get an invoice."
                : "The invoice is issued once the supplier confirms the order.");

        // Numbers run in sequence per supplier. Two requests at once can race for the same
        // number; the unique index stops the second, which then takes the next one.
        for (var attempt = 0; ; attempt++)
        {
            var next = (await db.TaxInvoices.Where(i => i.SupplierId == order.SupplierId)
                .MaxAsync(i => (int?)i.SequenceNumber, ct) ?? 0) + 1;
            var goods = order.Items.Sum(i => i.LineTotal);
            var isTax = !string.IsNullOrWhiteSpace(order.Supplier.VatNumber);
            var invoice = new TaxInvoice
            {
                OrderId = order.Id,
                SupplierId = order.SupplierId,
                SequenceNumber = next,
                InvoiceNumber = $"INV-{Prefix(order.Supplier)}-{next:D6}",
                IsTaxInvoice = isTax,
                SupplierVatNumber = isTax ? order.Supplier.VatNumber!.Trim() : null,
                GoodsTotalInclVat = goods,
                VatAmount = isTax ? VatIncluded(goods) : 0m,
                VerificationCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant(),
                IssuedAt = DateTime.UtcNow,
            };
            db.TaxInvoices.Add(invoice);
            try
            {
                await db.SaveChangesAsync(ct);
                return (new Issued(invoice, order), null);
            }
            catch (DbUpdateException ex) when (attempt < 3 && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.Entry(invoice).State = EntityState.Detached;
                // Another request may have issued this order's invoice meanwhile
                if (await db.TaxInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.OrderId == orderId, ct) is { } raced)
                    return (new Issued(raced, order), null);
            }
        }
    }

    public Task<Order?> LoadOrderAsync(Guid orderId, CancellationToken ct = default) =>
        db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Shop)
            .Include(o => o.Supplier)
            .Include(o => o.SupplierBranch)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    /// <summary>VAT inside a VAT-inclusive amount: 15/115 of it.</summary>
    public static decimal VatIncluded(decimal inclusive) =>
        Math.Round(inclusive * VatRate / (1m + VatRate), 2, MidpointRounding.AwayFromZero);

    /// <summary>Up to four letters of the company name, e.g. "Acme Foods" becomes "ACME".</summary>
    private static string Prefix(Supplier supplier)
    {
        var letters = new string(supplier.CompanyName.Where(char.IsAsciiLetter).Take(4).ToArray()).ToUpperInvariant();
        return letters.Length >= 2 ? letters : "SUP";
    }

    // ── Signed download links, so the app can open the PDF in the phone's browser ──

    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(30);

    private byte[] Key => SHA256.HashData(Encoding.UTF8.GetBytes("spazasure-invoices:" + config["Jwt:Secret"]));

    public string PublicBase => (config["PublicApiUrl"] ?? "").TrimEnd('/');

    public string DownloadLink(Guid invoiceId)
    {
        var expires = DateTimeOffset.UtcNow.Add(LinkLifetime).ToUnixTimeSeconds();
        return $"{PublicBase}/api/invoices/download/{invoiceId}?exp={expires}&sig={Sign(invoiceId, expires)}";
    }

    public bool IsValidLink(Guid invoiceId, string? exp, string? sig)
    {
        if (!long.TryParse(exp, NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires || string.IsNullOrEmpty(sig))
            return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(Sign(invoiceId, expires)), Convert.FromHexString(sig));
        }
        catch (FormatException) { return false; }
    }

    private string Sign(Guid invoiceId, long expires) =>
        Convert.ToHexString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes($"{invoiceId:N}\n{expires}"))).ToLowerInvariant();

    /// <summary>Where the QR code on the invoice leads: the verification service's public check.</summary>
    public string VerifyLink(TaxInvoice invoice) =>
        $"{PublicBase}/verify/invoice/{Uri.EscapeDataString(invoice.InvoiceNumber)}?c={invoice.VerificationCode}";
}
