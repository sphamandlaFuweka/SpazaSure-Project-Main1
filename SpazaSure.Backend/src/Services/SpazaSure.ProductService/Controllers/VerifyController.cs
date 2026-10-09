using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.ProductService.Services;
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
public class VerifyController(SpazaSureDbContext db, OpenFoodFactsService openFoodFactsService) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Verify(
        string code,
        [FromQuery] string[]? myAllergies,
        [FromQuery] DateOnly? expiry,
        [FromQuery] string? batch,
        [FromQuery] string? packagingText)
    {
        code = code.Trim();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        batch = string.IsNullOrWhiteSpace(batch) ? null : batch.Trim();
        var scannedPackaging = !string.IsNullOrWhiteSpace(packagingText);

        // 1. Is this a SpazaSure-issued QR code for a specific batch? Those
        //    carry recall/batch info a plain barcode lookup can't.
        var qr = await db.ProductQrCodes
            .Include(q => q.Product).ThenInclude(p => p.Supplier)
            .Include(q => q.Product).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(q => q.QrCode == code);

        if (qr is not null)
        {
            var qrExpiry = qr.ExpiryDate ?? expiry;
            var qrBatch = qr.BatchNumber ?? batch;
            var risk = VerificationRiskEngine.Evaluate(new VerificationInput
            {
                Code = code,
                InSpazaSureRegistry = true,
                InGlobalDatabase = true,
                Recalled = qr.IsRecalled,
                SupplierVerified = qr.Product.Supplier.Status == "verified",
                ExpiryDate = qrExpiry,
                BatchNumber = qrBatch,
                BatchTracked = qr.BatchNumber is not null,
                PackagingScanned = scannedPackaging,
                PackagingBarcodeMatches = !scannedPackaging || PackagingMatches(qr.Product.Name, packagingText),
                HasIngredients = false,
                IsFood = qr.Product.IsFoodItem,
            }, today);

            return Ok(ApiResponse<object>.Ok(BuildResult(
                source: "spazasure_qr",
                verdict: qr.IsRecalled ? "recalled" : "genuine",
                product: qr.Product,
                batchNumber: qrBatch,
                expiryDate: qrExpiry,
                isRecalled: qr.IsRecalled,
                myAllergies: myAllergies,
                risk: risk)));
        }

        // 2. Fall back to a plain product barcode (e.g. the manufacturer's
        //    UPC/EAN printed on the packaging).
        var product = await db.Products
            .Include(p => p.Supplier)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Barcode == code && p.IsApproved);

        if (product is not null)
        {
            // A barcode alone isn't batch-specific, so check if ANY of this
            // product's tracked batches have an active recall.
            var anyRecalled = await db.ProductQrCodes
                .AnyAsync(q => q.ProductId == product.Id && q.IsRecalled);
            var batchTracked = batch is not null && await db.ProductQrCodes
                .AnyAsync(q => q.ProductId == product.Id && q.BatchNumber == batch);

            var risk = VerificationRiskEngine.Evaluate(new VerificationInput
            {
                Code = code,
                InSpazaSureRegistry = true,
                InGlobalDatabase = true,
                Recalled = anyRecalled,
                SupplierVerified = product.Supplier.Status == "verified",
                ExpiryDate = expiry,
                BatchNumber = batch,
                BatchTracked = batchTracked,
                PackagingScanned = scannedPackaging,
                PackagingBarcodeMatches = !scannedPackaging || PackagingMatches(product.Name, packagingText),
                IsFood = product.IsFoodItem,
            }, today);

            return Ok(ApiResponse<object>.Ok(BuildResult(
                source: "spazasure_barcode",
                verdict: anyRecalled ? "recalled" : "genuine",
                product: product,
                batchNumber: batch,
                expiryDate: expiry,
                isRecalled: anyRecalled,
                myAllergies: myAllergies,
                risk: risk)));
        }

        // 3. Not in SpazaSure's registry at all: use Open Food Facts for a
        //    lightweight product lookup if the product exists globally.
        var openFoodFactsProduct = await openFoodFactsService.LookupAsync(code);

        if (openFoodFactsProduct is not null)
        {
            var fallbackAllergens = openFoodFactsProduct.Allergens ?? [];
            var matchedAllergies = myAllergies is { Length: > 0 }
                ? fallbackAllergens.Where(a => myAllergies.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList()
                : [];

            var offRisk = VerificationRiskEngine.Evaluate(new VerificationInput
            {
                Code = code,
                InGlobalDatabase = true,
                ExpiryDate = expiry,
                BatchNumber = batch,
                PackagingScanned = scannedPackaging,
                PackagingBarcodeMatches = !scannedPackaging || PackagingMatches(openFoodFactsProduct.Name, packagingText),
                HasIngredients = !string.IsNullOrWhiteSpace(openFoodFactsProduct.Ingredients),
                IsFood = true,
            }, today);

            return Ok(ApiResponse<object>.Ok(new
            {
                source = "open_food_facts",
                verdict = "unknown",
                code,
                name = openFoodFactsProduct.Name ?? openFoodFactsProduct.GenericName ?? "Unknown product",
                description = openFoodFactsProduct.Description ?? openFoodFactsProduct.Ingredients,
                images = new[] { openFoodFactsProduct.ImageUrl }.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray(),
                allergens = fallbackAllergens,
                isFoodItem = true,
                allergyWarning = matchedAllergies.Count > 0 ? new { matchedAllergens = matchedAllergies } : null,
                message = "This product is not in SpazaSure's registry, but basic product details were found in Open Food Facts.",
                batchNumber = batch,
                expiryDate = expiry,
                brand = openFoodFactsProduct.Brands,
                quantity = openFoodFactsProduct.Quantity,
                category = openFoodFactsProduct.Categories,
                origin = openFoodFactsProduct.Origins,
                countriesSold = openFoodFactsProduct.Countries,
                labels = openFoodFactsProduct.Labels,
                nutriScore = openFoodFactsProduct.NutriScore,
                ingredients = openFoodFactsProduct.Ingredients,
                riskScore = offRisk.Score,
                riskLevel = offRisk.Level,
                riskHeadline = offRisk.Headline,
                indicators = offRisk.Indicators,
                checks = offRisk.Checks,
                tips = offRisk.Tips,
            }));
        }

        var unknownRisk = VerificationRiskEngine.Evaluate(new VerificationInput
        {
            Code = code,
            ExpiryDate = expiry,
            BatchNumber = batch,
            PackagingScanned = scannedPackaging,
            IsFood = true,
        }, today);

        return Ok(ApiResponse<object>.Ok(new
        {
            source = "not_registered",
            verdict = "unknown",
            code,
            message = "This product isn't in SpazaSure's registry yet.",
            batchNumber = batch,
            expiryDate = expiry,
            riskScore = unknownRisk.Score,
            riskLevel = unknownRisk.Level,
            riskHeadline = unknownRisk.Headline,
            indicators = unknownRisk.Indicators,
            checks = unknownRisk.Checks,
            tips = unknownRisk.Tips,
        }));
    }

    // True when any distinctive word of the product name appears in the OCR text.
    private static bool PackagingMatches(string? productName, string? packagingText)
    {
        if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(packagingText))
            return true;
        var words = productName.Split([' ', '-', '/', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4);
        var any = false;
        foreach (var w in words)
        {
            any = true;
            if (packagingText.Contains(w, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return !any;
    }

    private static object BuildResult(
        string source, string verdict, Infrastructure.Entities.Product product,
        string? batchNumber, DateOnly? expiryDate, bool isRecalled, string[]? myAllergies,
        VerificationRisk risk)
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
            code = product.Barcode,
            sku = product.Sku,
            unit = product.Unit,
            category = product.Category?.Name,
            supplierName = product.Supplier.CompanyName,
            supplierVerified = product.Supplier.Status == "verified",
            supplierCity = product.Supplier.City,
            supplierProvince = product.Supplier.Province,
            riskScore = risk.Score,
            riskLevel = risk.Level,
            riskHeadline = risk.Headline,
            indicators = risk.Indicators,
            checks = risk.Checks,
            tips = risk.Tips,
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
