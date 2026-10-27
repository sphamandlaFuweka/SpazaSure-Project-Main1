using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.ProductService.Services;
using Xunit;

namespace SpazaSure.Tests.Products;

public class BarcodeInsightsTests
{
    [Theory]
    [InlineData("6001067089827", "EAN-13", "South Africa", "600")]
    [InlineData("6011234567898", "EAN-13", "South Africa", "601")]
    [InlineData("5449000000996", "EAN-13", "Belgium and Luxembourg", "544")]
    [InlineData("6161234567890", "EAN-13", "Kenya", "616")]
    [InlineData("036000291452", "UPC-A", "USA and Canada", "003")]
    public void Analyze_ReadsFormatAndRegistrationCountry(string code, string format, string country, string prefix)
    {
        var info = BarcodeInsights.Analyze(code);

        info.Format.Should().Be(format);
        info.CheckDigitValid.Should().BeTrue();
        info.RegisteredIn.Should().Be(country);
        info.Prefix.Should().Be(prefix);
        info.Kind.Should().Be("product");
        info.Explanation.Should().Contain("not necessarily where it was made");
    }

    [Fact]
    public void Analyze_FlagsAWrongCheckDigit()
    {
        var info = BarcodeInsights.Analyze("6001067089820");

        info.CheckDigitValid.Should().BeFalse();
        info.Explanation.Should().Contain("check digit is wrong");
    }

    [Fact]
    public void Analyze_RecognisesShopOwnCodesBooksAndOuterCases()
    {
        BarcodeInsights.Analyze("2001234567893").Kind.Should().Be("in_store");
        BarcodeInsights.Analyze("9780306406157").Kind.Should().Be("book");

        var outerCase = BarcodeInsights.Analyze("16001067089824");
        outerCase.Format.Should().Be("GTIN-14 (outer case)");
        outerCase.RegisteredIn.Should().Be("South Africa");
    }

    [Theory]
    [InlineData("SKU-001")]
    [InlineData("SPZ-QR-ABC123")]
    [InlineData("123456789")]
    public void Analyze_TreatsNonRetailCodesAsOther(string code)
    {
        var info = BarcodeInsights.Analyze(code);

        info.IsRetailBarcode.Should().BeFalse();
        info.RegisteredIn.Should().BeNull();
    }
}

public class ProductInsightsTests
{
    private static OpenFoodFactsProductSnapshot Cola() => new()
    {
        Name = "Coca-Cola Original",
        Brands = "Coca-Cola",
        Quantity = "2 l",
        Categories = "Beverages, Carbonated drinks, Sodas",
        ProductType = "food",
        NovaGroup = 4,
        NutriScore = "e",
        Nutrition = new() { ["energy-kcal"] = 42, ["sugars"] = 10.6, ["salt"] = 0, ["fat"] = 0 },
        NutrientLevels = new() { ["sugars"] = "high", ["salt"] = "low", ["fat"] = "low" },
        Additives = ["e150d", "e338"],
        IngredientAnalysis = ["vegan", "palm-oil-free"],
    };

    [Fact]
    public void Build_ExplainsSugarInTeaspoons_PerHundredMlForDrinks()
    {
        var report = ProductInsights.Build(Cola());

        report.NutritionBasis.Should().Be("100 ml");
        report.Highlights.Should().Contain(h => h.Tone == "warning"
            && h.Text == "High in sugar: 10.6 g per 100 ml (about 2½ teaspoons of sugar).");
    }

    [Fact]
    public void Build_DescribesProcessingScoreAndAdditives()
    {
        var report = ProductInsights.Build(Cola());

        report.NovaLabel.Should().Be("Ultra-processed food");
        report.NutriScore.Should().Be("E");
        report.NutriScoreLabel.Should().Be("Lowest nutritional quality");
        report.Additives.Select(a => (a.Code, a.Name)).Should().Equal(
            ("E150D", "Sulphite ammonia caramel"), ("E338", "Phosphoric acid"));
        report.Diet.Should().Equal("Vegan", "No palm oil");
        report.Summary.Should().Be("Coca-Cola Original (2 l). Food or drink, listed under sodas.");
    }

    [Fact]
    public void Build_WarnsAboutAspartame()
    {
        var p = Cola();
        p.Additives = ["e951"];

        ProductInsights.Build(p).Highlights.Should().Contain(h => h.Text.Contains("phenylketonuria"));
    }

