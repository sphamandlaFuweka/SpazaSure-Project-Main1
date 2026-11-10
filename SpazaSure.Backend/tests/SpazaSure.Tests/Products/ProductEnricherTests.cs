using FluentAssertions;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.ProductService.Services;
using Xunit;

namespace SpazaSure.Tests.Products;

public class ProductEnricherTests
{
    // A real South African EAN-13 (valid check digit)
    private const string Code = "6001087340021";

    private static Product NewProduct(string name = "Simba Chips Salt & Vinegar 120g", string? barcode = Code) => new()
    {
        Id = Guid.NewGuid(), Name = name, Sku = "SKU-1", Barcode = barcode, Price = 20m,
    };

    private static OpenFoodFactsProductSnapshot Record() => new()
    {
        Barcode = Code,
        Name = "Simba Salt & Vinegar Chips",
        Brands = "Simba, PepsiCo",
        Quantity = "120 g",
        Ingredients = "Potatoes, vegetable oil, salt, vinegar powder",
        IngredientsEnglish = "Potatoes, vegetable oil, salt, vinegar powder",
        Allergens = ["milk"],
        Categories = "Snacks, Salty snacks, Crisps",
        Packaging = "Plastic bag",
        NutriScore = "d",
        NovaGroup = 4,
        ServingSize = "30 g",
        Nutrition = new() { ["energy-kj"] = 2200, ["energy-kcal"] = 526, ["fat"] = 32.45, ["salt"] = 1.6, ["sodium"] = 0.64 },
        NutritionPerServing = new() { ["energy-kj"] = 660 },
        Photos = [new("https://images.example/front.jpg", "Front of pack")],
    };

    [Fact]
    public void Apply_FillsEveryEmptySection()
    {
        var product = NewProduct();

        var (filled, error) = ProductEnricher.Apply(product, Record(), Code);

        error.Should().BeNull();
        filled.Should().Contain(["brand", "pack size", "ingredients", "allergens", "serving size", "nutrition", "specifications", "spot a fake"]);
        product.Brand.Should().Be("Simba");
        product.PackSize.Should().Be("120 g");
        product.NutritionInfo.Should().Contain("2200 kJ (526 kcal)").And.Contain("32.5 g").And.Contain("640 mg");
        product.Specifications.Should().Contain("Crisps").And.Contain("Nutri-Score");
        product.AuthenticityTips.Should().Contain(Code).And.Contain("GS1 South Africa");
        product.Allergens.Should().Contain("Milk");
    }

    [Fact]
    public void Apply_NeverOverwritesWhatTheSupplierEntered()
    {
        var product = NewProduct();
        product.Ingredients = "Supplier's own list";
        product.AuthenticityTips = "[\"Supplier tip\"]";

        var (filled, _) = ProductEnricher.Apply(product, Record(), Code);

        product.Ingredients.Should().Be("Supplier's own list");
        product.AuthenticityTips.Should().Be("[\"Supplier tip\"]");
        filled.Should().NotContain(["ingredients", "spot a fake"]);
    }

    [Fact]
    public void Apply_SecondRun_HasNothingNew()
    {
        var product = NewProduct();
        ProductEnricher.Apply(product, Record(), Code);

        var (filled, error) = ProductEnricher.Apply(product, Record(), Code);

        error.Should().BeNull();
        filled.Should().BeEmpty();
    }

    [Fact]
    public void SameProduct_RejectsABarcodeBelongingToSomethingElse()
    {
        ProductEnricher.SameProduct(NewProduct("Sunlight Dishwashing Liquid 750ml"), Record()).Should().BeFalse();
        ProductEnricher.SameProduct(NewProduct(), Record()).Should().BeTrue();
    }

    [Fact]
    public void LookupCode_SkipsMissingInvalidAndSpazaSureMadeCodes()
    {
        ProductEnricher.LookupCode(NewProduct(barcode: null)).Should().BeNull();
        ProductEnricher.LookupCode(NewProduct(barcode: "6001087340022")).Should().BeNull(); // bad check digit
        var own = NewProduct();
        own.Barcode = BarcodeService.GenerateEan13(own.Id);
        ProductEnricher.LookupCode(own).Should().BeNull();
        ProductEnricher.LookupCode(NewProduct()).Should().Be(Code);
    }

    [Fact]
    public void LookupCode_UsesTheSkuWhenItIsARealBarcode()
    {
        var product = NewProduct(barcode: null);
        product.Sku = Code;
        ProductEnricher.LookupCode(product).Should().Be(Code);
    }

    // ── Cases found in the production catalogue ──

    [Fact]
    public void SizeConflict_FlagsACanBarcodeOnABottleListing()
    {
        var listing = NewProduct("Coca-Cola 12x1.5L", "5449000000996");
        var can = new OpenFoodFactsProductSnapshot { Name = "Coca-Cola Original", Quantity = "330 ml" };
        ProductEnricher.SizeConflict(listing, can).Should().Be("330 ml");

        var sugar = NewProduct("HULETTS WHITE SUGAR 5X1KG", "6001042000403");
        ProductEnricher.SizeConflict(sugar, new() { Name = "White Sugar", Quantity = "1kg" }).Should().BeNull();
    }

