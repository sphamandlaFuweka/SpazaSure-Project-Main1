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

/// <summary>Shops ask for a group buy; admins answer. A request never creates a deal by itself.</summary>
public class GroupBuyRequestTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");

    private static ControllerContext As(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")),
        },
    };

    private static (SpazaSureDbContext Db, SpazaShop Shop, ShopGroupBuyRequestsController Ctrl) Create()
    {
        var db = new SpazaSureDbContext(new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var shop = new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Corner Shop", OwnerName = "Sipho", Phone = "0821000000", Address = "1 Main Rd", City = "Soweto" };
        db.SpazaShops.Add(shop);
        db.SaveChanges();
        return (db, shop, new ShopGroupBuyRequestsController(db) { ControllerContext = As(shop.UserId) });
    }

    private static AdminGroupBuyRequestsController Admin(SpazaSureDbContext db) =>
        new(db, Events) { ControllerContext = As(Guid.NewGuid()) };

    [Fact]
    public async Task Shop_SendsARequest_WithoutCreatingADeal()
    {
        var (db, shop, ctrl) = Create();

        var result = await ctrl.Create(new CreateGroupBuyRequest(" Maize meal 10kg ", 40, " Any brand "));

        result.Should().BeOfType<OkObjectResult>();
        var request = await db.GroupBuyRequests.SingleAsync();
        request.ShopId.Should().Be(shop.Id);
        request.ProductName.Should().Be("Maize meal 10kg");
        request.Quantity.Should().Be(40);
        request.Note.Should().Be("Any brand");
        request.Status.Should().Be("new");
        (await db.GroupBuys.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("   ", 10)]
    [InlineData("Rice", 0)]
    [InlineData("Rice", 100_001)]
    public async Task Shop_MustNameAProduct_AndGiveASensibleQuantity(string product, int quantity)
    {
        var (db, _, ctrl) = Create();

        var result = await ctrl.Create(new CreateGroupBuyRequest(product, quantity, null));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.GroupBuyRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Shop_WithTenUnansweredRequests_MustWait_UntilOneIsAnswered()
    {
        var (db, _, ctrl) = Create();
        for (var i = 0; i < 10; i++) await ctrl.Create(new CreateGroupBuyRequest($"Product {i}", null, null));

        (await ctrl.Create(new CreateGroupBuyRequest("One more", null, null))).Should().BeOfType<BadRequestObjectResult>();

        var first = await db.GroupBuyRequests.FirstAsync();
        await Admin(db).UpdateStatus(first.Id, new UpdateGroupBuyRequestStatus("planned", null));
        (await ctrl.Create(new CreateGroupBuyRequest("One more", null, null))).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Shop_OnlySeesItsOwnRequests()
    {
        var (db, shop, ctrl) = Create();
        var other = new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Other", OwnerName = "Lindiwe", Phone = "0821000001", Address = "2 Main Rd" };
        db.SpazaShops.Add(other);
        db.GroupBuyRequests.AddRange(
            new GroupBuyRequest { ShopId = shop.Id, ProductName = "Mine" },
            new GroupBuyRequest { ShopId = other.Id, ProductName = "Theirs" });
        await db.SaveChangesAsync();

        var json = System.Text.Json.JsonSerializer.Serialize(((ObjectResult)await ctrl.List()).Value);

        json.Should().Contain("Mine").And.NotContain("Theirs");
    }

    [Fact]
    public async Task Admin_AnswersARequest()
    {
        var (db, shop, _) = Create();
        var request = new GroupBuyRequest { ShopId = shop.Id, ProductName = "Rice" };
        db.GroupBuyRequests.Add(request);
        await db.SaveChangesAsync();

        var result = await Admin(db).UpdateStatus(request.Id, new UpdateGroupBuyRequestStatus("declined", " No supplier yet "));

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.GroupBuyRequests.SingleAsync();
        saved.Status.Should().Be("declined");
        saved.AdminNote.Should().Be("No supplier yet");
    }

    [Fact]
    public async Task Admin_CannotSetAnUnknownStatus()
    {
        var (db, shop, _) = Create();
        var request = new GroupBuyRequest { ShopId = shop.Id, ProductName = "Rice" };
        db.GroupBuyRequests.Add(request);
        await db.SaveChangesAsync();

        (await Admin(db).UpdateStatus(request.Id, new UpdateGroupBuyRequestStatus("new", null)))
            .Should().BeOfType<BadRequestObjectResult>();
    }
}
