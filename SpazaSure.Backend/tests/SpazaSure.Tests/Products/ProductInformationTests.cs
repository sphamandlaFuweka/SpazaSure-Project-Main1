using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.ProductService.Controllers;
using SpazaSure.ProductService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Products;

/// <summary>Specifications, nutrition, ingredients and counterfeit checks on a product.</summary>
public class ProductInformationTests
{
    private static ProductInfoRequest FullInfo() => new(
        Brand: "White Star",
        PackSize: "10 kg",
        CountryOfOrigin: "South Africa",
        Manufacturer: "Pioneer Foods, Paarl",
        Ingredients: "Maize, vitamin A, thiamine, riboflavin, niacin, iron, zinc, folic acid.",
        Allergens: ["Gluten/Wheat", " gluten/wheat ", ""],
        ServingSize: "100 g",
        ServingsPerPack: "100",
        Nutrition:
        [
            new NutritionRow("Energy", "1490 kJ", "1490 kJ"),
            new NutritionRow("Protein", "8.2 g", null),
            new NutritionRow("", "ignored", null),
        ],
        UsageInstructions: "Bring 750 ml water to the boil, add 250 g meal and stir.",
        StorageInstructions: "Store in a cool, dry place.",
        Warnings: null,
        Specifications: [new SpecificationRow("Shelf life", "12 months")],
        AuthenticityTips: ["Blue star logo is embossed, not flat print.", "Batch and expiry are ink-jet printed on the seam."]);

    [Fact]
    public void Apply_StoresCleanedInformation()
    {
        var product = new Product();

        ProductInfo.Apply(product, FullInfo()).Should().BeNull();

        product.Brand.Should().Be("White Star");
        // Duplicates and blanks are dropped; empty nutrition rows are ignored.
        JsonSerializer.Deserialize<List<string>>(product.Allergens).Should().Equal("Gluten/Wheat");
        JsonDocument.Parse(product.NutritionInfo).RootElement.GetArrayLength().Should().Be(2);
        JsonSerializer.Deserialize<List<string>>(product.AuthenticityTips).Should().HaveCount(2);
    }

    [Fact]
    public void Apply_RejectsHalfFilledSpecification()
    {
        var product = new Product { Brand = "Keep me" };

        var error = ProductInfo.Apply(product, new ProductInfoRequest(
            Brand: "Changed", Specifications: [new SpecificationRow("Weight", "")]));

        error.Should().Contain("specification");
        product.Brand.Should().Be("Keep me"); // nothing applied when invalid
    }

    [Fact]
    public void Apply_RejectsTooManyTips()
    {
        var tips = Enumerable.Range(1, 13).Select(i => $"Check {i}").ToList();
        ProductInfo.Apply(new Product(), new ProductInfoRequest(AuthenticityTips: tips)).Should().NotBeNull();
    }

    [Fact]
    public void Project_ReadsBackWhatWasStored()
    {
        var product = new Product { IsFoodItem = true };
        ProductInfo.Apply(product, FullInfo());

        var json = JsonSerializer.SerializeToElement(ProductInfo.Project(product));

        json.GetProperty("packSize").GetString().Should().Be("10 kg");
        json.GetProperty("nutrition")[0].GetProperty("per100").GetString().Should().Be("1490 kJ");
        json.GetProperty("specifications")[0].GetProperty("label").GetString().Should().Be("Shelf life");
        json.GetProperty("authenticityTips").GetArrayLength().Should().Be(2);
        json.GetProperty("isFoodItem").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Project_ToleratesBrokenStoredJson()
    {
        var json = JsonSerializer.SerializeToElement(ProductInfo.Project(new Product { NutritionInfo = "not json" }));
        json.GetProperty("nutrition").GetArrayLength().Should().Be(0);
    }

    // ── Through the supplier and shop endpoints ─────────────────────────────────

    private static (ProductsController Supplier, ShopMarketplaceController Shop, SpazaSureDbContext Db) Create()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "supplier");
        var user = new User { Email = "info@test.com", PasswordHash = "h", RoleId = role.Id };
        var supplier = new Supplier { UserId = user.Id, CompanyName = "Info Foods", ContactPerson = "Lindi", Phone = "0823000000", Email = "info@test.com", Status = "verified" };
        db.AddRange(user, supplier);
        db.SaveChanges();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test"))
        };
        return (
            new ProductsController(db, new EventPublisher("__nonexistent__", "x", "x")) { ControllerContext = new ControllerContext { HttpContext = http } },
            new ShopMarketplaceController(db),
            db);
    }

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    private static ProductRequest Request(ProductInfoRequest? info) =>
        new("Maize Meal 10kg", "MM-10", 120m, "Super maize meal", null, null, Images: "[\"https://x/y.jpg\"]", IsFoodItem: true, Info: info);

    [Fact]
    public async Task SupplierCreates_WithInformation_AndShopSeesIt()
    {
        var (supplier, shop, db) = Create();

        (await supplier.Create(Request(FullInfo()))).Should().BeOfType<OkObjectResult>();
        var product = db.Products.Single();
        product.IsApproved = true;
        db.SaveChanges();

        var detail = Data(await shop.ProductDetail(product.Id));
        detail.GetProperty("info").GetProperty("brand").GetString().Should().Be("White Star");
        detail.GetProperty("info").GetProperty("authenticityTips").GetArrayLength().Should().Be(2);
        detail.GetProperty("isFoodItem").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithoutInformation_KeepsWhatIsStored()
    {
        var (supplier, _, db) = Create();
        await supplier.Create(Request(FullInfo()));
        var id = db.Products.Single().Id;

        (await supplier.Update(id, Request(null) with { Price = 130m })).Should().BeOfType<OkObjectResult>();

        var info = Data(await supplier.GetInfo(id));
        info.GetProperty("ingredients").GetString().Should().StartWith("Maize");
        db.Products.Single().Price.Should().Be(130m);
    }

    [Fact]
    public async Task Create_WithInvalidInformation_IsRefused()
    {
        var (supplier, _, db) = Create();

        var result = await supplier.Create(Request(new ProductInfoRequest(Brand: new string('x', 121))));

        result.Should().BeOfType<BadRequestObjectResult>();
        db.Products.Should().BeEmpty();
    }
}
