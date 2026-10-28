using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.ProductService.Controllers;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Products;

/// <summary>Lists link to stored photos instead of sending them, so product pages load quickly.</summary>
public class ProductImagesTests
{
    private const string Api = "https://api.test";
    private static readonly string Photo = "data:image/jpeg;base64," + Convert.ToBase64String(new byte[20_000]);

    private static (ProductsController Supplier, SpazaSureDbContext Db) Create()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var user = new User { Email = "img@test.com", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "supplier").Id };
        var supplier = new Supplier { UserId = user.Id, CompanyName = "Photo Foods", ContactPerson = "Ann", Phone = "0823000001", Email = "img@test.com", Status = "verified" };
        db.AddRange(user, supplier);
        db.SaveChanges();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test")),
        };
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("api.test");
        return (new ProductsController(db, new EventPublisher("__nonexistent__", "x", "x")) { ControllerContext = new ControllerContext { HttpContext = http } }, db);
    }

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    private static ProductRequest Request(string images) =>
        new("Maize Meal 10kg", "", 120m, null, null, null, Images: images);

    [Fact]
    public async Task List_LinksToStoredPhotos_AndSavingTheLinksKeepsThePhotos()
    {
        var (supplier, db) = Create();
        var images = JsonSerializer.Serialize(new[] { Photo, "https://cdn.test/side.jpg" });
        (await supplier.Create(Request(images))).Should().BeOfType<OkObjectResult>();
        var product = db.Products.Single();

        var item = Data(await supplier.List()).GetProperty("items")[0];
        var links = JsonSerializer.Deserialize<string[]>(item.GetProperty("images").GetString()!)!;
        links.Should().Equal(
            ProductImages.Link(Api, product.Id, 0, images.Length),
            ProductImages.Link(Api, product.Id, 1, images.Length));

        // The edit form sends the links back, with a new photo added
        var edited = JsonSerializer.Serialize(links.Append("https://cdn.test/back.jpg"));
        (await supplier.Update(product.Id, Request(edited))).Should().BeOfType<OkObjectResult>();
        ProductImages.Parse(db.Products.Single().Images).Should().Equal(Photo, "https://cdn.test/side.jpg", "https://cdn.test/back.jpg");
    }

    [Fact]
    public async Task ShortImageLists_AreSentAsTheyAre()
    {
        var (supplier, _) = Create();
        await supplier.Create(Request("[\"https://cdn.test/a.jpg\"]"));
        Data(await supplier.List()).GetProperty("items")[0].GetProperty("images").GetString()
            .Should().Be("[\"https://cdn.test/a.jpg\"]");
    }

    [Fact]
    public async Task PhotoLink_ServesTheImage_AndCachesIt()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var product = new Product { Name = "Rice", Images = JsonSerializer.Serialize(new[] { Photo, "https://cdn.test/a.jpg" }) };
        db.Products.Add(product);
        db.SaveChanges();
        var images = new ProductImagesController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var file = (await images.Product(product.Id, 0)).Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("image/jpeg");
        file.FileContents.Should().HaveCount(20_000);
        images.Response.Headers.CacheControl.ToString().Should().Contain("immutable");

        (await images.Product(product.Id, 1)).Should().BeOfType<RedirectResult>().Which.Url.Should().Be("https://cdn.test/a.jpg");
        (await images.Product(product.Id, 2)).Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz4=")]
    [InlineData("data:image/png,notbase64")]
    public void OnlyPlainImagesAreServed(string entry) => ProductImages.Decode(entry).Should().BeNull();

    [Fact]
    public void Logo_LinksOnlyWhenLong()
    {
        var id = Guid.NewGuid();
        ProductImages.LogoForList(Api, id, null, 0).Should().BeNull();
        ProductImages.LogoForList(Api, id, "https://cdn.test/logo.png", 25).Should().Be("https://cdn.test/logo.png");
        ProductImages.LogoForList(Api, id, null, 90_000).Should().Be(ProductImages.LogoLink(Api, id, 90_000));
    }

    /// <summary>On PostgreSQL the photos must stay in the database: only lengths and short values come back.</summary>
    [Fact]
    public void ListQueries_DontSelectThePhotos()
    {
        using var db = new SpazaSureDbContext(new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseNpgsql("Host=localhost;Database=none").UseSnakeCaseNamingConvention().Options);
        var apiBase = Api;

        var products = db.Products.Select(p => new
        {
            p.Id,
            Images = ProductImages.ForList(apiBase, p.Id, p.Images.Length <= ProductImages.InlineLimit ? p.Images : null, p.Images.Length, p.Images.Length - p.Images.Replace("\"", "").Length),
            Logo = ProductImages.LogoForList(apiBase, p.SupplierId, p.Supplier.LogoUrl != null && p.Supplier.LogoUrl.Length <= ProductImages.InlineLimit ? p.Supplier.LogoUrl : null, p.Supplier.LogoUrl == null ? 0 : p.Supplier.LogoUrl.Length),
        }).ToQueryString();
        var orders = db.Orders.Select(o => new
        {
            o.Id,
            Items = o.Items.Select(i => new
            {
                ProductImage = ProductImages.ForList(apiBase, i.ProductId, i.Product.Images.Length <= ProductImages.InlineLimit ? i.Product.Images : null, i.Product.Images.Length, i.Product.Images.Length - i.Product.Images.Replace("\"", "").Length),
            }),
        }).ToQueryString();

        foreach (var sql in new[] { products, orders })
        {
            sql.Should().Contain("CASE");
            // A bare column in the select list would send every photo
            sql.Should().NotMatchRegex(@"(SELECT|,)\s*[a-z0-9]+\.(images|logo_url)\s*(,|\bFROM\b)");
        }
    }
}
