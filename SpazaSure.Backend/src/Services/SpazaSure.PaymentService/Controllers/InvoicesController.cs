using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.PaymentService.Services;
using SpazaSure.Shared.Models;

namespace SpazaSure.PaymentService.Controllers;

/// <summary>
/// Invoices for orders. The shop that ordered, its supplier and admins can get an order's invoice;
/// it is issued the first time anyone asks once the supplier has confirmed the order.
/// </summary>
[ApiController]
[Route("api/invoices")]
public class InvoicesController(SpazaSureDbContext db, InvoiceService invoices) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("orders/{orderId:guid}")]
    [Authorize]
    public async Task<IActionResult> ForOrder(Guid orderId, CancellationToken ct)
    {
        var order = await invoices.LoadOrderAsync(orderId, ct);
        if (order is null || !CanSee(order)) return NotFound(ApiResponse.Fail("Order not found."));

        var (issued, error) = await invoices.GetOrIssueAsync(orderId, ct);
        if (issued is null) return BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse<object>.Ok(Project(issued.Invoice, issued.Order)));
    }

    /// <summary>The supplier's own invoices, newest first.</summary>
    [HttpGet("supplier")]
    [Authorize(Roles = "supplier")]
    public async Task<IActionResult> SupplierInvoices([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var supplierId = await db.Suppliers.Where(s => s.UserId == UserId).Select(s => s.Id).FirstOrDefaultAsync(ct);
        if (supplierId == Guid.Empty) return NotFound(ApiResponse.Fail("Supplier not found."));
        return Ok(ApiResponse<object>.Ok(await ListAsync(db.TaxInvoices.Where(i => i.SupplierId == supplierId), page, pageSize, ct)));
    }

    /// <summary>The PDF, from a signed link that works for 30 minutes (no login, so phones can open it).</summary>
    [HttpGet("download/{invoiceId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(Guid invoiceId, [FromQuery] string? exp, [FromQuery] string? sig, CancellationToken ct)
    {
        if (!invoices.IsValidLink(invoiceId, exp, sig))
            return Unauthorized(ApiResponse.Fail("This invoice link has expired. Open the invoice again from SpazaSure."));
        var invoice = await db.TaxInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return NotFound(ApiResponse.Fail("Invoice not found."));
        var order = await invoices.LoadOrderAsync(invoice.OrderId, ct);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        var pdf = InvoicePdf.Render(invoice, order, invoices.VerifyLink(invoice));
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }

    private bool CanSee(Order order) =>
        User.IsInRole("admin") || order.Shop.UserId == UserId || order.Supplier.UserId == UserId;

    private object Project(TaxInvoice i, Order o) => new
    {
        i.Id, i.InvoiceNumber, i.IsTaxInvoice, i.SupplierVatNumber, i.IssuedAt,
        i.GoodsTotalInclVat, i.VatAmount,
        o.OrderNumber, OrderId = o.Id, SupplierName = o.Supplier.CompanyName, ShopName = o.Shop.ShopName,
        DownloadUrl = invoices.DownloadLink(i.Id),
        VerifyUrl = invoices.VerifyLink(i),
    };

    internal async Task<object> ListAsync(IQueryable<TaxInvoice> query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await query.CountAsync(ct);
        var items = await query.AsNoTracking()
            .Include(i => i.Order).ThenInclude(o => o.Shop).Include(i => i.Supplier)
            .OrderByDescending(i => i.IssuedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new { total, page, pageSize, items = items.Select(i => Project(i, i.Order)).ToList() };
    }
}

/// <summary>Admins see every invoice, e.g. for finance reports and supplier queries.</summary>
[ApiController]
[Route("api/admin/invoices")]
[Authorize(Roles = "admin")]
public class AdminInvoicesController(SpazaSureDbContext db, InvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? supplierId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var query = db.TaxInvoices.AsQueryable();
        if (supplierId is { } id) query = query.Where(i => i.SupplierId == id);
        if (from is { } f) query = query.Where(i => i.IssuedAt >= DateTime.SpecifyKind(f, DateTimeKind.Utc));
        if (to is { } t) query = query.Where(i => i.IssuedAt < DateTime.SpecifyKind(t, DateTimeKind.Utc).AddDays(1));
        var lister = new InvoicesController(db, invoices) { ControllerContext = ControllerContext };
        return Ok(ApiResponse<object>.Ok(await lister.ListAsync(query, page, pageSize, ct)));
    }
}