    [Fact]
    public void Build_WorksOutLevelsWhenTheDatabaseHasNone()
    {
        var p = new OpenFoodFactsProductSnapshot
        {
            Name = "Salted chips", Quantity = "125 g", ProductType = "food",
            Nutrition = new() { ["salt"] = 1.8, ["saturated-fat"] = 1.0 },
        };

        var report = ProductInsights.Build(p);

        report.Nutrition.Single(r => r.Key == "salt").Level.Should().Be("high");
        report.Highlights.Should().Contain(h => h.Text == "High in salt: 1.8 g per 100 g.");
        report.Highlights.Should().Contain(h => h.Text == "Low in saturated fat.");
    }

    [Fact]
    public void Build_SkipsNutritionForToiletries()
    {
        var shampoo = new OpenFoodFactsProductSnapshot
        {
            Name = "Palmolive apple shampoo", Brands = "Palmolive", Quantity = "350 ml",
            ProductType = "beauty", Ingredients = "Aqua, Sodium Laureth Sulfate",
            Categories = "non-food-products, open-beauty-facts", DataSource = "Open Beauty Facts",
        };

        var report = ProductInsights.Build(shampoo);

        report.ProductTypeLabel.Should().Be("Personal care or beauty product");
        report.Nutrition.Should().BeEmpty();
        report.NovaLabel.Should().BeNull();
        report.Summary.Should().Be("Palmolive apple shampoo (350 ml). Personal care or beauty product.");
        report.DataSource.Should().Be("Open Beauty Facts");
    }

    [Theory]
    [InlineData("e322i", "E322I", "Lecithin")]
    [InlineData("e450iii", "E450III", "Diphosphates")]
    [InlineData("e999", "E999", null)]
    public void Additive_NamesVariantsByTheirBaseNumber(string tag, string code, string? name)
    {
        var a = ProductInsights.Additive(tag);

        a.Code.Should().Be(code);
        a.Name.Should().Be(name);
    }
}

public class OpenProductLookupTests
{
    private const string Shampoo = """
        {"status":1,"product":{"code":"6001067089827","product_name":"Palmolive apple shampoo",
         "brands":"Palmolive","quantity":"350 ml","product_type":"beauty",
         "ingredients_analysis_tags":["en:palm-oil-free","en:vegan-status-unknown"]}}
        """;

    private const string Cola = """
        {"status":1,"product":{"product_name":"Coca-Cola","product_type":"food","nova_group":"4",
         "nutriments":{"sugars_100g":10.6,"salt_100g":"0.01","energy-kcal_100g":42},
         "nutrient_levels":{"sugars":"high","salt":"low"},
         "additives_tags":["en:e150d","en:e338"],"traces_tags":["en:milk"],
         "allergens_tags":["fr:gluten"]}}
        """;

    private sealed class FakeDatabases(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(respond(request.RequestUri!));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    // What the databases really send for an unknown barcode
    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound)
    {
        Content = new StringContent("{\"code\":\"x\",\"status\":0,\"status_verbose\":\"product not found\"}"),
    };

