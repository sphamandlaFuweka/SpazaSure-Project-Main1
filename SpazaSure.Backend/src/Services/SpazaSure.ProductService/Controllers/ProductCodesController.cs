using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.ProductService.Services;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.ProductService.Controllers;

/// <summary>Suppliers generate per-unit authenticity codes (open code plus scratch-off PIN) for a product batch.</summary>
[ApiController]
[Route("api/supplier/products/{productId:guid}/codes")]
[Authorize]
public class ProductCodesController(SpazaSureDbContext db, IConfiguration config) : ControllerBase
{
    private const int MaxPerBatch = 5000;
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string PinSecret => config["Codes:PinSecret"] ?? config["Jwt:Secret"]!;

    public record GenerateRequest(string BatchNumber, DateOnly? ExpiryDate, int Quantity);

    private async Task<Product?> OwnedProductAsync(Guid productId)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return null;
        return await db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.SupplierId == supplier.Id);
    }

    /// <summary>Creates the codes. The PINs are returned once here and can never be read again.</summary>
    [HttpPost]
    public async Task<IActionResult> Generate(Guid productId, [FromBody] GenerateRequest req)
    {
        var product = await OwnedProductAsync(productId);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));
        if (!product.IsApproved) return BadRequest(ApiResponse.Fail("The product must be approved before codes can be issued."));

        var batch = req.BatchNumber?.Trim() ?? "";
        if (batch.Length is < 2 or > 40) return BadRequest(ApiResponse.Fail("Enter a batch number (2 to 40 characters)."));
        if (req.Quantity is < 1 or > MaxPerBatch) return BadRequest(ApiResponse.Fail($"Quantity must be between 1 and {MaxPerBatch}."));
        if (req.ExpiryDate is { } exp && exp < DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(ApiResponse.Fail("The expiry date is in the past."));
        if (await db.ProductUnitCodes.AnyAsync(u => u.ProductId == productId && u.BatchNumber == batch))
            return Conflict(ApiResponse.Fail("Codes were already generated for this batch number."));

        var units = new List<ProductUnitCode>(req.Quantity);
        var issued = new List<object>(req.Quantity);
        var seen = new HashSet<string>();
        while (units.Count < req.Quantity)
        {
            var code = UnitCodeService.NewCode();
            if (!seen.Add(code)) continue;
            var pin = UnitCodeService.NewPin();
            units.Add(new ProductUnitCode
            {
                ProductId = productId,
                Code = code,
                PinHash = UnitCodeService.HashPin(pin, PinSecret),
                BatchNumber = batch,
                ExpiryDate = req.ExpiryDate,
            });
            issued.Add(new { code, pin = UnitCodeService.FormatPin(pin) });
        }

        db.ProductUnitCodes.AddRange(units);
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            productId,
            batchNumber = batch,
            expiryDate = req.ExpiryDate,
            quantity = units.Count,
            codes = issued,
        }, "Codes generated. Download the list now: the PINs cannot be shown again."));
    }

    [HttpGet("batches")]
    public async Task<IActionResult> Batches(Guid productId)
    {
        if (await OwnedProductAsync(productId) is null) return NotFound(ApiResponse.Fail("Product not found."));

        var batches = await db.ProductUnitCodes
            .Where(u => u.ProductId == productId)
            .GroupBy(u => u.BatchNumber)
            .Select(g => new
            {
                batchNumber = g.Key,
                expiryDate = g.Max(u => u.ExpiryDate),
                total = g.Count(),
                verified = g.Count(u => u.Status == "consumed"),
                compromised = g.Count(u => u.Status == "compromised"),
                recalled = g.Count(u => u.Status == "recalled"),
                createdAt = g.Min(u => u.CreatedAt),
            })
            .OrderByDescending(b => b.createdAt)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(batches));
    }

    /// <summary>Marks every unit in a batch as recalled so customers are warned on their next scan.</summary>
    [HttpPatch("batches/{batchNumber}/recall")]
    public async Task<IActionResult> Recall(Guid productId, string batchNumber)
    {
        if (await OwnedProductAsync(productId) is null) return NotFound(ApiResponse.Fail("Product not found."));

        var count = await db.ProductUnitCodes
            .Where(u => u.ProductId == productId && u.BatchNumber == batchNumber)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "recalled"));

        return count == 0
            ? NotFound(ApiResponse.Fail("Batch not found."))
            : Ok(ApiResponse<object>.Ok(new { batchNumber, recalled = count }, "Batch recalled."));
    }
}
