using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
/// info when available. Codes SpazaSure doesn't know are looked up in the
/// open product databases and explained (nutrition, processing, additives),
/// with an offline reading of the barcode itself either way.
/// </summary>
[ApiController]
[Route("api/customer/verify")]
[Authorize]
public class VerifyController(
    SpazaSureDbContext db,
    OpenFoodFactsService openFoodFactsService,
    PackagingVisionService packagingVision,
    IMemoryCache cache,
    IConfiguration config) : ControllerBase
{
    private const int MaxPinAttempts = 5;
    // Each photo is a paid vision call, so one account can't run up the bill
    private const int MaxPackagingAnalysesPerDay = 40;
    private const long MaxPhotoBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> PhotoTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];

    /// <summary>
    /// Reads a photo of the pack itself, for when there is no barcode to scan or it isn't
    /// registered: what the product is, who made it, what the label says, and whether the
    /// pack looks genuine. The barcode read off the pack is cross-checked against SpazaSure
    /// and the open product databases. Returns source "unavailable" when the vision model
    /// is off or fails, so the app shows its own reading of the text instead.
    /// </summary>
    [HttpPost("packaging")]
    [RequestSizeLimit(MaxPhotoBytes + 64 * 1024)]
    public async Task<IActionResult> AnalyzePackaging(
        IFormFile photo,
        [FromForm] string? ocrText,
        [FromForm] string? barcode,
        CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(ApiResponse.Fail("Add a photo of the packaging."));
        if (photo.Length > MaxPhotoBytes)
            return BadRequest(ApiResponse.Fail("That photo is too large. Try again with a smaller one."));
        // The app sends application/octet-stream, so fall back to the file name
        var mediaType = photo.ContentType?.ToLowerInvariant() ?? "";
        if (!PhotoTypes.Contains(mediaType))
            mediaType = Path.GetExtension(photo.FileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg",
            };

        var userKey = $"packaging-analyses:{User.FindFirstValue(ClaimTypes.NameIdentifier)}:{DateTime.UtcNow:yyyyMMdd}";
        var usedToday = cache.Get<int>(userKey);
        if (usedToday >= MaxPackagingAnalysesPerDay)
            return Ok(ApiResponse<object>.Ok(new { source = "unavailable", reason = "daily_limit" }));

        await using var stream = photo.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var text = string.IsNullOrWhiteSpace(ocrText) ? null : ocrText.Length > 3000 ? ocrText[..3000] : ocrText;

        var result = await packagingVision.AnalyzeAsync(buffer.ToArray(), mediaType, text, cancellationToken);
        if (result.Analysis is not { } analysis)
            return Ok(ApiResponse<object>.Ok(new { source = "unavailable", reason = result.Unavailable }));
        cache.Set(userKey, usedToday + 1, TimeSpan.FromDays(1));

        var checks = new List<PackagingCheck>();
        void Check(string label, string status, string detail) => checks.Add(new(label, status, detail));

        // The digits printed on the pack win over the phone's guess; both are compared.
        var printed = Digits(analysis.BarcodeDigits);
        var scanned = Digits(barcode);
        var code = printed ?? scanned;
        if (printed is not null && scanned is not null && printed != scanned)
            Check("Barcode", "warn", $"The phone read {scanned} but the digits printed on the pack are {printed}. Re-scan the barcode to be sure.");

        BarcodeInsight? barcodeInfo = code is null ? null : BarcodeInsights.Analyze(code);
        if (barcodeInfo is { IsRetailBarcode: true, CheckDigitValid: false })
            Check("Barcode", "fail", $"{code} fails the barcode check digit. Genuine barcodes always pass; this one may be misprinted or made up.");

        var packName = string.Join(' ', new[] { analysis.Brand, analysis.ProductName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // 1. Is this product on SpazaSure, by barcode first and then by name?
        Product? registry = null;
        if (code is not null)
            registry = await db.Products.Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Barcode == code && p.IsApproved, cancellationToken);
        if (registry is not null)
        {
            if (packName.Length > 0 && !NamesOverlap(packName, $"{registry.Brand} {registry.Name}"))
                Check("Barcode matches product", "fail",
                    $"On SpazaSure this barcode belongs to \"{registry.Name}\", but the pack says \"{packName}\". A barcode copied from another product is a common sign of a fake.");
            else
                Check("Barcode matches product", "pass", $"The barcode is registered on SpazaSure for {registry.Name}.");
            if (!string.IsNullOrWhiteSpace(registry.Manufacturer) && !string.IsNullOrWhiteSpace(analysis.Manufacturer)
                && !NamesOverlap(registry.Manufacturer, analysis.Manufacturer))
                Check("Manufacturer", "warn",
                    $"SpazaSure lists {registry.Manufacturer} as the maker, but the pack names {analysis.Manufacturer}.");
        }
        else if (packName.Length > 0)
        {
            // Search on the most distinctive word (the brand when it was read), rank the rest in memory
            var words = Words(packName).ToList();
            var key = $"%{Words(analysis.Brand ?? "").FirstOrDefault() ?? words.FirstOrDefault()}%";
            List<Product> candidates = words.Count == 0 ? [] : await db.Products.Include(p => p.Supplier)
                .Where(p => p.IsApproved && (EF.Functions.ILike(p.Name, key) || (p.Brand != null && EF.Functions.ILike(p.Brand, key))))
                .Take(20)
                .ToListAsync(cancellationToken);
            registry = candidates
                .OrderByDescending(p => Words($"{p.Brand} {p.Name}").Intersect(words).Count())
                .FirstOrDefault(p => NamesOverlap(packName, $"{p.Brand} {p.Name}"));
            if (registry is not null && code is not null && registry.Barcode is { Length: > 0 } && registry.Barcode != code)
                Check("Barcode matches product", "warn",
                    $"SpazaSure has {registry.Name} with barcode {registry.Barcode}, but this pack shows {code}. It may be a different size or variant; compare them.");
        }

        // 2. Not on SpazaSure: what do the open databases say this barcode is?
        OpenFoodFactsProductSnapshot? database = null;
        if (registry is null && code is not null && barcodeInfo is { IsRetailBarcode: true })
        {
            database = (await openFoodFactsService.LookupAsync(code, cancellationToken)).Product;
            var dbName = $"{database?.Brands} {database?.Name}".Trim();
            if (database is not null && packName.Length > 0 && dbName.Length > 0)
            {
                if (NamesOverlap(packName, dbName))
                    Check("Barcode matches product", "pass", $"{database.DataSource} lists this barcode as {dbName}, which matches the pack.");
                else
                    Check("Barcode matches product", "fail",
                        $"{database.DataSource} lists this barcode as \"{dbName}\", but the pack says \"{packName}\". A barcode copied from another product is a common sign of a fake.");
            }
        }

        var expiry = DateOnly.TryParse(analysis.ExpiryDate, out var e) ? e : (DateOnly?)null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (expiry is { } exp)
        {
            if (exp < today) Check("Expiry date", "fail", $"Expired on {exp:d MMM yyyy}. Do not sell or use it.");
            else if (exp <= today.AddDays(30)) Check("Expiry date", "warn", $"Expires soon, on {exp:d MMM yyyy}.");
            else Check("Expiry date", "pass", $"Good until {exp:d MMM yyyy}.");
        }
        if (string.IsNullOrWhiteSpace(analysis.Manufacturer))
            Check("Manufacturer", "warn", "No manufacturer or importer could be read. South African retail packs must name one; check the other sides.");

        var riskLevel = analysis.Verdict switch
        {
            "likely_counterfeit" => "high",
            _ when checks.Any(c => c.Status == "fail") => "high",
            "some_concerns" => "medium",
            _ when checks.Any(c => c.Status == "warn") => "medium",
            "looks_genuine" => "low",
            _ => "unknown",
        };

        return Ok(ApiResponse<object>.Ok(new
        {
            source = "packaging_ai",
            analysis,
            barcode = code,
            barcodeInfo,
            registryProduct = registry is null ? null : new
            {
                productId = registry.Id,
                name = registry.Name,
                brand = registry.Brand,
                manufacturer = registry.Manufacturer,
                barcode = registry.Barcode,
                supplierName = registry.Supplier.CompanyName,
                supplierVerified = registry.Supplier.Status == "verified",
            },
            databaseProduct = database is null ? null : new
            {
                name = database.Name,
                brand = database.Brands,
                quantity = database.Quantity,
                dataSource = database.DataSource,
                imageUrl = database.ImageUrl,
            },
            checks,
            riskLevel,
        }));
    }

    private static string? Digits(string? value)
    {
        var digits = value is null ? "" : new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 8 ? digits : null;
    }

    private static readonly HashSet<string> CommonWords =
        ["the", "and", "with", "for", "new", "original", "pack", "brand", "limited", "ltd", "pty", "(pty)"];

    private static IEnumerable<string> Words(string text) =>
        text.ToLowerInvariant()
            .Split([' ', '-', '/', ',', '.', '&', '(', ')', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !CommonWords.Contains(w) && !w.All(char.IsDigit))
            .Distinct();

    // True when the two names share a distinctive word, e.g. a brand.
    private static bool NamesOverlap(string a, string b) => Words(a).Intersect(Words(b)).Any();

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

        // 3. Not in SpazaSure's registry at all: look it up in the open product
        //    databases (food, toiletries, household, pet food) and explain it.
        var barcodeInfo = BarcodeInsights.Analyze(code);
        var lookup = await openFoodFactsService.LookupAsync(code);
        var openFoodFactsProduct = lookup.Product;

        if (openFoodFactsProduct is not null)
        {
            var fallbackAllergens = openFoodFactsProduct.Allergens ?? [];
            var matchedAllergies = myAllergies is { Length: > 0 }
                ? fallbackAllergens.Where(a => myAllergies.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList()
                : [];
            var insights = ProductInsights.Build(openFoodFactsProduct);
            var isFood = insights.ProductType == "food";

            var offRisk = VerificationRiskEngine.Evaluate(new VerificationInput
            {
                Code = code,
                InGlobalDatabase = true,
                ExpiryDate = expiry,
                BatchNumber = batch,
                PackagingScanned = scannedPackaging,
                PackagingBarcodeMatches = !scannedPackaging || PackagingMatches(openFoodFactsProduct.Name, packagingText),
                HasIngredients = !string.IsNullOrWhiteSpace(openFoodFactsProduct.Ingredients),
                IsFood = isFood, CloneSuspected = cloned,
            }, today);

            return Ok(ApiResponse<object>.Ok(new
            {
                source = "open_food_facts",
                verdict = "unknown",
                code,
                name = openFoodFactsProduct.Name ?? openFoodFactsProduct.GenericName ?? "Unknown product",
                description = openFoodFactsProduct.Description ?? openFoodFactsProduct.Ingredients,
                images = new[] { openFoodFactsProduct.ImageUrl }.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray(),
                // Reference photos the customer can compare with the pack in their hand
                gallery = openFoodFactsProduct.Photos.Select(ph => new { url = ph.Url, label = ph.Label, source = openFoodFactsProduct.DataSource }),
                allergens = fallbackAllergens,
                isFoodItem = isFood,
                allergyWarning = matchedAllergies.Count > 0 ? new { matchedAllergens = matchedAllergies } : null,
                message = $"This product is not in SpazaSure's registry, but product details were found in {openFoodFactsProduct.DataSource}.",
                insights,
                barcodeInfo,
                dataSource = openFoodFactsProduct.DataSource,
                servingSize = openFoodFactsProduct.ServingSize,
                packaging = openFoodFactsProduct.Packaging,
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
            GlobalDatabaseUnavailable = lookup.Unavailable,
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
            message = lookup.Unavailable
                ? "This product isn't in SpazaSure's registry, and the global product databases can't be reached right now. Try again later."
                : $"This product isn't in SpazaSure's registry, and none of the global product databases we searched ({string.Join(", ", OpenFoodFactsService.DatabaseNames)}) has it yet.",
            lookupUnavailable = lookup.Unavailable,
            barcodeInfo,
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
            ingredients = product.Ingredients,
            info = ProductInfo.Project(product),
            // The supplier's own "how to spot a fake" checks for this product.
            authenticityTips = ProductInfo.Tips(product),
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

/// <summary>One cross-check of a packaging photo: pass, warn or fail.</summary>
public sealed record PackagingCheck(string Label, string Status, string Detail);
