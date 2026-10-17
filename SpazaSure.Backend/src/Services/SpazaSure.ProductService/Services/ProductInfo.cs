using System.Text.Json;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.ProductService.Services;

public record NutritionRow(string? Nutrient, string? Per100, string? PerServing);

public record SpecificationRow(string? Label, string? Value);

/// <summary>
/// The product information a supplier fills in, like a retail product page. Every field is
/// optional. On update, a null request keeps what is stored; a field left null is cleared.
/// </summary>
public record ProductInfoRequest(
    string? Brand = null,
    string? PackSize = null,
    string? CountryOfOrigin = null,
    string? Manufacturer = null,
    string? Ingredients = null,
    List<string>? Allergens = null,
    string? ServingSize = null,
    string? ServingsPerPack = null,
    List<NutritionRow>? Nutrition = null,
    string? UsageInstructions = null,
    string? StorageInstructions = null,
    string? Warnings = null,
    List<SpecificationRow>? Specifications = null,
    List<string>? AuthenticityTips = null);

/// <summary>Validates, stores and projects product information.</summary>
public static class ProductInfo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Copies a validated request onto the product. Returns an error message when invalid.</summary>
    public static string? Apply(Product product, ProductInfoRequest req)
    {
        string? error = null;
        string? Text(string? value, int max, string label)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return null;
            if (trimmed.Length > max) error ??= $"{label} can be up to {max} characters.";
            return trimmed;
        }

        var brand = Text(req.Brand, 120, "Brand");
        var packSize = Text(req.PackSize, 60, "Pack size");
        var origin = Text(req.CountryOfOrigin, 80, "Country of origin");
        var manufacturer = Text(req.Manufacturer, 160, "Manufacturer");
        var ingredients = Text(req.Ingredients, 4000, "Ingredients");
        var servingSize = Text(req.ServingSize, 60, "Serving size");
        var servings = Text(req.ServingsPerPack, 60, "Servings per pack");
        var usage = Text(req.UsageInstructions, 2000, "Usage and serving suggestions");
        var storage = Text(req.StorageInstructions, 1000, "Storage instructions");
        var warnings = Text(req.Warnings, 1000, "Warnings");

        var allergens = (req.Allergens ?? [])
            .Select(a => a?.Trim() ?? "").Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (allergens.Count > 20 || allergens.Any(a => a.Length > 40))
            error ??= "List up to 20 allergens, each up to 40 characters.";

        var nutrition = (req.Nutrition ?? [])
            .Select(r => new NutritionRow(r.Nutrient?.Trim(), r.Per100?.Trim(), r.PerServing?.Trim()))
            .Where(r => !string.IsNullOrEmpty(r.Nutrient))
            .ToList();
        if (nutrition.Count > 30 || nutrition.Any(r => r.Nutrient!.Length > 60 || (r.Per100?.Length ?? 0) > 30 || (r.PerServing?.Length ?? 0) > 30))
            error ??= "Nutrition can have up to 30 rows. Keep each name under 60 characters and each value under 30.";
        if (nutrition.Any(r => string.IsNullOrEmpty(r.Per100) && string.IsNullOrEmpty(r.PerServing)))
            error ??= "Every nutrition row needs a value per 100 or per serving.";

        var specs = (req.Specifications ?? [])
            .Select(r => new SpecificationRow(r.Label?.Trim(), r.Value?.Trim()))
            .Where(r => !string.IsNullOrEmpty(r.Label) || !string.IsNullOrEmpty(r.Value))
            .ToList();
        if (specs.Any(r => string.IsNullOrEmpty(r.Label) || string.IsNullOrEmpty(r.Value)))
            error ??= "Every specification needs both a name and a value.";
        if (specs.Count > 30 || specs.Any(r => (r.Label?.Length ?? 0) > 60 || (r.Value?.Length ?? 0) > 200))
            error ??= "Specifications can have up to 30 rows. Keep names under 60 characters and values under 200.";

        var tips = (req.AuthenticityTips ?? [])
            .Select(t => t?.Trim() ?? "").Where(t => t.Length > 0).ToList();
        if (tips.Count > 12 || tips.Any(t => t.Length > 300))
            error ??= "Add up to 12 authenticity checks, each up to 300 characters.";

        if (error is not null) return error;

        product.Brand = brand;
        product.PackSize = packSize;
        product.CountryOfOrigin = origin;
        product.Manufacturer = manufacturer;
        product.Ingredients = ingredients;
        product.Allergens = JsonSerializer.Serialize(allergens);
        product.ServingSize = servingSize;
        product.ServingsPerPack = servings;
        product.NutritionInfo = JsonSerializer.Serialize(nutrition, Json);
        product.UsageInstructions = usage;
        product.StorageInstructions = storage;
        product.Warnings = warnings;
        product.Specifications = JsonSerializer.Serialize(specs, Json);
        product.AuthenticityTips = JsonSerializer.Serialize(tips);
        return null;
    }

    /// <summary>The information block returned to the supplier portal, shop app and customer app.</summary>
    public static object Project(Product p)
    {
        var allergens = Read<List<string>>(p.Allergens) ?? [];
        var nutrition = Read<List<NutritionRow>>(p.NutritionInfo) ?? [];
        var specs = Read<List<SpecificationRow>>(p.Specifications) ?? [];
        var tips = Read<List<string>>(p.AuthenticityTips) ?? [];
        return new
        {
            brand = p.Brand,
            packSize = p.PackSize,
            countryOfOrigin = p.CountryOfOrigin,
            manufacturer = p.Manufacturer,
            ingredients = p.Ingredients,
            allergens,
            servingSize = p.ServingSize,
            servingsPerPack = p.ServingsPerPack,
            nutrition = nutrition.Select(n => new { nutrient = n.Nutrient, per100 = n.Per100, perServing = n.PerServing }),
            usageInstructions = p.UsageInstructions,
            storageInstructions = p.StorageInstructions,
            warnings = p.Warnings,
            specifications = specs.Select(s => new { label = s.Label, value = s.Value }),
            authenticityTips = tips,
            isFoodItem = p.IsFoodItem,
        };
    }

    /// <summary>The supplier's own counterfeit checks, for the verification result.</summary>
    public static List<string> Tips(Product p) => Read<List<string>>(p.AuthenticityTips) ?? [];

    private static T? Read<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }
}
