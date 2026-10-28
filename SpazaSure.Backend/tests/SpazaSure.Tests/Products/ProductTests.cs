using FluentAssertions;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.ProductService.Services;
using Xunit;

namespace SpazaSure.Tests.Products;

public class BarcodeServiceTests
{
    [Fact]
    public void GenerateEan13_ReturnsThirteenDigitSpazaSureCode()
    {
        var barcode = BarcodeService.GenerateEan13(Guid.NewGuid());
        barcode.Should().MatchRegex("^290[0-9]{10}$");
    }

    [Fact]
    public void GenerateEan13_SameGuid_ReturnsSameBarcode()
    {
        var id     = Guid.NewGuid();
        var first  = BarcodeService.GenerateEan13(id);
        var second = BarcodeService.GenerateEan13(id);
        first.Should().Be(second);
    }

    [Fact]
    public void GenerateEan13_DifferentGuids_ReturnDifferentBarcodes()
    {
        var a = BarcodeService.GenerateEan13(Guid.NewGuid());
        var b = BarcodeService.GenerateEan13(Guid.NewGuid());
        a.Should().NotBe(b);
    }

    [Theory]
    [InlineData("6009523606526")]   // EAN-13 off a real pack
    [InlineData("96385074")]        // EAN-8
    [InlineData("036000291452")]    // UPC-A
    [InlineData("10012345678902")]  // GTIN-14
    public void Problem_AcceptsPrintedBarcodes(string barcode) =>
        BarcodeService.Problem(barcode).Should().BeNull();

    [Theory]
    [InlineData("6009523606527")]   // one digit wrong
    [InlineData("600952360652")]    // a digit missing
    [InlineData("60095236065AB")]
    public void Problem_RejectsMistypedBarcodes(string barcode) =>
        BarcodeService.Problem(barcode).Should().NotBeNull();

    [Fact]
    public void Normalize_DropsSpacesAndDashes() =>
        BarcodeService.Normalize(" 6 009523-606526 ").Should().Be("6009523606526");

    [Fact]
    public void Normalize_TreatsBlankAsNoBarcode() =>
        BarcodeService.Normalize("  ").Should().BeNull();

    [Fact]
    public void GenerateEan13_IsAValidCodeRecognisedAsSpazaSures()
    {
        // Scanners reject a wrong check digit, so every generated code must pass it
        for (var i = 0; i < 200; i++)
        {
            var id = Guid.NewGuid();
            var code = BarcodeService.GenerateEan13(id);
            BarcodeService.Problem(code).Should().BeNull();
            BarcodeService.IsGenerated(id, code).Should().BeTrue();
            BarcodeInsights.Analyze(code).Kind.Should().Be("spazasure");
        }
    }

    [Fact]
    public void IsGenerated_IsFalseForAPrintedBarcode() =>
        BarcodeService.IsGenerated(Guid.NewGuid(), "6009523606526").Should().BeFalse();

    [Fact]
    public void RenderSvg_ReturnsNonEmptySvgString()
    {
        // RenderSvg pads its input to 13 digits, so any barcode is accepted.
        var barcode = BarcodeService.GenerateEan13(Guid.NewGuid());
        var svg = BarcodeService.RenderSvg(barcode);
        svg.Should().NotBeNullOrEmpty();
        svg.Should().Contain("<svg");
    }
}

public class OpenFoodFactsServiceTests
{
    [Fact]
    public void ParseProductResponse_WithValidProduct_ReturnsSnapshot()
    {
        const string json = """
        {
          "product": {
            "product_name": "Coca Cola",
            "allergens_tags": ["en:milk", "en:soy"],
            "ingredients_text": "Water, sugar",
            "image_url": "https://example.com/product.jpg"
          }
        }
        """;

        var snapshot = OpenFoodFactsService.ParseProductResponse(json);

        snapshot.Should().NotBeNull();
        snapshot!.Name.Should().Be("Coca Cola");
        snapshot.Allergens.Should().Contain(new[] { "milk", "soy" });
        snapshot.Ingredients.Should().Be("Water, sugar");
        snapshot.ImageUrl.Should().Be("https://example.com/product.jpg");
    }

    [Fact]
    public void ParseProductResponse_WithMissingProduct_ReturnsNull()
    {
        const string json = "{\"status\":0}";

        var snapshot = OpenFoodFactsService.ParseProductResponse(json);

        snapshot.Should().BeNull();
    }
}

public class ProductEntityTests
{
    [Fact]
    public void Product_DefaultValues_AreCorrect()
    {
        var product = new Product
        {
            Name   = "Test Product",
            Sku    = "SKU-001",
            Price  = 9.99m,
            Unit   = "unit",
            Images = "[]"
        };

        product.IsAvailable.Should().BeTrue();
        product.IsApproved.Should().BeFalse();
        product.IsFoodItem.Should().BeFalse();
        product.MinOrderQty.Should().Be(1);
    }

    [Fact]
    public void Product_NewId_IsNotEmpty()
    {
        var product = new Product { Name = "P", Sku = "S", Price = 1m, Unit = "unit", Images = "[]" };
        product.Id.Should().NotBe(Guid.Empty);
    }
}
