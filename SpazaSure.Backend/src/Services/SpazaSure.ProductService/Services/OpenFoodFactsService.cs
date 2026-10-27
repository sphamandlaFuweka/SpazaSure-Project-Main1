using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace SpazaSure.ProductService.Services;

/// <summary>A reference photo of the product, e.g. the front of the pack or its nutrition label.</summary>
public sealed record ProductPhoto(string Url, string Label);

public sealed class OpenFoodFactsProductSnapshot
{
    public string? Barcode { get; set; }
    public string? Name { get; set; }
    public string? GenericName { get; set; }
    public string? Description { get; set; }
    public string? Ingredients { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Allergens { get; set; } = [];
    public string? Brands { get; set; }
    public string? Quantity { get; set; }
    public string? Categories { get; set; }
    public string? Origins { get; set; }
    public string? Countries { get; set; }
    public string? Labels { get; set; }
    public string? NutriScore { get; set; }

    /// <summary>food, beauty, product (household and other) or petfood.</summary>
    public string ProductType { get; set; } = "food";
    /// <summary>Which open database the record came from, e.g. "Open Food Facts".</summary>
    public string DataSource { get; set; } = "Open Food Facts";
    public int? NovaGroup { get; set; }
    /// <summary>Per 100 g (or 100 ml): energy-kcal, fat, saturated-fat, sugars, salt, proteins, fiber.</summary>
    public Dictionary<string, double> Nutrition { get; set; } = [];
    /// <summary>fat, saturated-fat, sugars, salt → low, moderate or high.</summary>
    public Dictionary<string, string> NutrientLevels { get; set; } = [];
    /// <summary>E-numbers in lower case, e.g. "e330".</summary>
    public List<string> Additives { get; set; } = [];
    /// <summary>Ingredient analysis tags without the language prefix, e.g. "vegan", "palm-oil-free".</summary>
    public List<string> IngredientAnalysis { get; set; } = [];
    public List<string> Traces { get; set; } = [];
    public string? ServingSize { get; set; }
    public string? Packaging { get; set; }
    public string? Stores { get; set; }
    /// <summary>Front first, then ingredients, nutrition label and packaging, where the database has them.</summary>
    public List<ProductPhoto> Photos { get; set; } = [];
}

/// <summary>Result of a lookup across the open product databases.</summary>
/// <param name="Product">The product, or null when no database has it.</param>
/// <param name="Unavailable">True when nothing was found and a database could not be reached,
/// so "not found" can't be trusted.</param>
public sealed record OpenProductLookup(OpenFoodFactsProductSnapshot? Product, bool Unavailable);

/// <summary>
/// Looks a barcode up in the Open Food Facts family of free databases: food, beauty
/// (toiletries), other products (household) and pet food. They share one API format.
/// </summary>
public class OpenFoodFactsService(HttpClient httpClient, IMemoryCache cache, ILogger<OpenFoodFactsService> logger)
{
    private static readonly (string Host, string Name)[] Databases =
    [
        ("https://world.openfoodfacts.org", "Open Food Facts"),
        ("https://world.openbeautyfacts.org", "Open Beauty Facts"),
        ("https://world.openproductsfacts.org", "Open Products Facts"),
        ("https://world.openpetfoodfacts.org", "Open Pet Food Facts"),
    ];

    private const string Fields =
        "code,product_name,product_name_en,generic_name,generic_name_en,brands,quantity,product_type," +
        "categories,manufacturing_places,origins,countries,labels,nutriscore_grade,nutrition_grades," +
        "ingredients_text,ingredients_text_en,allergens_tags,traces_tags,additives_tags," +
        "ingredients_analysis_tags,nova_group,nutriments,nutrient_levels,serving_size,packaging,stores," +
        "image_url,image_front_url,image_front_small_url,image_ingredients_url,image_nutrition_url,image_packaging_url";

    private static readonly TimeSpan FoundFor = TimeSpan.FromHours(12);
    private static readonly TimeSpan MissingFor = TimeSpan.FromHours(2);
    private static readonly TimeSpan PerDatabaseTimeout = TimeSpan.FromSeconds(8);

    public async Task<OpenProductLookup> LookupAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new(null, false);
        code = code.Trim();

        var cacheKey = "open-product:" + code;
        if (cache.TryGetValue(cacheKey, out OpenProductLookup? cached) && cached is not null)
            return cached;

        // All four at once, so a miss costs one round trip instead of four
        var lookups = Databases
            .Select(db => FetchAsync(db.Host, db.Name, code, cancellationToken))
            .ToArray();
        var results = await Task.WhenAll(lookups);

        var found = results.FirstOrDefault(r => r.Product is not null).Product;
        var result = new OpenProductLookup(found, found is null && results.Any(r => r.Failed));

        // Never cache an outage, or the product stays "unknown" after the service is back
        if (!result.Unavailable)
            cache.Set(cacheKey, result, found is null ? MissingFor : FoundFor);
        return result;
    }

