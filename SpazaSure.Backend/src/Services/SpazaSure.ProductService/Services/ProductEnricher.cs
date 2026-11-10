using System.Globalization;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.ProductService.Services;

/// <summary>What enriching one product found and did.</summary>
/// <param name="Status">filled, up_to_date, mismatch, not_found, no_barcode, lookup_failed or invalid.</param>
public sealed record EnrichmentOutcome(string Status, List<string> Filled, string? Note = null, string? Source = null);

/// <summary>
/// Fills a product's details &amp; specifications, ingredients &amp; nutrition and "spot a fake" checks
/// from its record in the open product databases. Only empty sections are filled: whatever the
/// supplier typed is never overwritten.
/// </summary>
public static class ProductEnricher
{
    /// <summary>The pack barcode to look up: the product's barcode, else a SKU that is a real barcode.</summary>
    public static string? LookupCode(Product product)
    {
        foreach (var raw in new[] { product.Barcode, product.Sku })
        {
            var digits = BarcodeService.Normalize(raw);
            if (digits is null || BarcodeService.Problem(digits) is not null) continue;
            // SpazaSure made this code itself, so no outside database knows it
            if (digits.StartsWith(BarcodeService.GeneratedPrefix) || BarcodeService.IsGenerated(product.Id, digits)) continue;
            return digits;
        }
        return null;
    }

    /// <summary>
    /// True when the database record plausibly is this product: the names share a real word.
    /// Guards against a mistyped barcode filling in another product's ingredients.
    /// </summary>
    public static bool SameProduct(Product product, OpenFoodFactsProductSnapshot record)
    {
        var ours = Words(product.Name + " " + product.Brand);
        var theirs = Words($"{record.Name} {record.GenericName} {record.Brands}");
        // A record without a name can't be checked; its barcode matched, so trust it
        return theirs.Count == 0 || ours.Overlaps(theirs);
    }

    /// <summary>
    /// The record's pack size when it clearly differs from every size in the product's name, e.g. a
    /// 330 ml can's barcode on a "12 x 1.5 L" listing. Null when the sizes agree or can't be compared.
    /// </summary>
    public static string? SizeConflict(Product product, OpenFoodFactsProductSnapshot record)
    {
        var ours = Sizes(product.Name + " " + product.PackSize);
        var theirs = Sizes(record.Quantity);
        if (ours.Count == 0 || theirs.Count == 0) return null;
        return theirs.Overlaps(ours) ? null : record.Quantity!.Trim();
    }

    private static readonly System.Text.RegularExpressions.Regex SizePattern = new(
        @"(\d+(?:[.,]\d+)?)\s*(kg|g|gm|gr|grams?|ml|l|lt|litres?|liters?)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Sizes in a text, in grams or millilitres.</summary>
    private static HashSet<double> Sizes(string? text)
    {
        var sizes = new HashSet<double>();
        if (string.IsNullOrWhiteSpace(text)) return sizes;
        foreach (System.Text.RegularExpressions.Match m in SizePattern.Matches(text))
        {
            if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) continue;
            var unit = m.Groups[2].Value.ToLowerInvariant();
            sizes.Add(Math.Round(unit is "kg" || unit.StartsWith('l') ? n * 1000 : n, 1));
        }
        return sizes;
    }

