using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;

namespace SpazaSure.ProductService.Services;

/// <summary>What Claude read off a photo of a product's packaging.</summary>
public sealed class PackagingAnalysis
{
    [JsonPropertyName("imageQuality")] public string ImageQuality { get; set; } = "good";
    [JsonPropertyName("productName")] public string? ProductName { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("variant")] public string? Variant { get; set; }
    [JsonPropertyName("packSize")] public string? PackSize { get; set; }
    [JsonPropertyName("productType")] public string ProductType { get; set; } = "other";
    [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
    [JsonPropertyName("manufacturerAddress")] public string? ManufacturerAddress { get; set; }
    [JsonPropertyName("contacts")] public List<string> Contacts { get; set; } = [];
    [JsonPropertyName("countryOfOrigin")] public string? CountryOfOrigin { get; set; }
    [JsonPropertyName("barcodeDigits")] public string? BarcodeDigits { get; set; }
    [JsonPropertyName("batchNumber")] public string? BatchNumber { get; set; }
    [JsonPropertyName("expiryDate")] public string? ExpiryDate { get; set; }
    [JsonPropertyName("manufactureDate")] public string? ManufactureDate { get; set; }
    [JsonPropertyName("ingredients")] public string? Ingredients { get; set; }
    [JsonPropertyName("allergens")] public List<string> Allergens { get; set; } = [];
    [JsonPropertyName("claims")] public List<string> Claims { get; set; } = [];
    [JsonPropertyName("certifications")] public List<string> Certifications { get; set; } = [];
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
    [JsonPropertyName("usage")] public string? Usage { get; set; }
    [JsonPropertyName("languages")] public List<string> Languages { get; set; } = [];
    [JsonPropertyName("verdict")] public string Verdict { get; set; } = "cannot_tell";
    [JsonPropertyName("redFlags")] public List<string> RedFlags { get; set; } = [];
    [JsonPropertyName("goodSigns")] public List<string> GoodSigns { get; set; } = [];
    [JsonPropertyName("summary")] public string Summary { get; set; } = "";
    [JsonPropertyName("tips")] public List<string> Tips { get; set; } = [];
}

/// <summary>Why an analysis could not be made, so the app can fall back to reading the text on the phone.</summary>
public sealed record PackagingVisionResult(PackagingAnalysis? Analysis, string? Unavailable);

/// <summary>
/// Reads a packaging photo with Claude's vision model: the product, who made it, what the
/// label says, and signs that the pack is fake. Off when no Anthropic API key is configured.
/// </summary>
public class PackagingVisionService(IConfiguration config, ILogger<PackagingVisionService> logger)
{
    private const string Model = "claude-opus-5-5";

