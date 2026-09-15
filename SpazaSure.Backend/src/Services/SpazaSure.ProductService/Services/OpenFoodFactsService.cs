using System.Text.Json;

namespace SpazaSure.ProductService.Services;

public sealed class OpenFoodFactsProductSnapshot
{
    public string? Barcode { get; set; }
    public string? Name { get; set; }
    public string? GenericName { get; set; }
    public string? Description { get; set; }
    public string? Ingredients { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Allergens { get; set; } = [];
}

public class OpenFoodFactsService(HttpClient httpClient, ILogger<OpenFoodFactsService> logger)
{
    private const string ApiBaseUrl = "https://world.openfoodfacts.org/api/v2";

    public async Task<OpenFoodFactsProductSnapshot?> LookupAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        try
        {
            using var response = await httpClient.GetAsync($"{ApiBaseUrl}/product/{Uri.EscapeDataString(code)}.json", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseProductResponse(json, code);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Open Food Facts lookup failed for code {Code}", code);
            return null;
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
            var description = GetString(productEl, "generic_name", "brands");
            var imageUrl = GetString(productEl, "image_url", "image_front_url", "image_front_small_url");
            var allergens = ExtractAllergens(productEl);

            return new OpenFoodFactsProductSnapshot
            {
                Barcode = fallbackBarcode ?? GetString(productEl, "code") ?? string.Empty,
                Name = productName,
                GenericName = GetString(productEl, "generic_name"),
                Description = description,
                Ingredients = ingredients,
                ImageUrl = imageUrl,
                Allergens = allergens,
            };
        }
        catch (JsonException)
        {
            return null;
        }
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

    private static List<string> ExtractAllergens(JsonElement product)
    {
        var allergens = new List<string>();
        if (!product.TryGetProperty("allergens_tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            return allergens;

        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
                continue;

            var value = tag.GetString();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var normalized = value.Trim();
            if (normalized.StartsWith("en:", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[3..];

            if (!string.IsNullOrWhiteSpace(normalized) && !allergens.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                allergens.Add(normalized);
        }

        return allergens;
    }
}