    /// <summary>
    /// The database's brand, only when the product's name starts with it ("Lucky Star Pilchards…").
    /// Records often hold a flavour or variant in the brand field ("mint", "Long grain", "XTRA STRONG").
    /// </summary>
    private static string? TrustedBrand(Product product, OpenFoodFactsProductSnapshot record)
    {
        var name = AllWords(product.Name);
        foreach (var brand in record.Brands?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
        {
            var words = AllWords(brand);
            if (words.Length > 0 && words.Length <= name.Length && words.SequenceEqual(name.Take(words.Length)))
                return brand.Any(char.IsUpper) ? brand : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(brand);
        }
        return null;
    }

    private static string[] AllWords(string? text) =>
        (text ?? "").ToLowerInvariant().Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Whether the record's details can be trusted for this listing: it names our brand, gives a pack
    /// size our listing also has, or names no brand at all. A record naming another brand with no
    /// matching size (e.g. a foreign "Twist" by Schweppes) is likely a different product.
    /// </summary>
    public static bool Corroborated(Product product, OpenFoodFactsProductSnapshot record) =>
        string.IsNullOrWhiteSpace(record.Brands) || TrustedBrand(product, record) is not null || SizeMatches(product, record);

    private static bool SizeMatches(Product product, OpenFoodFactsProductSnapshot record) =>
        Sizes(record.Quantity).Overlaps(Sizes(product.Name + " " + product.PackSize));

    /// <summary>English ingredients that read like an ingredients list, not a size, a stray note or a garbled scan.</summary>
    private static string? TrustedIngredients(OpenFoodFactsProductSnapshot record)
    {
        var text = record.IngredientsEnglish?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 12 || Sizes(text).Count > 0 && text.Length < 30) return null;
        if (text.Count(char.IsLetter) < 8) return null;
        // A list has separators; a garbled photo scan often has neither, or stray symbols such as "£"
        if (!text.Contains(',') && !text.Contains(';')) return null;
        return text.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || ",.;:()[]%-*/&'+!".Contains(c)) ? text : null;
    }

    /// <summary>The value when it reads as English: no accented letters and no "fr:"-style tags.</summary>
    private static string? English(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Any(c => char.IsLetter(c) && c > 127)) return null;
        return System.Text.RegularExpressions.Regex.IsMatch(value, @"(^|[\s,])[a-z]{2}:") ? null : value;
    }

    /// <summary>The record's pack size when it is a real size the listing agrees with, or the brand vouches for it.</summary>
    private static string? TrustedSize(Product product, OpenFoodFactsProductSnapshot record, string? brand) =>
        Sizes(record.Quantity).Count > 0 && (brand is not null || SizeMatches(product, record)) ? TidySize(record.Quantity) : null;

    /// <summary>"125.0g" → "125g".</summary>
    private static string? TidySize(string? size) =>
        size is null ? null : System.Text.RegularExpressions.Regex.Replace(size.Trim(), @"(\d+)\.0+(?!\d)", "$1");

    private static HashSet<string> Words(string text) =>
        text.ToLowerInvariant()
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !w.All(char.IsDigit) && !StopWords.Contains(w))
            .ToHashSet();

    private static readonly char[] Separators = [' ', ',', '.', '-', '/', '&', '(', ')', '\'', '"', '+', ':', ';'];
    private static readonly HashSet<string> StopWords =
        ["the", "and", "with", "for", "pack", "flavour", "flavoured", "original", "new"];

    /// <summary>
    /// Applies the record to the product's empty sections. Returns the sections filled (empty when
    /// nothing was new), or an error message when the result fails validation (nothing is changed then).
    /// </summary>
    public static (List<string> Filled, string? Error) Apply(Product product, OpenFoodFactsProductSnapshot record, string code)
    {
        var filled = new List<string>();
        string? Keep(string? current, string? candidate, int max, string section)
        {
            if (!string.IsNullOrWhiteSpace(current)) return current;
            var value = Clean(candidate, max);
            if (value is not null) filled.Add(section);
            return value;
        }

        var allergens = ProductInfo.Read<List<string>>(product.Allergens) ?? [];
        var nutrition = ProductInfo.Read<List<NutritionRow>>(product.NutritionInfo) ?? [];
        var specs = ProductInfo.Read<List<SpecificationRow>>(product.Specifications) ?? [];
        var tips = ProductInfo.Read<List<string>>(product.AuthenticityTips) ?? [];

        // A weak record only gives the barcode-based "spot a fake" checks
        var trusted = Corroborated(product, record);
        if (!trusted) record = new OpenFoodFactsProductSnapshot { Barcode = record.Barcode, Name = record.Name, DataSource = record.DataSource };

        var brand = TrustedBrand(product, record);
        var request = new ProductInfoRequest(
            Brand: Keep(product.Brand, brand, 120, "brand"),
            // Only a real size, and only one the listing agrees with unless the brand confirms the record
            PackSize: Keep(product.PackSize, TrustedSize(product, record, brand), 60, "pack size"),
            CountryOfOrigin: product.CountryOfOrigin,
            Manufacturer: product.Manufacturer,
            Ingredients: Keep(product.Ingredients, TrustedIngredients(record), 4000, "ingredients"),
            Allergens: allergens,
            ServingSize: Keep(product.ServingSize, TidySize(record.ServingSize), 60, "serving size"),
            ServingsPerPack: product.ServingsPerPack,
            Nutrition: nutrition,
            UsageInstructions: product.UsageInstructions,
            StorageInstructions: product.StorageInstructions,
            Warnings: product.Warnings,
            Specifications: specs,
            AuthenticityTips: tips);

        if (allergens.Count == 0 && record.Allergens.Count > 0)
        {
            request = request with { Allergens = record.Allergens.Take(20).Select(Label).Select(a => Truncate(a, 40)).ToList() };
            filled.Add("allergens");
        }
        if (nutrition.Count == 0 && NutritionRows(record) is { Count: > 0 } rows)
        {
            request = request with { Nutrition = rows };
            filled.Add("nutrition");
        }
        if (specs.Count == 0 && Specifications(record, code) is { Count: > 0 } newSpecs)
        {
            request = request with { Specifications = newSpecs };
            filled.Add("specifications");
        }
        if (tips.Count == 0 && AuthenticityTips(product, record, code) is { Count: > 0 } newTips)
        {
            request = request with { AuthenticityTips = newTips };
            filled.Add("spot a fake");
        }

        if (filled.Count == 0) return ([], null);

        if (ProductInfo.Apply(product, request) is { } error) return ([], error);
        return (filled, null);
    }

    // ── Nutrition ──

    private static readonly (string Key, string Label, string Unit)[] Nutrients =
    [
        ("fat", "Total fat", "g"),
        ("saturated-fat", "Saturated fat", "g"),
        ("carbohydrates", "Carbohydrates", "g"),
        ("sugars", "of which total sugars", "g"),
        ("proteins", "Protein", "g"),
        ("fiber", "Dietary fibre", "g"),
        ("sodium", "Sodium", "mg"),
        ("salt", "Salt", "g"),
    ];

    public static List<NutritionRow> NutritionRows(OpenFoodFactsProductSnapshot record)
    {
        var rows = new List<NutritionRow>();
        if (Energy(record.Nutrition) is var per100 && Energy(record.NutritionPerServing) is var perServing
            && (per100 is not null || perServing is not null))
            rows.Add(new NutritionRow("Energy", per100, perServing));

        foreach (var (key, label, unit) in Nutrients)
        {
            var a = Amount(record.Nutrition, key, unit);
            var b = Amount(record.NutritionPerServing, key, unit);
            if (a is not null || b is not null) rows.Add(new NutritionRow(label, a, b));
        }
        return rows;
    }

    private static string? Energy(Dictionary<string, double> values)
    {
        // Open Food Facts' plain "energy" is always kJ
        double? kj = values.TryGetValue("energy-kj", out var k) ? k : values.TryGetValue("energy", out var e) ? e : null;
        double? kcal = values.TryGetValue("energy-kcal", out var c) ? c : null;
        // 1 kcal = 4.184 kJ; when the two disagree one was mistyped (often kcal copied from kJ). Keep kJ.
        if (kj is { } x && kcal is { } y && (y <= 0 || x / y is < 3.9 or > 4.5)) kcal = null;
        return (kj, kcal) switch
        {
            ({ } j, { } cal) => $"{Number(j, 0)} kJ ({Number(cal, 0)} kcal)",
            ({ } j, null) => $"{Number(j, 0)} kJ",
            (null, { } cal) => $"{Number(cal, 0)} kcal",
            _ => null,
        };
    }

    private static string? Amount(Dictionary<string, double> values, string key, string unit)
    {
        if (!values.TryGetValue(key, out var grams)) return null;
        return unit == "mg" ? $"{Number(grams * 1000, 0)} mg" : $"{Number(grams, 1)} g";
    }

    private static string Number(double value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString(decimals == 0 ? "0" : "0.#", CultureInfo.InvariantCulture);

    // ── Details & specifications ──

    public static List<SpecificationRow> Specifications(OpenFoodFactsProductSnapshot record, string code)
    {
        var rows = new List<SpecificationRow> { new("Barcode", code) };
        void Add(string label, string? value)
        {
            if (Clean(value, 200) is { } v) rows.Add(new(label, v));
        }
        Add("Category", English(LastOf(record.Categories)));
        Add("Packaging", English(record.Packaging));
        Add("Labels and certifications", English(record.Labels));
        if (record.ProductType == "food")
        {
            if (record.NutriScore is { Length: 1 } grade && "abcde".Contains(grade, StringComparison.OrdinalIgnoreCase))
                Add("Nutri-Score", grade.ToUpperInvariant());
            if (record.NovaGroup is >= 1 and <= 4)
                Add("Processing (NOVA group)", record.NovaGroup switch
                {
                    1 => "1 – unprocessed or minimally processed",
                    2 => "2 – processed culinary ingredient",
                    3 => "3 – processed food",
                    _ => "4 – ultra-processed food",
                });
            var diet = record.IngredientAnalysis
                .Where(t => t is "vegan" or "vegetarian" or "palm-oil-free")
                .Select(Label).ToList();
            if (diet.Count > 0) Add("Suitable for", string.Join(", ", diet));
        }
        if (record.Additives.Count > 0)
            Add("Additives", string.Join(", ", record.Additives.Take(15).Select(a => a.ToUpperInvariant())));
        Add("Information source", $"{record.DataSource} (open product database)");
        // A barcode alone isn't worth a specifications table
        return rows.Count > 2 ? rows : [];
    }

    // ── Spot a fake ──

    public static List<string> AuthenticityTips(Product product, OpenFoodFactsProductSnapshot record, string code)
    {
        var tips = new List<string>
        {
            $"The barcode on a genuine pack reads {code}. Scan it with the SpazaSure app: it should show this product, not \"not found\" or a different item.",
        };
        if (code.Length == 13 && (code.StartsWith("600") || code.StartsWith("601")))
            tips.Add("The barcode starts with 600 or 601, so the brand registered it with GS1 South Africa. A pack of this product with a different barcode is a warning sign.");
        if (TrustedBrand(product, record) is { } brand)
            tips.Add($"The brand is printed as \"{Truncate(brand, 80)}\". Misspellings, odd fonts, blurry print or faded colours are common signs of a fake.");
        if (Clean(TrustedSize(product, record, TrustedBrand(product, record)), 60) is { } size)
            tips.Add($"Genuine packs state the size as {size}. A missing, different or hand-written size is suspicious.");
        if (Clean(English(record.Packaging), 150) is { } packaging)
            tips.Add($"Genuine packaging: {packaging}. Different materials or a flimsy, poorly sealed pack are warning signs.");
        if (TrustedIngredients(record) is not null)
            tips.Add("The ingredients list on the pack should match the one on this page. Missing, misspelt or different ingredients can mean a counterfeit.");
        if (record.Photos.Count > 0)
            tips.Add("Compare the pack with the reference photos of the genuine product shown when you scan it in the SpazaSure app.");
        tips.Add("Check that the best-before date and batch number are printed clearly, not stuck on or scratched, and that the seal hasn't been opened.");
        if (product.Price > 0)
            tips.Add("Be careful of stock offered far below the usual price: very cheap deals are a common way counterfeits are sold.");
        return tips.Select(t => Truncate(t, 300)).Take(12).ToList();
    }

    // ── Text helpers ──

    private static string? Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : Truncate(trimmed, max);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";

    private static string? FirstOf(string? list) =>
        list?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

    /// <summary>The most specific category, skipping ones tagged in another language (e.g. "pt:bebidas").</summary>
    private static string? LastOf(string? list) =>
        list?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(c => c.IndexOf(':') is not (> 0 and <= 3) || c.StartsWith("en:", StringComparison.OrdinalIgnoreCase)) is { } last
            ? Label(last) : null;

    /// <summary>"en:milk" or "palm-oil-free" → "Milk" / "Palm oil free".</summary>
    private static string Label(string tag)
    {
        var colon = tag.IndexOf(':');
        var text = (colon is > 0 and <= 3 ? tag[(colon + 1)..] : tag).Replace('-', ' ').Trim();
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
