using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController(SpazaSureDbContext db) : ControllerBase
{
    //  SUPPLIERS 

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.Suppliers
            .Include(s => s.Documents)
            .Include(s => s.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s =>
                s.CompanyName.ToLower().Contains(term) ||
                s.Email.ToLower().Contains(term) ||
                s.ContactPerson.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "verified")
                query = query.Where(s => s.Status == "verified");
            else if (status == "pending")
                query = query.Where(s => s.Status != "verified");
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.CompanyName,
                s.ContactPerson,
                s.Email,
                s.Phone,
                s.Tier,
                IsVerified = s.Status == "verified",
                s.Status,
                JoinedAt = s.CreatedAt,
                Documents = s.Documents.Select(d => new
                {
                    d.Id,
                    d.DocType,
                    d.DocUrl,
                    d.Status,
                    d.ExpiryDate,
                    d.RejectionNote
                })
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    [HttpPatch("suppliers/{id}/verify")]
    public async Task<IActionResult> VerifySupplier(Guid id)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        supplier.Status = "verified";
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Supplier verified successfully."));
    }

    //  SPAZA OWNERS 

    [HttpGet("spaza-owners")]
    public async Task<IActionResult> GetSpazaOwners(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.SpazaShops.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s =>
                s.ShopName.ToLower().Contains(term) ||
                s.OwnerName.ToLower().Contains(term) ||
                (s.Email != null && s.Email.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "verified")
                query = query.Where(s => s.Status == "verified");
            else if (status == "pending")
                query = query.Where(s => s.Status != "verified");
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.ShopName,
                s.OwnerName,
                Email = s.Email != null ? s.Email : (s.User != null ? s.User.Email ?? "" : ""),
                s.Phone,
                Address = s.Address ?? "",
                City = s.City ?? "",
                Province = s.Province ?? "",
                IsVerified = s.Status == "verified",
                s.Status,
                s.ComplianceStatus,
                JoinedAt = s.CreatedAt,
                Documents = s.Documents.Select(d => new
                {
                    d.Id,
                    d.DocType,
                    d.DocUrl,
                    d.Status,
                    d.ExpiryDate,
                    d.RejectionNote
                })
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    [HttpPatch("spaza-owners/{id}/verify")]
    public async Task<IActionResult> VerifySpazaOwner(Guid id)
    {
        var shop = await db.SpazaShops.FindAsync(id);
        if (shop is null) return NotFound(ApiResponse.Fail("Spaza shop not found."));
        shop.Status = "verified";
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Spaza owner verified successfully."));
    }

    [HttpGet("wallet-top-ups")]
    public async Task<IActionResult> GetWalletTopUps([FromQuery] string status = "pending")
    {
        var query = db.ShopWalletTransactions.Include(t => t.Shop).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        var items = await query.OrderByDescending(t => t.CreatedAt).Take(100).Select(t => new {
            t.Id, t.Amount, t.Method, t.Status, t.Reference, t.Notes,
            t.CreatedAt, ShopId = t.ShopId, ShopName = t.Shop.ShopName
        }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpPatch("wallet-top-ups/{id:guid}/approve")]
    public async Task<IActionResult> ApproveWalletTopUp(Guid id)
    {
        var transaction = await db.ShopWalletTransactions.FirstOrDefaultAsync(t => t.Id == id && t.Type == "top_up");
        if (transaction is null) return NotFound(ApiResponse.Fail("Top-up request not found."));
        if (transaction.Status != "pending") return BadRequest(ApiResponse.Fail("Top-up request is no longer pending."));
        transaction.Status = "approved";
        transaction.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Wallet top-up approved."));
    }

    [HttpPatch("wallet-top-ups/{id:guid}/reject")]
    public async Task<IActionResult> RejectWalletTopUp(Guid id, [FromBody] WalletTopUpRejectRequest req)
    {
        var transaction = await db.ShopWalletTransactions.FirstOrDefaultAsync(t => t.Id == id && t.Type == "top_up");
        if (transaction is null) return NotFound(ApiResponse.Fail("Top-up request not found."));
        if (transaction.Status != "pending") return BadRequest(ApiResponse.Fail("Top-up request is no longer pending."));
        transaction.Status = "rejected";
        transaction.Notes = req.Reason;
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Wallet top-up rejected."));
    }

    //  REPORTS / REGULATORY ESCALATIONS

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports(
        [FromQuery] string? status,
        [FromQuery] string? escalatedTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.Reports
            .Include(r => r.Product)
            .Include(r => r.Reporter)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(escalatedTo))
            query = query.Where(r => r.EscalatedTo != null && r.EscalatedTo.Contains(escalatedTo));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.ReportType,
                r.Barcode,
                ProductName = r.Product != null ? r.Product.Name : null,
                ReporterName = r.Reporter != null && r.Reporter.Phone != null ? r.Reporter.Phone : null,
                r.ShopName,
                r.IsAnonymous,
                r.Description,
                r.PhotoUrl,
                r.Status,
                r.EscalatedTo,
                r.EscalatedAt,
                r.ResolutionNote,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    public record EscalateReportRequest(string EscalatedTo, string Status, string? ResolutionNote);

    [HttpPatch("reports/{id:guid}/escalate")]
    public async Task<IActionResult> EscalateReport(Guid id, [FromBody] EscalateReportRequest req)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null)
            return NotFound(ApiResponse.Fail("Report not found."));

        if (string.IsNullOrWhiteSpace(req.EscalatedTo))
            return BadRequest(ApiResponse.Fail("Escalation destination is required."));

        report.Status = string.IsNullOrWhiteSpace(req.Status) ? "escalated" : req.Status;
        report.EscalatedTo = req.EscalatedTo;
        report.EscalatedAt = DateTime.UtcNow;
        report.ResolutionNote = req.ResolutionNote;

        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            report.Id,
            report.Status,
            report.EscalatedTo,
            report.EscalatedAt,
            report.ResolutionNote
        }, "Report escalated to the required authority."));
    }
}

public record WalletTopUpRejectRequest(string? Reason);