    private static (OpenFoodFactsService Service, FakeDatabases Handler) Create(Func<Uri, HttpResponseMessage> respond)
    {
        var handler = new FakeDatabases(respond);
        var service = new OpenFoodFactsService(new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()),
            NullLogger<OpenFoodFactsService>.Instance);
        return (service, handler);
    }

    [Fact]
    public void Parse_ReadsNutritionProcessingAdditivesAndTags()
    {
        var p = OpenFoodFactsService.ParseProductResponse(Cola)!;

        p.NovaGroup.Should().Be(4);
        p.Nutrition.Should().Contain(new KeyValuePair<string, double>("sugars", 10.6));
        p.Nutrition.Should().Contain(new KeyValuePair<string, double>("salt", 0.01));
        p.NutrientLevels["sugars"].Should().Be("high");
        p.Additives.Should().Equal("e150d", "e338");
        p.Traces.Should().Equal("milk");
        p.Allergens.Should().Equal("gluten");
    }

    [Fact]
    public void Parse_CollectsReferencePhotos_FrontFirst_SkippingDuplicatesAndInsecureLinks()
    {
        const string json = """
            {"product":{"product_name":"Coca-Cola",
             "image_url":"https://images.openfoodfacts.org/front.jpg",
             "image_front_url":"https://images.openfoodfacts.org/front.jpg",
             "image_ingredients_url":"https://images.openfoodfacts.org/ingredients.jpg",
             "image_nutrition_url":"http://images.openfoodfacts.org/nutrition.jpg",
             "image_packaging_url":"https://images.openfoodfacts.org/packaging.jpg"}}
            """;

        var photos = OpenFoodFactsService.ParseProductResponse(json)!.Photos;

        photos.Select(p => p.Label).Should().Equal("Front of pack", "Ingredients", "Packaging and recycling");
        photos[0].Url.Should().Be("https://images.openfoodfacts.org/front.jpg");
    }

    [Fact]
    public async Task Lookup_FindsToiletriesInTheBeautyDatabase()
    {
        var (service, _) = Create(uri => uri.Host.Contains("openbeautyfacts")
            ? Json(Shampoo)
            : NotFound());

        var result = await service.LookupAsync("6001067089827");

        result.Unavailable.Should().BeFalse();
        result.Product!.Name.Should().Be("Palmolive apple shampoo");
        result.Product.ProductType.Should().Be("beauty");
        result.Product.DataSource.Should().Be("Open Beauty Facts");
    }

    [Fact]
    public async Task Lookup_PrefersTheFoodDatabaseWhenSeveralHaveTheCode()
    {
        var (service, _) = Create(uri => uri.Host.Contains("openfoodfacts") || uri.Host.Contains("openbeautyfacts")
            ? Json(uri.Host.Contains("openfoodfacts") ? Cola : Shampoo)
            : NotFound());

        (await service.LookupAsync("5449000000996")).Product!.Name.Should().Be("Coca-Cola");
    }

    [Fact]
    public async Task Lookup_ReportsAnOutage_InsteadOfNotFound_AndDoesNotCacheIt()
    {
        var down = true;
        var (service, handler) = Create(uri => uri.Host.Contains("openfoodfacts")
            ? (down ? new HttpResponseMessage(HttpStatusCode.BadGateway) : Json(Cola))
            : NotFound());

        var during = await service.LookupAsync("5449000000996");
        down = false;
        var after = await service.LookupAsync("5449000000996");

        during.Product.Should().BeNull();
        during.Unavailable.Should().BeTrue();
        after.Product!.Name.Should().Be("Coca-Cola");
        handler.Calls.Should().Be(8);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "<html><title>Unscheduled downtime</title></html>")]
    [InlineData(HttpStatusCode.OK, "")]
    [InlineData(HttpStatusCode.NotFound, "<html>Not Found</html>")]
    [InlineData(HttpStatusCode.Forbidden, "Access denied")]
    public async Task Lookup_TreatsAPageThatIsNotTheApi_AsCouldNotCheck_AndRetriesNextTime(HttpStatusCode status, string body)
    {
        var (service, handler) = Create(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

        var first = await service.LookupAsync("5449000000996");
        await service.LookupAsync("5449000000996");

        first.Product.Should().BeNull();
        first.Unavailable.Should().BeTrue();
        handler.Calls.Should().Be(8); // not cached
    }

    [Fact]
    public async Task Lookup_CachesResults_SoRepeatScansDontHitTheDatabases()
    {
        var (service, handler) = Create(_ => NotFound());

        await service.LookupAsync("6009999999999");
        var again = await service.LookupAsync("6009999999999");

        again.Product.Should().BeNull();
        again.Unavailable.Should().BeFalse();
        handler.Calls.Should().Be(4);
    }
}

public class VerificationRiskOutageTests
{
    [Fact]
    public void AnUnreachableDatabase_IsNotTreatedAsAMissingProduct()
    {
        var today = new DateOnly(2026, 10, 27);
        var missing = VerificationRiskEngine.Evaluate(new VerificationInput { Code = "6001067089827" }, today);
        var outage = VerificationRiskEngine.Evaluate(
            new VerificationInput { Code = "6001067089827", GlobalDatabaseUnavailable = true }, today);

        missing.Indicators.Should().Contain(i => i.Contains("not found"));
        outage.Indicators.Should().BeEmpty();
        outage.Score.Should().BeLessThan(missing.Score);
        outage.Checks.Should().Contain(c => c.Label == "Product databases" && c.Status == "unknown");
    }
}