    [Fact]
    public void Apply_IgnoresABrandTheProductNameDoesNotCarry()
    {
        var rice = NewProduct("Tastic Rice 5kg", "6001231012798");
        var record = new OpenFoodFactsProductSnapshot { Name = "TASTIC RICE", Brands = "Long grain", Quantity = "5 kg" };

        ProductEnricher.Apply(rice, record, "6001231012798");

        rice.Brand.Should().BeNull();
        rice.PackSize.Should().Be("5 kg");
        rice.AuthenticityTips.Should().NotContain("Long grain");
    }

    [Fact]
    public void Apply_RejectsForeignOrNonsenseIngredientsAndSizes()
    {
        var twist = NewProduct("LEMON TWIST 12X1.5 L", "5449000060075");
        var record = new OpenFoodFactsProductSnapshot
        {
            Name = "Twist", Brands = "Citron, Schweppes",
            Quantity = "Speaking low kilojoule lemon flavoured drink",
            Ingredients = "1.5 litre", IngredientsEnglish = "1.5 litre",
            Categories = "Beverages, pt:bebidas",
        };

        ProductEnricher.Apply(twist, record, "5449000060075");

        twist.Ingredients.Should().BeNull();
        twist.PackSize.Should().BeNull();
        twist.Specifications.Should().NotContain("bebidas");

        var switchDrink = NewProduct("SWITCH 24X500ML VIT C", "6009907414730");
        ProductEnricher.Apply(switchDrink, new() { Name = "Switch", Brands = "Switch", IngredientsEnglish = "s 23 11 13 a gzi switch immune boosta vitamin c zinc contains 3% juice" }, "6009907414730");
        switchDrink.Ingredients.Should().BeNull();
        switchDrink.Brand.Should().Be("Switch");

        var naks = NewProduct("BIGGA AMAPELEPELE SNACKS 40 X 30G", "6009801460192");
        ProductEnricher.Apply(naks, new() { Name = "Bigga Naks", IngredientsEnglish = "corn, salt, glutamate (flavour enhanocr £621), godd stufe" }, "6009801460192");
        naks.Ingredients.Should().BeNull();

        var coke = NewProduct("Coca-Cola Original 330ml", "5449000000996");
        ProductEnricher.Apply(coke, new() { Name = "Coca-Cola", Ingredients = "Agua carbonatada, azucar, colorante" }, "5449000000996");
        coke.Ingredients.Should().BeNull();
    }

    [Fact]
    public void Apply_BrandMustLeadTheProductName()
    {
        var mints = NewProduct("Beacon 50's Mint Smoothies", "6001120625139");
        ProductEnricher.Apply(mints, new() { Name = "SMOOTHIES SUPA", Brands = "mint", Quantity = "250g" }, "6001120625139");
        mints.Brand.Should().BeNull();
        mints.PackSize.Should().BeNull(); // the record names another brand and the listing has no size to confirm it

        var stock = NewProduct("Knorrox Chilli Beef Stock Cubes Jar", "6001087358422");
        ProductEnricher.Apply(stock, new() { Name = "Chilli beef", Brands = "knorrox" }, "6001087358422");
        stock.Brand.Should().Be("Knorrox");
    }

    [Fact]
    public void Apply_WeakRecordGivesOnlyBarcodeChecks()
    {
        var twist = NewProduct("LEMON TWIST 12X1.5 L", "5449000060075");
        var record = new OpenFoodFactsProductSnapshot
        {
            Name = "Twist", Brands = "Citron, Schweppes", Nutrition = new() { ["energy-kj"] = 207, ["sugars"] = 12 },
        };

        var (filled, _) = ProductEnricher.Apply(twist, record, "5449000060075");

        filled.Should().Equal("spot a fake");
        twist.NutritionInfo.Should().Be("[]");
    }

    [Fact]
    public void Apply_SkipsForeignLanguageDetailsAndBadEnergy()
    {
        var sprite = NewProduct("Sprite 6x2L", "5449000004864");
        var record = new OpenFoodFactsProductSnapshot
        {
            Name = "Sprite", Brands = "Coca-Cola Company", Quantity = "2 l", NovaGroup = 4,
            Categories = "Beverages, Boissons avec sucre ajouté", Packaging = "Plastique, Bouteille, en:pet-bottle",
            ServingSize = "250.0ml", Nutrition = new() { ["energy-kj"] = 2105, ["energy-kcal"] = 2105 },
        };

        ProductEnricher.Apply(sprite, record, "5449000004864");

        sprite.Specifications.Should().NotContain("Boissons").And.NotContain("Plastique");
        sprite.AuthenticityTips.Should().NotContain("Plastique");
        sprite.NutritionInfo.Should().Contain("2105 kJ").And.NotContain("kcal");
        sprite.ServingSize.Should().Be("250ml");
        sprite.PackSize.Should().Be("2 l");
    }
}
