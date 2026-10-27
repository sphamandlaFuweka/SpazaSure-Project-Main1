using System.Globalization;
using System.Text.RegularExpressions;

namespace SpazaSure.ProductService.Services;

/// <summary>One plain-language point about a product. Tone: warning, good or info.</summary>
public sealed record InsightPoint(string Tone, string Text);

public sealed record NutrientFact(string Key, string Label, double Value, string Unit, string? Level);

public sealed record AdditiveInfo(string Code, string? Name, string? Purpose);

public sealed record ProductInsightReport(
    string ProductType,
    string ProductTypeLabel,
    string Summary,
    List<InsightPoint> Highlights,
    string? NutritionBasis,
    List<NutrientFact> Nutrition,
    int? NovaGroup,
    string? NovaLabel,
    string? NutriScore,
    string? NutriScoreLabel,
    List<AdditiveInfo> Additives,
    List<string> Diet,
    List<string> Traces,
    string? DataSource);

/// <summary>
/// Explains a product found in an open database in everyday words, for products that
/// aren't registered with SpazaSure. Thresholds follow the UK FSA front-of-pack guidance
/// that the Open Food Facts nutrient levels also use.
/// </summary>
public static partial class ProductInsights
{
    private const double GramsPerTeaspoonOfSugar = 4.0;

    public static ProductInsightReport Build(OpenFoodFactsProductSnapshot p)
    {
        var type = p.ProductType is "beauty" or "product" or "petfood" ? p.ProductType : "food";
        var isFood = type == "food";
        var liquid = IsLiquid(p.Quantity);
        var basis = liquid ? "100 ml" : "100 g";
        var highlights = new List<InsightPoint>();

        // Nutrition (food and pet food only)
        var rows = new List<NutrientFact>();
        if (type is "food" or "petfood")
        {
            foreach (var (key, label, unit) in NutritionLabels)
            {
                if (!p.Nutrition.TryGetValue(key, out var value)) continue;
                var level = p.NutrientLevels.TryGetValue(key, out var l) ? l : LevelFor(key, value, liquid);
                rows.Add(new(key, label, Math.Round(value, 1), unit, level));
            }
        }

        if (isFood)
        {
            if (p.Nutrition.TryGetValue("sugars", out var sugar))
            {
                var spoons = sugar / GramsPerTeaspoonOfSugar;
                var level = Level(rows, "sugars");
                var spoonText = spoons >= 0.5 ? $" (about {FormatSpoons(spoons)} of sugar)" : "";
                if (level == "high")
                    highlights.Add(new("warning", $"High in sugar: {Num(sugar)} g per {basis}{spoonText}."));
                else if (level == "low")
                    highlights.Add(new("good", $"Low in sugar: {Num(sugar)} g per {basis}."));
                else if (spoons >= 0.5)
                    highlights.Add(new("info", $"{Num(sugar)} g sugar per {basis}{spoonText}."));
            }
            AddLevelPoint(highlights, rows, "salt", "salt", basis);
            AddLevelPoint(highlights, rows, "saturated-fat", "saturated fat", basis);
            AddLevelPoint(highlights, rows, "fat", "fat", basis);
            if (p.Nutrition.TryGetValue("fiber", out var fibre) && fibre >= 6)
                highlights.Add(new("good", $"High in fibre: {Num(fibre)} g per {basis}."));
            if (p.Nutrition.TryGetValue("proteins", out var protein) && !liquid && protein >= 12)
                highlights.Add(new("good", $"A good source of protein: {Num(protein)} g per {basis}."));
        }

        string? novaLabel = p.NovaGroup is { } nova ? NovaLabels.GetValueOrDefault(nova) : null;
        if (isFood && p.NovaGroup == 4)
            highlights.Add(new("warning", "Ultra-processed food (NOVA 4): made mostly from industrial ingredients and additives. Fine now and then, but not as an everyday staple."));
        else if (isFood && p.NovaGroup == 1)
            highlights.Add(new("good", "Unprocessed or minimally processed food (NOVA 1)."));

        var nutriScore = p.NutriScore?.Trim().ToLowerInvariant() is { Length: 1 } g && "abcde".Contains(g) ? g.ToUpperInvariant() : null;
        string? nutriLabel = nutriScore is null ? null : NutriScoreLabels[nutriScore];

        // Additives, with everyday names where known
        var additives = p.Additives
            .Select(code => Additive(code))
            .ToList();
        var colours = additives.Where(a => a.Purpose == "colour").ToList();
        if (additives.Count > 0)
            highlights.Add(new(additives.Count >= 5 ? "warning" : "info",
                additives.Count == 1 ? "Contains 1 additive." : $"Contains {additives.Count} additives."));
        if (additives.Any(a => a.Code == "E951"))
            highlights.Add(new("warning", "Contains aspartame, a source of phenylalanine. Not suitable for people with phenylketonuria (PKU)."));
        if (colours.Count > 0 && isFood)
            highlights.Add(new("info", $"Coloured with {string.Join(", ", colours.Select(c => c.Name ?? c.Code))}."));

        // Diet
        var diet = new List<string>();
        var tags = p.IngredientAnalysis.Select(t => t.ToLowerInvariant()).ToHashSet();
        if (tags.Contains("vegan")) diet.Add("Vegan");
        else if (tags.Contains("vegetarian")) diet.Add("Vegetarian");
        else if (tags.Contains("non-vegan") && tags.Contains("non-vegetarian")) diet.Add("Contains meat or fish");
        else if (tags.Contains("non-vegan")) diet.Add("Not vegan");
        if (tags.Contains("palm-oil")) diet.Add("Contains palm oil");
        else if (tags.Contains("palm-oil-free")) diet.Add("No palm oil");

        var traces = p.Traces.Select(Readable).ToList();
        if (traces.Count > 0)
            highlights.Add(new("info", $"May contain traces of {string.Join(", ", traces)}."));

        if (!isFood && !string.IsNullOrWhiteSpace(p.Ingredients))
            highlights.Add(new("info", "Check the ingredient list if you have sensitive skin or allergies."));

        return new ProductInsightReport(
            ProductType: type,
            ProductTypeLabel: TypeLabels[type],
            Summary: Summary(p, type),
            Highlights: highlights,
            NutritionBasis: rows.Count > 0 ? basis : null,
            Nutrition: rows,
            NovaGroup: isFood ? p.NovaGroup : null,
            NovaLabel: isFood ? novaLabel : null,
            NutriScore: isFood ? nutriScore : null,
            NutriScoreLabel: isFood ? nutriLabel : null,
            Additives: additives,
            Diet: diet,
            Traces: traces,
            DataSource: p.DataSource);
    }