    private const string Instructions = """
        You inspect photos of product packaging for SpazaSure, a South African app that helps
        spaza shops and their customers avoid counterfeit, expired and unsafe goods.

        Read everything visible on the pack and fill in the fields. Copy text as printed; use
        null or an empty list for anything you cannot see. Never guess a barcode, batch or date:
        only report digits you can actually read. Dates are YYYY-MM-DD; for a month-only date
        use the last day of that month.

        Then judge whether the pack looks genuine, as an experienced shop owner would:
        - spelling or grammar mistakes, wrong or blurry logos, odd fonts, poor print quality
        - missing manufacturer or importer name and address (required on SA retail goods)
        - missing or tampered batch / expiry marks, stickers covering dates
        - claims that don't fit the product, or health claims a food or cosmetic may not make
        - certification marks (SABS, NRCS, Halaal, Kosher, SAHPRA registration numbers) that
          look wrong or are missing where they are normally expected
        - a barcode whose digits look printed separately from the pack, or a damaged seal
        Only flag what you can actually see. A single photo shows one side of the pack, so
        missing details may be on another side; say so in the tips rather than calling it fake.

        verdict: looks_genuine, some_concerns, likely_counterfeit, or cannot_tell (for blurry,
        partial or non-product photos). imageQuality: good, blurry, partial or not_a_product.
        summary: two or three plain sentences a shop owner can act on.
        tips: short, practical next checks (e.g. "Check the expiry date on the bottom").
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private string? ApiKey =>
        config["Anthropic:ApiKey"] is { Length: > 0 } key ? key : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);

    public async Task<PackagingVisionResult> AnalyzeAsync(
        byte[] image, string mediaType, string? deviceText, CancellationToken cancellationToken = default)
    {
        if (!Enabled) return new(null, "not_configured");

        var client = new AnthropicClient { ApiKey = ApiKey, Timeout = TimeSpan.FromSeconds(90) };
        var prompt = string.IsNullOrWhiteSpace(deviceText)
            ? "Analyse this product packaging."
            : $"Analyse this product packaging. The phone's text recognition read the following, which may contain mistakes:\n\n{deviceText}";

        try
        {
            var response = await client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 16000,
                Betas = ["server-side-fallback-2026-07-01"],
                // A declined request is re-served by a fallback model chosen by refusal category
                Fallbacks = new Default(),
                OutputConfig = new BetaOutputConfig
                {
                    Effort = Effort.Medium,
                    Format = new BetaJsonOutputFormat { Schema = Schema },
                },
                System = Instructions,
                Messages =
                [
                    new BetaUserMessageParam
                    {
                        Content = new List<BetaContentBlockParam>
                        {
                            new BetaImageBlockParam
                            {
                                Source = new BetaBase64ImageSource
                                {
                                    MediaType = mediaType,
                                    Data = Convert.ToBase64String(image),
                                },
                            },
                            new BetaTextBlockParam { Text = prompt },
                        },
                    },
                ],
            }, cancellationToken);

            if (response.StopReason == "refusal") return new(null, "declined");

            var json = string.Concat(response.Content
                .Select(b => b.TryPickText(out var t) ? t.Text : null)
                .Where(t => t is not null));
            var analysis = JsonSerializer.Deserialize<PackagingAnalysis>(json, JsonOptions);
            return analysis is null ? new(null, "unreadable") : new(analysis, null);
        }
        catch (AnthropicRateLimitException ex)
        {
            logger.LogWarning(ex, "Packaging analysis rate limited");
            return new(null, "busy");
        }
        catch (AnthropicApiException ex)
        {
            logger.LogError(ex, "Packaging analysis failed");
            return new(null, "error");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Packaging analysis could not be completed");
            return new(null, "error");
        }
    }

    private static JsonElement Text(string description) =>
        JsonSerializer.SerializeToElement(new { type = new[] { "string", "null" }, description });

    private static JsonElement List(string description) =>
        JsonSerializer.SerializeToElement(new { type = "array", items = new { type = "string" }, description });

    private static JsonElement Choice(params string[] values) =>
        JsonSerializer.SerializeToElement(new { type = "string", @enum = values });

    private static readonly Dictionary<string, JsonElement> Properties = new()
    {
        ["imageQuality"] = Choice("good", "blurry", "partial", "not_a_product"),
        ["productName"] = Text("Product name as printed"),
        ["brand"] = Text("Brand name"),
        ["variant"] = Text("Flavour, scent or variant"),
        ["packSize"] = Text("Net contents, e.g. 500 ml or 6 x 410 g"),
        ["productType"] = Choice("food", "beverage", "cosmetic", "medicine", "household", "baby", "pet", "other"),
        ["manufacturer"] = Text("Manufacturer, importer or distributor named on the pack"),
        ["manufacturerAddress"] = Text("Their address as printed"),
        ["contacts"] = List("Phone numbers, careline, email and website printed on the pack"),
        ["countryOfOrigin"] = Text("Country the pack says it was made or packed in"),
        ["barcodeDigits"] = Text("Digits printed under the barcode, no spaces"),
        ["batchNumber"] = Text("Batch or lot code"),
        ["expiryDate"] = Text("Expiry or best-before date, YYYY-MM-DD"),
        ["manufactureDate"] = Text("Manufacture date, YYYY-MM-DD"),
        ["ingredients"] = Text("Ingredients list as printed"),
        ["allergens"] = List("Allergens named on the pack"),
        ["claims"] = List("Marketing and health claims, e.g. 'Sugar free', 'Kills 99.9% of germs'"),
        ["certifications"] = List("Marks and registration numbers, e.g. SABS, Halaal, SAHPRA Reg. No."),
        ["warnings"] = List("Safety warnings"),
        ["usage"] = Text("Directions for use"),
        ["languages"] = List("Languages the label is printed in"),
        ["verdict"] = Choice("looks_genuine", "some_concerns", "likely_counterfeit", "cannot_tell"),
        ["redFlags"] = List("Specific things on the pack that suggest it is fake, tampered or unsafe"),
        ["goodSigns"] = List("Specific things on the pack that suggest it is genuine"),
        ["summary"] = JsonSerializer.SerializeToElement(new { type = "string" }),
        ["tips"] = List("Practical next checks for the shop owner or customer"),
    };

    private static readonly Dictionary<string, JsonElement> Schema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(Properties),
        ["required"] = JsonSerializer.SerializeToElement(Properties.Keys),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };
}
