using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.VerificationService.Controllers;

/// <summary>
/// Public checks that something claiming to come from SpazaSure is genuine: an invoice (from the
/// QR code printed on it) and a delivery driver (from the badge code on their driver page).
/// Each check has a JSON form for the apps and a plain page for anyone scanning with a phone.
/// Only what is needed to confirm is shown, so the checks don't leak personal information.
/// </summary>
[ApiController]
[AllowAnonymous]
public class VerificationController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet("api/verification/invoices/{number}")]
    public async Task<IActionResult> Invoice(string number, [FromQuery] string? code)
    {
        var result = await CheckInvoiceAsync(number, code);
        return result is null
            ? NotFound(ApiResponse.Fail("No SpazaSure invoice matches this number and code. It may not be genuine."))
            : Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("api/verification/drivers/{badgeCode}")]
    public async Task<IActionResult> Driver(string badgeCode)
    {
        var result = await CheckDriverAsync(badgeCode);
        return result is null
            ? NotFound(ApiResponse.Fail("No active SpazaSure driver has this badge code. Don't hand over goods or cash."))
            : Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("verify/invoice/{number}")]
    public async Task<ContentResult> InvoicePage(string number, [FromQuery] string? c)
    {
        var r = await CheckInvoiceAsync(number, c);
        return Page(r is null
            ? Bad("Invoice not confirmed", "No SpazaSure invoice matches this code. Treat it as suspicious and contact SpazaSure.")
            : Good("Genuine SpazaSure invoice",
                ("Invoice", r.InvoiceNumber), ("Type", r.IsTaxInvoice ? "Tax invoice" : "Invoice"),
                ("Supplier", r.SupplierName), ("VAT number", r.SupplierVatNumber ?? "Not VAT registered"),
                ("Issued to", r.ShopName), ("Date", r.IssuedAt.ToLocalTime().ToString("d MMM yyyy")),
                ("Total", $"R {r.Total:N2}")));
    }

    [HttpGet("verify/driver/{badgeCode}")]
    public async Task<ContentResult> DriverPage(string badgeCode)
    {
        var r = await CheckDriverAsync(badgeCode);
        return Page(r is null
            ? Bad("Not a SpazaSure driver", "No active driver has this badge code. Don't hand over goods or cash; contact SpazaSure.")
            : Good("Registered SpazaSure driver",
                ("Name", r.FullName), ("Badge", r.BadgeCode), ("Vehicle", $"{r.VehicleType}, {r.VehicleRegistration}")));
    }

    private sealed record InvoiceCheck(string InvoiceNumber, bool IsTaxInvoice, string SupplierName, string? SupplierVatNumber,
        string ShopName, DateTime IssuedAt, decimal Total, decimal Vat);

    private async Task<InvoiceCheck?> CheckInvoiceAsync(string number, string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || number.Length > 40) return null;
        var invoice = await db.TaxInvoices.AsNoTracking()
            .Include(i => i.Supplier).Include(i => i.Order).ThenInclude(o => o.Shop)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == number.Trim().ToUpper());
        // Comparing the code here, not in the query, keeps it out of the database's query log
        if (invoice is null || !string.Equals(invoice.VerificationCode, code.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
        return new InvoiceCheck(invoice.InvoiceNumber, invoice.IsTaxInvoice, invoice.Supplier.CompanyName,
            invoice.SupplierVatNumber, invoice.Order.Shop.ShopName, invoice.IssuedAt, invoice.GoodsTotalInclVat, invoice.VatAmount);
    }

    private sealed record DriverCheck(string FullName, string BadgeCode, string VehicleType, string VehicleRegistration);

    private async Task<DriverCheck?> CheckDriverAsync(string badgeCode)
    {
        var code = badgeCode.Trim().ToUpperInvariant();
        if (!code.StartsWith("DRV-")) code = "DRV-" + code;
        if (code.Length > 12) return null;
        var d = await db.Drivers.AsNoTracking().FirstOrDefaultAsync(x => x.BadgeCode == code && x.IsActive);
        return d is null ? null : new DriverCheck(d.FullName, d.BadgeCode, d.VehicleType, d.VehicleRegistration);
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Good(string title, params (string Label, string Value)[] rows) =>
        $"<div class=\"ok\">&#10003;</div><h1>{E(title)}</h1><dl>" +
        string.Concat(rows.Select(r => $"<dt>{E(r.Label)}</dt><dd>{E(r.Value)}</dd>")) + "</dl>";

    private static string Bad(string title, string message) =>
        $"<div class=\"bad\">!</div><h1>{E(title)}</h1><p>{E(message)}</p>";

    private ContentResult Page(string body) => Content(
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        "<meta name=\"robots\" content=\"noindex\"><title>SpazaSure check</title><style>" +
        "body{font-family:system-ui,sans-serif;background:#F5F7FF;color:#17244F;margin:0;padding:24px}" +
        "main{max-width:440px;margin:0 auto;background:#fff;border-radius:20px;padding:24px;text-align:center;box-shadow:0 8px 30px rgba(37,68,154,.12)}" +
        ".ok,.bad{width:56px;height:56px;border-radius:50%;margin:0 auto 8px;display:flex;align-items:center;justify-content:center;font-size:28px;color:#fff;font-weight:800}" +
        ".ok{background:#078C50}.bad{background:#E02526}h1{font-size:20px}dl{text-align:left;display:grid;grid-template-columns:auto 1fr;gap:6px 12px}" +
        "dt{color:#5A5A7A}dd{margin:0;font-weight:700}small{display:block;margin-top:16px;color:#8888AA}</style></head><body><main>" +
        body + "<small>SpazaSure verification</small></main></body></html>",
        "text/html; charset=utf-8");
}