    private static string Summary(OpenFoodFactsProductSnapshot p, string type)
    {
        var name = p.Name ?? p.GenericName ?? "This product";
        var brand = FirstOf(p.Brands);
        var sentence = name + (p.Quantity is null ? "" : $" ({p.Quantity})");
        if (brand is not null && !name.Contains(brand, StringComparison.OrdinalIgnoreCase))
            sentence += $" by {brand}";
        sentence += $". {TypeLabels[type]}";
        // Categories are plural ("Sodas"), so name them rather than build "a sodas"
        if (FirstCategory(p.Categories) is { } category)
            sentence += $", listed under {category.ToLowerInvariant()}";
        sentence += ".";
        if (FirstOf(p.Stores) is { } store)
            sentence += $" Sold at stores such as {store}.";
        return sentence;
    }

    private static void AddLevelPoint(List<InsightPoint> points, List<NutrientFact> rows, string key, string label, string basis)
    {
        var row = rows.FirstOrDefault(r => r.Key == key);
        if (row is null) return;
        if (row.Level == "high")
            points.Add(new("warning", $"High in {label}: {Num(row.Value)} g per {basis}."));
        else if (row.Level == "low" && key is "salt" or "saturated-fat")
            points.Add(new("good", $"Low in {label}."));
    }

    private static string? Level(List<NutrientFact> rows, string key) => rows.FirstOrDefault(r => r.Key == key)?.Level;

    /// <summary>FSA traffic-light thresholds per 100 g (food) or 100 ml (drinks).</summary>
    public static string? LevelFor(string key, double value, bool liquid)
    {
        (double low, double high)? limits = (key, liquid) switch
        {
            ("fat", false) => (3, 17.5),
            ("fat", true) => (1.5, 8.75),
            ("saturated-fat", false) => (1.5, 5),
            ("saturated-fat", true) => (0.75, 2.5),
            ("sugars", false) => (5, 22.5),
            ("sugars", true) => (2.5, 11.25),
            ("salt", false) => (0.3, 1.5),
            ("salt", true) => (0.3, 0.75),
            _ => null,
        };
        if (limits is not { } l) return null;
        return value <= l.low ? "low" : value > l.high ? "high" : "moderate";
    }