    private async Task<(OpenFoodFactsProductSnapshot? Product, bool Failed)> FetchAsync(
        string host, string name, string code, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PerDatabaseTimeout);
        try
        {
            using var response = await httpClient.GetAsync(
                $"{host}/api/v2/product/{Uri.EscapeDataString(code)}.json?fields={Fields}", timeout.Token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (null, false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("{Database} returned {Status} for {Code}", name, (int)response.StatusCode, code);
                return (null, true);
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token);
            var product = ParseProductResponse(json, code);
            if (product is not null) product.DataSource = name;
            return (product, false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Database} lookup failed for {Code}", name, code);
            return (null, true);
        }
    }

    public static OpenFoodFactsProductSnapshot? ParseProductResponse(string json, string? fallbackBarcode = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("product", out var productEl) || productEl.ValueKind != JsonValueKind.Object)
                return null;

            var productName = GetString(productEl, "product_name", "product_name_en", "generic_name");
            var ingredients = GetString(productEl, "ingredients_text", "ingredients_text_en");
            var description = GetString(productEl, "generic_name", "generic_name_en", "brands");
            var imageUrl = GetString(productEl, "image_url", "image_front_url", "image_front_small_url");

            return new OpenFoodFactsProductSnapshot
            {
                Barcode = fallbackBarcode ?? GetString(productEl, "code") ?? string.Empty,
                Name = productName,
                GenericName = GetString(productEl, "generic_name", "generic_name_en"),
                Description = description,
                Ingredients = ingredients,
                ImageUrl = imageUrl,
                Allergens = GetTags(productEl, "allergens_tags"),
                Brands = GetString(productEl, "brands"),
                Quantity = GetString(productEl, "quantity"),
                Categories = GetString(productEl, "categories"),
                Origins = GetString(productEl, "manufacturing_places", "origins"),
                Countries = GetString(productEl, "countries"),
                Labels = GetString(productEl, "labels"),
                NutriScore = GetString(productEl, "nutriscore_grade", "nutrition_grades"),
                ProductType = GetString(productEl, "product_type") ?? "food",
                NovaGroup = GetNumber(productEl, "nova_group") is { } nova ? (int)nova : null,
                Nutrition = GetNutrition(productEl),
                NutrientLevels = GetStringMap(productEl, "nutrient_levels"),
                Additives = GetTags(productEl, "additives_tags").Select(a => a.ToLowerInvariant()).ToList(),
                IngredientAnalysis = GetTags(productEl, "ingredients_analysis_tags"),
                Traces = GetTags(productEl, "traces_tags"),
                ServingSize = GetString(productEl, "serving_size"),
                Packaging = GetString(productEl, "packaging"),
                Stores = GetString(productEl, "stores"),
                Photos = GetPhotos(productEl, imageUrl),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<ProductPhoto> GetPhotos(JsonElement product, string? front)
    {
        var photos = new List<ProductPhoto>();
        void Add(string? url, string label)
        {
            if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && photos.All(p => p.Url != url))
                photos.Add(new(url, label));
        }
        Add(front, "Front of pack");
        Add(GetString(product, "image_ingredients_url"), "Ingredients");
        Add(GetString(product, "image_nutrition_url"), "Nutrition label");
        Add(GetString(product, "image_packaging_url"), "Packaging and recycling");
        return photos;
    }

    private static readonly string[] NutritionKeys =
        ["energy-kcal", "fat", "saturated-fat", "sugars", "salt", "proteins", "fiber"];

    private static Dictionary<string, double> GetNutrition(JsonElement product)
    {
        var result = new Dictionary<string, double>();
        if (!product.TryGetProperty("nutriments", out var n) || n.ValueKind != JsonValueKind.Object)
            return result;
        foreach (var key in NutritionKeys)
            if (GetNumber(n, key + "_100g") is { } value && value >= 0)
                result[key] = value;
        return result;
    }

    private static Dictionary<string, string> GetStringMap(JsonElement element, string propertyName)
    {
        var result = new Dictionary<string, string>();
        if (!element.TryGetProperty(propertyName, out var map) || map.ValueKind != JsonValueKind.Object)
            return result;
        foreach (var p in map.EnumerateObject())
            if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                result[p.Name] = p.Value.GetString()!;
        return result;
    }

    // The databases store numbers sometimes as JSON numbers and sometimes as strings
    private static double? GetNumber(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private static string? GetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return null;
    }

    /// <summary>Tag list with the "en:" style language prefix removed and duplicates dropped.</summary>
    private static List<string> GetTags(JsonElement product, string propertyName)
    {
        var tags = new List<string>();
        if (!product.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return tags;

        foreach (var tag in array.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
                continue;

            var value = tag.GetString();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var normalized = value.Trim();
            var colon = normalized.IndexOf(':');
            if (colon is > 0 and <= 3)
                normalized = normalized[(colon + 1)..];

            if (!string.IsNullOrWhiteSpace(normalized) && !tags.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                tags.Add(normalized);
        }

        return tags;
    }
}
