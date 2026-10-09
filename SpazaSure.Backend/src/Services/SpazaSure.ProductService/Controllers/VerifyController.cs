using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using System.Security.Claims;
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
public class VerifyController(SpazaSureDbContext db, OpenFoodFactsService openFoodFactsService, IConfiguration config) : ControllerBase
{
    private const int MaxPinAttempts = 5;

    [HttpGet("{code}")]
    public async Task<IActionResult> Verify(
        string code,
        [FromQuery] string[]? myAllergies,
        [FromQuery] DateOnly? expiry,
        [FromQuery] string? batch,
        [FromQuery] string? packagingText,
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] string? pin)
    {
        code = code.Trim();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cloned = await LooksClonedAsync(code, lat, lng);
        batch = string.IsNullOrWhiteSpace(batch) ? null : batch.Trim();
        var scannedPackaging = !string.IsNullOrWhiteSpace(packagingText);

        // 0. A per-unit code with a hidden scratch-off PIN.
        var unit = await db.ProductUnitCodes
            .Include(u => u.Product).ThenInclude(p => p.Supplier)
            .Include(u => u.Product).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(u => u.Code == code);

        if (unit is not null)
        {
            var pinStatus = await CheckPinAsync(unit, pin, lat, lng);
            var recalled = unit.Status == "recalled";
            var unitExpiry = unit.ExpiryDate ?? expiry;
            var unitRisk = VerificationRiskEngine.Evaluate(new VerificationInput
            {
                Code = code,
                InSpazaSureRegistry = true,
                InGlobalDatabase = true,
                Recalled = recalled,
                SupplierVerified = unit.Product.Supplier.Status == "verified",
                ExpiryDate = unitExpiry,
                BatchNumber = unit.BatchNumber,
                BatchTracked = true,
                PackagingScanned = scannedPackaging,
                PackagingBarcodeMatches = !scannedPackaging || PackagingMatches(unit.Product.Name, packagingText),
                IsFood = unit.Product.IsFoodItem,
                CloneSuspected = cloned,
                PinStatus = pinStatus,
            }, today);

            return Ok(ApiResponse<object>.Ok(BuildResult(
                source: "spazasure_unit",
                verdict: recalled ? "recalled" : pinStatus is "valid" ? "genuine" : pinStatus,
                product: unit.Product,
                batchNumber: unit.BatchNumber,
                expiryDate: unitExpiry,
                isRecalled: recalled,
                myAllergies: myAllergies,
                risk: unitRisk,
                requiresPin: pinStatus == "required",
                pinStatus: pinStatus)));
        }

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
                IsFood = qr.Product.IsFoodItem, CloneSuspected = cloned,
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
                IsFood = product.IsFoodItem, CloneSuspected = cloned,
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
                IsFood = true, CloneSuspected = cloned,
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
            IsFood = true, CloneSuspected = cloned,
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

    // Validates the scratch PIN and consumes the unit on first success.
    private async Task<string> CheckPinAsync(ProductUnitCode unit, string? pin, double? lat, double? lng)
    {
        if (unit.Status == "compromised" || unit.FailedPinAttempts >= MaxPinAttempts) return "locked";
        if (string.IsNullOrWhiteSpace(pin)) return unit.Status == "consumed" ? "reused" : "required";

        var secret = config["Codes:PinSecret"] ?? config["Jwt:Secret"]!;
        var normalized = UnitCodeService.NormalizePin(pin);
        if (!UnitCodeService.PinMatches(normalized, unit.PinHash, secret))
        {
            unit.FailedPinAttempts++;
            if (unit.FailedPinAttempts >= MaxPinAttempts) unit.Status = "compromised";
            await db.SaveChangesAsync();
            return unit.Status == "compromised" ? "locked" : "invalid";
        }

        var userId = Guid.TryParse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var uid) ? uid : (Guid?)null;
        if (unit.Status == "consumed")
            return unit.ConsumedByUserId == userId ? "valid" : "reused";
        if (unit.Status != "active") return "valid";

        unit.Status = "consumed";
        unit.ConsumedAt = DateTime.UtcNow;
        unit.ConsumedByUserId = userId;
        var okLocation = SpazaSure.Shared.Helpers.AddressValidation.ValidateCoordinates(lat, lng) is null;
        unit.ConsumedLatitude = okLocation ? lat : null;
        unit.ConsumedLongitude = okLocation ? lng : null;
        await db.SaveChangesAsync();
        return "valid";
    }

    // True when the same code was scanned elsewhere recently at an impossible travel speed.
    private async Task<bool> LooksClonedAsync(string code, double? lat, double? lng)
    {
        if (lat is null || lng is null) return false;
        var since = DateTime.UtcNow.AddHours(-24);
        var recent = await db.CustomerScanEvents
            .Where(s => s.Code == code && s.CreatedAt >= since && s.Latitude != null && s.Longitude != null)
            .Select(s => new { s.Latitude, s.Longitude, s.CreatedAt })
            .ToListAsync();

        foreach (var s in recent)
        {
            var km = DistanceKm(lat.Value, lng.Value, s.Latitude!.Value, s.Longitude!.Value);
            var hours = Math.Max((DateTime.UtcNow - s.CreatedAt).TotalHours, 1.0 / 60);
            if (km > 100 && km / hours > 250) return true;
        }
        return false;
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
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
        VerificationRisk risk, bool requiresPin = false, string? pinStatus = null)
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
            requiresPin,
            pinStatus,
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
