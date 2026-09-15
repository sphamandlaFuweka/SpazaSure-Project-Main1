using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using SpazaSure.Shared.Storage;
using System.Security.Claims;

namespace SpazaSure.ComplianceService.Controllers;

/// <summary>
/// Suspicious-product / shop-conduct reports, filed by any authenticated
/// user — customers reporting a scanned product, or retailers reporting a
/// shop's conduct (fake goods, expired stock, pricing, hygiene). This is
/// the starting point for the admin escalation-to-health-authority flow
/// described in the system manual — that escalation itself (the admin-side
/// "build an evidence email" step) isn't implemented here yet, just
/// create/list.
/// </summary>
[ApiController]
[Route("api/customer/reports")]
[Authorize]
public class ReportsController(SpazaSureDbContext db, IFileStorageService storage) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record CreateReportRequest(
        string? Barcode,
        Guid? ProductId,
        string? ReportType,
        string? ShopName,
        bool IsAnonymous,
        string Description,
        string? PhotoUrl,
        string? BatchNumber,
        DateOnly? ExpiryDate,
        string? PurchaseLocation,
        string? SupplierName
    );

    /// <summary>
    /// Upload evidence photo before creating the report — call this first,
    /// then pass the returned url as PhotoUrl in the create request. Kept
    /// separate from Create() because that's a JSON body, not multipart.
    /// </summary>
    [HttpPost("photo")]
    public async Task<IActionResult> UploadPhoto(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest(ApiResponse.Fail("No file provided."));
        if (file.Length > 8 * 1024 * 1024) return BadRequest(ApiResponse.Fail("Image must be under 8MB."));

        var ext = Path.GetExtension(file.FileName);
        var key = $"report-photos/{UserId}/{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";

        string url;
        await using (var stream = file.OpenReadStream())
        {
            url = await storage.SaveAsync(stream, key, file.ContentType, HttpContext.RequestAborted);
        }

        return Ok(ApiResponse<object>.Ok(new { url }));
    }

    [HttpGet]
    public async Task<IActionResult> GetMyReports()
    {
        var reports = await db.Reports
            .Where(r => r.ReporterUserId == UserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Barcode,
                r.ProductId,
                productName = r.Product != null ? r.Product.Name : null,
                r.ReportType,
                r.ShopName,
                r.IsAnonymous,
                r.Description,
                r.PhotoUrl,
                r.Status,
                r.EscalatedTo,
                r.CreatedAt,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(reports));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var report = await db.Reports
            .Where(r => r.Id == id && r.ReporterUserId == UserId)
            .Select(r => new
            {
                r.Id,
                r.Barcode,
                r.ProductId,
                productName = r.Product != null ? r.Product.Name : null,
                r.ReportType,
                r.ShopName,
                r.IsAnonymous,
                r.Description,
                r.PhotoUrl,
                r.BatchNumber,
                r.ExpiryDate,
                r.PurchaseLocation,
                r.SupplierName,
                r.Status,
                r.EscalatedTo,
                r.ResolutionNote,
                r.CreatedAt,
            })
            .FirstOrDefaultAsync();

        if (report is null) return NotFound(ApiResponse.Fail("Report not found."));
        return Ok(ApiResponse<object>.Ok(report));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Description))
            return BadRequest(ApiResponse.Fail("A description of the issue is required."));

        // If a product was matched during the scan, tie the report to it —
        // but don't hard-fail if the caller passes a stale/invalid id.
        Guid? productId = null;
        if (req.ProductId is { } pid && await db.Products.AnyAsync(p => p.Id == pid))
            productId = pid;

        var report = new Report
        {
            ReporterUserId = UserId,
            ProductId = productId,
            Barcode = req.Barcode,
            ReportType = req.ReportType,
            ShopName = req.ShopName,
            IsAnonymous = req.IsAnonymous,
            Description = req.Description.Trim(),
            PhotoUrl = req.PhotoUrl,
            BatchNumber = req.BatchNumber,
            ExpiryDate = req.ExpiryDate,
            PurchaseLocation = req.PurchaseLocation,
            SupplierName = req.SupplierName,
            Status = "submitted",
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { report.Id, report.Status }, "Report submitted. Thanks for helping keep other shoppers safe."));
    }

    public record EscalateReportRequest(string EscalatedTo, string? ResolutionNote, string Status);

    [HttpPatch("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] EscalateReportRequest req)
    {
        var report = await db.Reports
            .FirstOrDefaultAsync(r => r.Id == id && r.ReporterUserId == UserId);

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
            report.ResolutionNote,
        }, "Report escalated successfully."));
    }
}
