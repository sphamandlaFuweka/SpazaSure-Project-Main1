using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.ProductService.Services;
using SpazaSure.Shared.Models;
using System.Text.Json;

namespace SpazaSure.ProductService.Controllers;

/// <summary>
/// The Customer app's "Verify a product" flow: identify the product (SpazaSure
/// QR, SpazaSure barcode, then Open Food Facts), run the checks, and return a
/// risk level with the indicators behind it. It never declares a product
/// genuine or counterfeit; unknown products are "not yet verified".
/// </summary>
[ApiController]
[Route("api/customer/verify")]
[Authorize]
public class VerifyController(
    SpazaSureDbContext db,
    OpenFoodFactsService openFoodFactsService,
    IConfiguration config) : ControllerBase
{
    private const string Disclaimer =
        "This check does not guarantee authenticity or product safety. It is based only on the information available to SpazaSure.";

    [HttpGet("{code}")]
    public async Task<IActionResult> Verify(
        string code,
        [FromQuery] string[]? myAllergies,
        [FromQuery] DateOnly? expiryDate,
        [FromQuery] string? productName,
        [FromQuery] string? packagingText)
    {
        code = code.Trim();
        if (packagingText?.Length > 800) packagingText = packagingText[..800];
        var thresholds = new RiskThresholds(
            config.GetValue("Verification:Thresholds:Review", 21),
            config.GetValue("Verification:Thresholds:Suspicious", 51),
            config.GetValue("Verification:Thresholds:High", 76));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. A SpazaSure-issued QR code identifies a specific batch.
        var qr = await db.ProductQrCodes
            .Include(q => q.Product).ThenInclude(p => p.Supplier)
            .FirstOrDefaultAsync(q => q.QrCode == code);

        if (qr is not null)
        {
            return Ok(ApiResponse<object>.Ok(await BuildRegisteredResult(
                "spazasure_qr", qr.Product, code, qr.BatchNumber, expiryDate ?? qr.ExpiryDate,
                qr.IsRecalled, productName, packagingText, myAllergies, thresholds, today)));
        }

        // 2. A plain product barcode (e.g. the manufacturer's EAN/UPC).
        var product = await db.Products
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Barcode == code && p.IsApproved);

        if (product is not null)
        {
            var anyRecalled = await db.ProductQrCodes
                .AnyAsync(q => q.ProductId == product.Id && q.IsRecalled);
            return Ok(ApiResponse<object>.Ok(await BuildRegisteredResult(
                "spazasure_barcode", product, code, null, expiryDate,
                anyRecalled, productName, packagingText, myAllergies, thresholds, today)));
        }

        // 3. Not in SpazaSure's registry: Open Food Facts gives basic details.
        var priorReports = await CountReports(null, code);
        var off = await openFoodFactsService.LookupAsync(code);
        var assessment = VerificationRiskEngine.Assess(
            new RiskInput(false, off is not null, false, expiryDate, null, priorReports), today, thresholds);

        if (off is not null)
        {
            var allergens = off.Allergens ?? [];
            var matched = MatchAllergies(allergens, myAllergies);
            return Ok(ApiResponse<object>.Ok(new
            {
                source = "open_food_facts",
                verdict = "unknown",
                code,
                name = off.Name ?? off.GenericName ?? "Unknown product",
                description = off.Description ?? off.Ingredients,
                images = new[] { off.ImageUrl }.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray(),
                allergens,
                isFoodItem = true,
                allergyWarning = matched.Count > 0 ? new { matchedAllergens = matched } : null,
                expiryDate,
                riskScore = assessment.Score,
                riskLevel = assessment.Level,
                indicators = assessment.Indicators,
                checks = assessment.Checks,
                tips = GenericTips(),
                priorReports,
                disclaimer = Disclaimer,
                message = "This product is not in SpazaSure's registry, but basic details were found in Open Food Facts.",
            }));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            source = "not_registered",
            verdict = "unknown",
            code,
            expiryDate,
            riskScore = assessment.Score,
            riskLevel = assessment.Level,
            indicators = assessment.Indicators,
            checks = assessment.Checks,
            tips = GenericTips(),
            priorReports,
            disclaimer = Disclaimer,
            message = "We couldn't find this product in the SpazaSure database. This does not mean it is counterfeit; it may simply not be registered yet."
        }));
    }

    private async Task<object> BuildRegisteredResult(
        string source, Product product, string code, string? batchNumber, DateOnly? expiryDate,
        bool isRecalled, string? enteredName, string? packagingText, string[]? myAllergies,
        RiskThresholds thresholds, DateOnly today)
    {
        var priorReports = await CountReports(product.Id, product.Barcode);
        var confidence = VerificationRiskEngine.IdentityConfidence(product.Name, packagingText);
        bool? nameMatches = !string.IsNullOrWhiteSpace(enteredName)
            ? VerificationRiskEngine.NamesMatch(product.Name, enteredName)
            : confidence switch { null => null, >= 0.5 => true, 0 => false, _ => null };
        var assessment = VerificationRiskEngine.Assess(
            new RiskInput(true, false, isRecalled, expiryDate, nameMatches, priorReports),
            today, thresholds);

        var allergens = DeserializeList(product.Allergens);
        var matched = MatchAllergies(allergens, myAllergies);

        return new
        {
            source,
            verdict = isRecalled ? "recalled" : "genuine",
            code,
            productId = product.Id,
            name = product.Name,
            description = product.Description,
            barcode = product.Barcode,
            supplierName = product.Supplier?.CompanyName,
            images = DeserializeList(product.Images),
            allergens,
            isFoodItem = product.IsFoodItem,
            isRecalled,
            batchNumber,
            expiryDate,
            allergyWarning = matched.Count > 0 ? new { matchedAllergens = matched } : null,
            riskScore = assessment.Score,
            riskLevel = assessment.Level,
            identityConfidence = confidence,
            indicators = assessment.Indicators,
            checks = assessment.Checks,
            tips = ProductTips(product),
            priorReports,
            disclaimer = Disclaimer,
        };
    }

    private Task<int> CountReports(Guid? productId, string? barcode) =>
        db.Reports.CountAsync(r => r.Status != "dismissed" &&
            ((productId != null && r.ProductId == productId) ||
             (barcode != null && r.Barcode == barcode)));

    private static List<string> MatchAllergies(IEnumerable<string> productAllergens, string[]? mine) =>
        mine is { Length: > 0 }
            ? productAllergens.Where(a => mine.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList()
            : [];

    private static List<string> ProductTips(Product product)
    {
        var tips = new List<string>
        {
            $"Check the product name on the pack reads \"{product.Name}\".",
            "Compare the logo, colours and print quality with a pack you trust.",
        };
        if (!string.IsNullOrWhiteSpace(product.Barcode))
            tips.Add($"The printed barcode number should read {product.Barcode}.");
        if (!string.IsNullOrWhiteSpace(product.Supplier?.CompanyName))
            tips.Add($"SpazaSure lists {product.Supplier.CompanyName} as the supplier. Check the manufacturer details on the pack.");
        tips.Add("Look for a batch or lot number and an expiry or best-before date, clearly and evenly printed.");
        if (product.IsFoodItem)
            tips.Add("Check the ingredients list for spelling mistakes or missing information.");
        return tips;
    }

    private static List<string> GenericTips() =>
    [
        "Compare the logo, colours and print quality with a pack you trust.",
        "Look for a batch or lot number and an expiry or best-before date.",
        "Check the manufacturer details and look for spelling mistakes.",
        "Be cautious if the price is far below what other shops charge.",
    ];

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