    public static bool IsLiquid(string? quantity) =>
        quantity is not null && LiquidQuantity().IsMatch(quantity);

    public static AdditiveInfo Additive(string tag)
    {
        var code = tag.Trim().ToLowerInvariant();
        if (!code.StartsWith('e')) code = "e" + code;
        // e322i, e450iii and the like share the base number's name
        var known = KnownAdditives.TryGetValue(code, out var info)
            || KnownAdditives.TryGetValue(RomanSuffix().Replace(code, ""), out info);
        return new(code.ToUpperInvariant(), known ? info.Name : null, known ? info.Purpose : null);
    }

    private static string FormatSpoons(double spoons)
    {
        var rounded = Math.Round(spoons * 2, MidpointRounding.AwayFromZero) / 2;
        var text = rounded % 1 == 0 ? $"{rounded:0}" : $"{Math.Floor(rounded):0}½";
        if (text.StartsWith("0½")) text = "½";
        return rounded == 1 ? "1 teaspoon" : $"{text} teaspoons";
    }

    private static string Num(double v) => v.ToString(v >= 10 ? "0.#" : "0.##", CultureInfo.InvariantCulture);

    private static string? FirstOf(string? list) =>
        list?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

    // Open Food Facts lists categories from broad to narrow; the last readable one is the most specific
    private static string? FirstCategory(string? categories) =>
        categories?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => !c.Contains(':') && !c.Contains("open-", StringComparison.OrdinalIgnoreCase)
                        && !c.Equals("non-food-products", StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

    private static string Readable(string tag) => tag.Replace('-', ' ');

    [GeneratedRegex(@"\b\d+([.,]\d+)?\s*(ml|cl|dl|l|litre|liter|litres|liters)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LiquidQuantity();

    [GeneratedRegex(@"[iv]+$")]
    private static partial Regex RomanSuffix();

    private static readonly (string Key, string Label, string Unit)[] NutritionLabels =
    [
        ("energy-kcal", "Energy", "kcal"),
        ("fat", "Fat", "g"),
        ("saturated-fat", "Saturated fat", "g"),
        ("sugars", "Sugars", "g"),
        ("salt", "Salt", "g"),
        ("proteins", "Protein", "g"),
        ("fiber", "Fibre", "g"),
    ];

    private static readonly Dictionary<string, string> TypeLabels = new()
    {
        ["food"] = "Food or drink",
        ["beauty"] = "Personal care or beauty product",
        ["product"] = "Household or other product",
        ["petfood"] = "Pet food",
    };

    private static readonly Dictionary<int, string> NovaLabels = new()
    {
        [1] = "Unprocessed or minimally processed",
        [2] = "Processed cooking ingredient",
        [3] = "Processed food",
        [4] = "Ultra-processed food",
    };

    private static readonly Dictionary<string, string> NutriScoreLabels = new()
    {
        ["A"] = "Best nutritional quality",
        ["B"] = "Good nutritional quality",
        ["C"] = "Average nutritional quality",
        ["D"] = "Poor nutritional quality",
        ["E"] = "Lowest nutritional quality",
    };

    private static readonly Dictionary<string, (string Name, string Purpose)> KnownAdditives = new()
    {
        ["e100"] = ("Curcumin (turmeric)", "colour"), ["e101"] = ("Riboflavin (vitamin B2)", "colour"),
        ["e102"] = ("Tartrazine", "colour"), ["e104"] = ("Quinoline yellow", "colour"),
        ["e110"] = ("Sunset yellow", "colour"), ["e120"] = ("Carmine (from insects)", "colour"),
        ["e122"] = ("Carmoisine", "colour"), ["e124"] = ("Ponceau 4R", "colour"),
        ["e129"] = ("Allura red", "colour"), ["e133"] = ("Brilliant blue", "colour"),
        ["e140"] = ("Chlorophyll", "colour"), ["e141"] = ("Copper chlorophyll", "colour"),
        ["e150a"] = ("Plain caramel", "colour"), ["e150c"] = ("Ammonia caramel", "colour"),
        ["e150d"] = ("Sulphite ammonia caramel", "colour"), ["e160a"] = ("Carotenes", "colour"),
        ["e160b"] = ("Annatto", "colour"), ["e160c"] = ("Paprika extract", "colour"),
        ["e162"] = ("Beetroot red", "colour"), ["e170"] = ("Calcium carbonate", "mineral"),
        ["e171"] = ("Titanium dioxide", "colour"),
        ["e200"] = ("Sorbic acid", "preservative"), ["e202"] = ("Potassium sorbate", "preservative"),
        ["e210"] = ("Benzoic acid", "preservative"), ["e211"] = ("Sodium benzoate", "preservative"),
        ["e220"] = ("Sulphur dioxide", "preservative"), ["e223"] = ("Sodium metabisulphite", "preservative"),
        ["e250"] = ("Sodium nitrite", "preservative"), ["e251"] = ("Sodium nitrate", "preservative"),
        ["e260"] = ("Acetic acid (vinegar)", "acid"), ["e270"] = ("Lactic acid", "acid"),
        ["e282"] = ("Calcium propionate", "preservative"), ["e290"] = ("Carbon dioxide", "gas"),
        ["e296"] = ("Malic acid", "acid"), ["e300"] = ("Ascorbic acid (vitamin C)", "antioxidant"),
        ["e301"] = ("Sodium ascorbate", "antioxidant"), ["e306"] = ("Tocopherols (vitamin E)", "antioxidant"),
        ["e319"] = ("TBHQ", "antioxidant"), ["e320"] = ("BHA", "antioxidant"), ["e321"] = ("BHT", "antioxidant"),
        ["e322"] = ("Lecithin", "emulsifier"), ["e330"] = ("Citric acid", "acid"),
        ["e331"] = ("Sodium citrates", "acidity regulator"), ["e332"] = ("Potassium citrates", "acidity regulator"),
        ["e338"] = ("Phosphoric acid", "acid"), ["e339"] = ("Sodium phosphates", "acidity regulator"),
        ["e341"] = ("Calcium phosphates", "acidity regulator"), ["e407"] = ("Carrageenan", "thickener"),
        ["e412"] = ("Guar gum", "thickener"), ["e414"] = ("Gum arabic", "thickener"),
        ["e415"] = ("Xanthan gum", "thickener"), ["e420"] = ("Sorbitol", "sweetener"),
        ["e422"] = ("Glycerol", "humectant"), ["e440"] = ("Pectin", "thickener"),
        ["e450"] = ("Diphosphates", "raising agent"), ["e451"] = ("Triphosphates", "stabiliser"),
        ["e452"] = ("Polyphosphates", "stabiliser"), ["e460"] = ("Cellulose", "thickener"),
        ["e466"] = ("Carboxymethyl cellulose", "thickener"),
        ["e471"] = ("Mono- and diglycerides of fatty acids", "emulsifier"),
        ["e472e"] = ("DATEM", "emulsifier"), ["e476"] = ("PGPR", "emulsifier"),
        ["e481"] = ("Sodium stearoyl lactylate", "emulsifier"),
        ["e500"] = ("Sodium carbonates (baking soda)", "raising agent"),
        ["e503"] = ("Ammonium carbonates", "raising agent"), ["e508"] = ("Potassium chloride", "salt substitute"),
        ["e621"] = ("Monosodium glutamate (MSG)", "flavour enhancer"),
        ["e627"] = ("Disodium guanylate", "flavour enhancer"), ["e631"] = ("Disodium inosinate", "flavour enhancer"),
        ["e635"] = ("Disodium ribonucleotides", "flavour enhancer"),
        ["e900"] = ("Dimethylpolysiloxane", "anti-foaming agent"),
        ["e950"] = ("Acesulfame K", "sweetener"), ["e951"] = ("Aspartame", "sweetener"),
        ["e952"] = ("Cyclamate", "sweetener"), ["e954"] = ("Saccharin", "sweetener"),
        ["e955"] = ("Sucralose", "sweetener"), ["e960"] = ("Steviol glycosides (stevia)", "sweetener"),
        ["e965"] = ("Maltitol", "sweetener"),
        ["e1422"] = ("Acetylated distarch adipate (modified starch)", "thickener"),
        ["e1442"] = ("Hydroxypropyl distarch phosphate (modified starch)", "thickener"),
    };
}
