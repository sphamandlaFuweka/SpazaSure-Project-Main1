using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;
using System.Text.Json;

namespace SpazaSure.ProductService.Controllers;

/// <summary>
/// The Customer app's core "Verify" scan flow. Checks SpazaSure's own
/// registry first — a product's own barcode, or a specific SpazaSure QR
/// code applied to a batch — and returns a verdict plus recall/allergen
/// info when available.
///
/// NOT implemented here: the Open Food Facts fallback for barcodes
/// SpazaSure doesn't recognize at all. That's a real external API
/// integration (openfoodfacts.org) — this endpoint returns a clear
/// "not_registered" result instead, which the client can use to decide
/// whether to call Open Food Facts itself or just show "unknown product".
/// </summary>
[ApiController]
[Route("api/customer/verify")]
[Authorize]
public class VerifyController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Verify(string code, [FromQuery] string[]? myAllergies)
    {
        code = code.Trim();

        // 1. Is this a SpazaSure-issued QR code for a specific batch? Those
        //    carry recall/batch info a plain barcode lookup can't.
        var qr = await db.ProductQrCodes
            .Include(q => q.Product)
            .FirstOrDefaultAsync(q => q.QrCode == code);

        if (qr is not null)
        {
            return Ok(ApiResponse<object>.Ok(BuildResult(
                source: "spazasure_qr",
                verdict: qr.IsRecalled ? "recalled" : "genuine",
                product: qr.Product,
                batchNumber: qr.BatchNumber,
                expiryDate: qr.ExpiryDate,
                isRecalled: qr.IsRecalled,
                myAllergies: myAllergies)));
        }

        // 2. Fall back to a plain product barcode (e.g. the manufacturer's
        //    UPC/EAN printed on the packaging).
        var product = await db.Products.FirstOrDefaultAsync(p => p.Barcode == code && p.IsApproved);

        if (product is not null)
        {
            // A barcode alone isn't batch-specific, so check if ANY of this
            // product's tracked batches have an active recall.
            var anyRecalled = await db.ProductQrCodes
                .AnyAsync(q => q.ProductId == product.Id && q.IsRecalled);

            return Ok(ApiResponse<object>.Ok(BuildResult(
                source: "spazasure_barcode",
                verdict: anyRecalled ? "recalled" : "genuine",
                product: product,
                batchNumber: null,
                expiryDate: null,
                isRecalled: anyRecalled,
                myAllergies: myAllergies)));
        }

        // 3. Not in SpazaSure's registry at all.
        return Ok(ApiResponse<object>.Ok(new
        {
            source = "not_registered",
            verdict = "unknown",
            code,
            message = "This product isn't in SpazaSure's registry yet.",
            // TODO: fall back to https://world.openfoodfacts.org/api/v2/product/{code}.json
            // for basic product info on barcodes SpazaSure doesn't track.
        }));
    }

    private static object BuildResult(
        string source, string verdict, Infrastructure.Entities.Product product,
        string? batchNumber, DateOnly? expiryDate, bool isRecalled, string[]? myAllergies)
    {
        var productAllergens = DeserializeList(product.Allergens);
        var matchedAllergies = myAllergies is { Length: > 0 }
            ? productAllergens.Where(a => myAllergies.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList()
            : [];

        return new
        {
            source,
            verdict,
            productId = product.Id,
            name = product.Name,
            description = product.Description,
            images = DeserializeList(product.Images),
            allergens = productAllergens,
            isFoodItem = product.IsFoodItem,
            isRecalled,
            batchNumber,
            expiryDate,
            allergyWarning = matchedAllergies.Count > 0
                ? new { matchedAllergens = matchedAllergies }
                : null,
        };
    }

    private static List<string> DeserializeList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
