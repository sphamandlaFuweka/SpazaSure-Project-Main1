using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>Shops ask for a group buy from existing products; admins answer. A request never creates a deal by itself.</summary>
public class GroupBuyRequestTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2));

    private sealed record Fixture(SpazaSureDbContext Db, SpazaShop Shop, Supplier Supplier, Product Maize, Product Rice, ShopGroupBuyRequestsController Ctrl);

    private static ControllerContext As(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")),
        },
    };

    private static Product Item(Supplier supplier, string name, decimal price) => new()
    {
        SupplierId = supplier.Id, Name = name, Sku = name, Price = price, StockQty = 500, MinOrderQty = 1, IsApproved = true, IsAvailable = true,
    };

    private static Fixture Create()
    {
        var db = new SpazaSureDbContext(new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var shop = new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Corner Shop", OwnerName = "Sipho", Phone = "0821000000", Address = "1 Main Rd", City = "Soweto" };
        var supplier = new Supplier { UserId = Guid.NewGuid(), CompanyName = "Bulk Co", ContactPerson = "Thabo", Phone = "0821000002", Email = "b@test.com" };
        var maize = Item(supplier, "Maize meal 10kg", 300m);
        var rice = Item(supplier, "Rice 10kg", 300m);
        db.AddRange(shop, supplier, maize, rice);
        db.SaveChanges();
        return new Fixture(db, shop, supplier, maize, rice, new ShopGroupBuyRequestsController(db) { ControllerContext = As(shop.UserId) });
    }

    private static CreateGroupBuyRequest Request(
        Fixture f, int quantity = 10, int? shops = 3, DateOnly? opens = null, DateOnly? closes = null, string? area = "Soweto", int? discount = 10) =>
        new([new GroupBuyRequestLine(f.Maize.Id, quantity)], shops, opens ?? Today.AddDays(2), closes ?? Today.AddDays(16), area, discount, " Any brand ");

    private static string Json(IActionResult result) =>
        System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value,
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static AdminGroupBuyRequestsController Admin(SpazaSureDbContext db) =>
        new(db, Events) { ControllerContext = As(Guid.NewGuid()) };

    [Fact]
    public async Task Shop_SendsARequest_WithoutCreatingADeal()
    {
        var f = Create();
        var req = Request(f) with { Items = [new(f.Maize.Id, 6), new(f.Rice.Id, 4)] };

        (await f.Ctrl.Create(req)).Should().BeOfType<OkObjectResult>();

        var request = await f.Db.GroupBuyRequests.Include(r => r.Items).SingleAsync();
        request.ShopId.Should().Be(f.Shop.Id);
        request.ProductName.Should().Be("Maize meal 10kg +1 more");
        request.Quantity.Should().Be(10);
        request.Items.Select(i => (i.ProductName, i.Quantity)).Should().BeEquivalentTo([("Maize meal 10kg", 6), ("Rice 10kg", 4)]);
        request.SupplierId.Should().Be(f.Supplier.Id);
        request.SupplierName.Should().Be("Bulk Co");
        request.ShopsWanted.Should().Be(3);
        request.OpensOn.Should().Be(Today.AddDays(2));
        request.ClosesOn.Should().Be(Today.AddDays(16));
        request.Area.Should().Be("Soweto");
        request.TargetDiscountPct.Should().Be(10);
        request.Note.Should().Be("Any brand");
        request.Status.Should().Be("new");
        (await f.Db.GroupBuys.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EachShop_MustBuyTenItems_AndSpendTheMinimum()
    {
        var f = Create();

        Json(await f.Ctrl.Create(Request(f, quantity: 9))).Should().Contain("not less than 10 items");

        f.Maize.Price = 100m;
        await f.Db.SaveChangesAsync();
        Json(await f.Ctrl.Create(Request(f, quantity: 10))).Should().Contain("must spend not less than");
        (await f.Db.GroupBuyRequests.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public async Task TheNumberOfShops_IsRequired_AndNotLessThanTheMinimum(int? shops)
    {
        var f = Create();

        Json(await f.Ctrl.Create(Request(f, shops: shops))).Should().Contain("not less than 3");
    }

    [Fact]
    public async Task TheDates_AreRequired_AndMustMakeSense()
    {
        var f = Create();

        Json(await f.Ctrl.Create(Request(f) with { OpensOn = null })).Should().Contain("opening and closing dates");
        Json(await f.Ctrl.Create(Request(f, opens: Today.AddDays(-1)))).Should().Contain("can't be in the past");
        Json(await f.Ctrl.Create(Request(f, opens: Today.AddDays(5), closes: Today.AddDays(5)))).Should().Contain("after the opening date");
        Json(await f.Ctrl.Create(Request(f, opens: Today.AddDays(1), closes: Today.AddDays(62)))).Should().Contain("60 days");
    }

    [Fact]
    public async Task TheProducts_MustExist_AndComeFromOneSupplier()
    {
        var f = Create();
        var other = new Supplier { UserId = Guid.NewGuid(), CompanyName = "Other Co", ContactPerson = "Lindiwe", Phone = "0821000003", Email = "o@test.com" };
        var sugar = Item(other, "Sugar 10kg", 300m);
        var hidden = Item(f.Supplier, "Hidden", 300m);
        hidden.IsApproved = false;
        f.Db.AddRange(other, sugar, hidden);
        await f.Db.SaveChangesAsync();

        Json(await f.Ctrl.Create(Request(f) with { Items = [new(f.Maize.Id, 10), new(sugar.Id, 10)] })).Should().Contain("same supplier");
        Json(await f.Ctrl.Create(Request(f) with { Items = [new(hidden.Id, 10)] })).Should().Contain("no longer available");
        Json(await f.Ctrl.Create(Request(f) with { Items = [new(f.Maize.Id, 5), new(f.Maize.Id, 5)] })).Should().Contain("only be added once");
        Json(await f.Ctrl.Create(Request(f) with { Items = [] })).Should().Contain("at least one product");
    }

    [Fact]
    public async Task OlderApps_AreAskedToUpdate()
    {
        var f = Create();

        Json(await f.Ctrl.Create(Request(f) with { Items = null })).Should().Contain("update the SpazaSure app");
    }

    [Fact]
    public async Task Shop_WithTenUnansweredRequests_MustWait_UntilOneIsAnswered()
    {
        var f = Create();
        for (var i = 0; i < 10; i++) await f.Ctrl.Create(Request(f));

        (await f.Ctrl.Create(Request(f))).Should().BeOfType<BadRequestObjectResult>();

        var first = await f.Db.GroupBuyRequests.FirstAsync();
        await Admin(f.Db).UpdateStatus(first.Id, new UpdateGroupBuyRequestStatus("planned", null));
        (await f.Ctrl.Create(Request(f))).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Shop_OnlySeesItsOwnRequests()
    {
        var f = Create();
        var other = new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Other", OwnerName = "Lindiwe", Phone = "0821000001", Address = "2 Main Rd" };
        f.Db.SpazaShops.Add(other);
        f.Db.GroupBuyRequests.AddRange(
            new GroupBuyRequest { ShopId = f.Shop.Id, ProductName = "Mine" },
            new GroupBuyRequest { ShopId = other.Id, ProductName = "Theirs" });
        await f.Db.SaveChangesAsync();

        Json(await f.Ctrl.List()).Should().Contain("Mine").And.NotContain("Theirs");
    }

    [Fact]
    public async Task Admin_AnswersARequest()
    {
        var f = Create();
        var request = new GroupBuyRequest { ShopId = f.Shop.Id, ProductName = "Rice" };
        f.Db.GroupBuyRequests.Add(request);
        await f.Db.SaveChangesAsync();

        var result = await Admin(f.Db).UpdateStatus(request.Id, new UpdateGroupBuyRequestStatus("declined", " No supplier yet "));

        result.Should().BeOfType<OkObjectResult>();
        var saved = await f.Db.GroupBuyRequests.SingleAsync();
        saved.Status.Should().Be("declined");
        saved.AdminNote.Should().Be("No supplier yet");
    }

    [Fact]
    public async Task Admin_CannotSetAnUnknownStatus()
    {
        var f = Create();
        var request = new GroupBuyRequest { ShopId = f.Shop.Id, ProductName = "Rice" };
        f.Db.GroupBuyRequests.Add(request);
        await f.Db.SaveChangesAsync();

        (await Admin(f.Db).UpdateStatus(request.Id, new UpdateGroupBuyRequestStatus("new", null)))
            .Should().BeOfType<BadRequestObjectResult>();
    }
}
