using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.OrderService.Services;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>Suppliers don't create group buys; they accept the shops' request and dispatch the orders.</summary>
public class SupplierGroupBuyTests
{
    private sealed record Fixture(
        SupplierGroupBuyController Ctrl, SpazaSureDbContext Db, Supplier Supplier, Product Product, GroupBuy GroupBuy, SpazaShop Shop);

    private static Fixture Create(int joinedQty = 6, int targetQty = 5, string groupStatus = "active")
    {
        var options = new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new SpazaSureDbContext(options);
        var supplierRole = new Role { Name = "supplier", IsActive = true };
        var shopRole = new Role { Name = "spaza_owner", IsActive = true };
        db.Roles.AddRange(supplierRole, shopRole);

        var supplierUser = new User { Email = "sup@test.com", PasswordHash = "h", RoleId = supplierRole.Id };
        var supplier = new Supplier
        {
            UserId = supplierUser.Id, CompanyName = "Bulk Co", ContactPerson = "Sipho", Phone = "0821000000", Email = "sup@test.com",
            Latitude = -26.2041, Longitude = 28.0473, CommissionRate = 5m
        };
        var shopUser = new User { Email = "shop@test.com", PasswordHash = "h", RoleId = shopRole.Id };
        var shop = new SpazaShop
        {
            UserId = shopUser.Id, ShopName = "Corner Spaza", OwnerName = "Nomsa", Phone = "0822000000",
            Address = "3 Test St, Soweto", Latitude = -26.2485, Longitude = 27.8540
        };
        var product = new Product
        {
            SupplierId = supplier.Id, Name = "Maize Meal 10kg", Sku = "MM10", Price = 100m,
            StockQty = 500, MinOrderQty = 1, IsApproved = true, IsAvailable = true
        };
        var groupBuy = new GroupBuy
        {
            Title = "Maize month", SupplierId = supplier.Id, ProductId = product.Id, Status = groupStatus,
            ExpiresAt = DateTime.UtcNow.AddDays(5), CreatedByRole = "admin",
            OriginalPrice = 100m, DiscountPct = 10, DiscountPrice = 90m, TargetQty = targetQty, CurrentQty = joinedQty
        };
        var gbProduct = new GroupBuyProduct
        {
            GroupBuyId = groupBuy.Id, ProductId = product.Id, OriginalPrice = 100m, DiscountPct = 10, DiscountPrice = 90m,
            TargetQty = targetQty, CurrentQty = joinedQty, Status = joinedQty >= targetQty ? "qualified" : "active"
        };
        var participant = new GroupBuyParticipant { GroupBuyId = groupBuy.Id, ShopId = shop.Id, Quantity = joinedQty, Status = "joined" };
        var item = new GroupBuyParticipantItem { ParticipantId = participant.Id, GroupBuyProductId = gbProduct.Id, Quantity = joinedQty, Status = "joined" };
        db.AddRange(supplierUser, supplier, shopUser, shop, product, groupBuy, gbProduct, participant, item);
        db.SaveChanges();

        var ctrl = new SupplierGroupBuyController(
            db,
            new DeliveryPricingService(Options.Create(new DeliveryPricingOptions())),
            new EventPublisher("__nonexistent__", "x", "x"))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, supplierUser.Id.ToString()), new Claim(ClaimTypes.Role, "supplier")], "Test"))
                }
            }
        };
        return new Fixture(ctrl, db, supplier, product, groupBuy, shop);
    }

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    [Fact]
    public async Task Accept_ChargesTheAdvertisedGroupPrice_AndCreatesShopOrder()
    {
        var f = Create();

        var result = await f.Ctrl.Accept(f.GroupBuy.Id);

        result.Should().BeOfType<OkObjectResult>();
        var order = f.Db.Orders.Include(o => o.Items).Single();
        order.ShopId.Should().Be(f.Shop.Id);
        order.Status.Should().Be("pending");
        order.Items.Single().UnitPrice.Should().Be(90m);
        order.Items.Single().DiscountPct.Should().Be(10);
        f.Db.Products.Single().StockQty.Should().Be(494);
        f.Db.GroupBuys.Single().Status.Should().Be("completed");
    }

    [Fact]
    public async Task Accept_BeforeAnyTargetIsReached_IsRefused()
    {
        var f = Create(joinedQty: 3, targetQty: 5);

        var result = await f.Ctrl.Accept(f.GroupBuy.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        f.Db.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispatch_MovesAcceptedOrdersOnTheirWay()
    {
        var f = Create();
        await f.Ctrl.Accept(f.GroupBuy.Id);

        var data = Data(await f.Ctrl.Dispatch(f.GroupBuy.Id));

        data.GetProperty("dispatchedCount").GetInt32().Should().Be(1);
        f.Db.Orders.Single().Status.Should().Be("dispatched");
    }

    [Fact]
    public async Task Dispatch_WithNothingAccepted_IsRefused()
    {
        var f = Create();

        (await f.Ctrl.Dispatch(f.GroupBuy.Id)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task List_Requests_ShowsCampaignsWaitingForTheSupplier_WithTheirOrders()
    {
        var f = Create();

        var requests = Data(await f.Ctrl.List("requests"));
        requests.GetArrayLength().Should().Be(1);

        await f.Ctrl.Accept(f.GroupBuy.Id);
        var toDispatch = Data(await f.Ctrl.List("to_dispatch"));
        toDispatch.GetArrayLength().Should().Be(1);
        toDispatch[0].GetProperty("orders").GetArrayLength().Should().Be(1);
        toDispatch[0].GetProperty("orders")[0].GetProperty("shopName").GetString().Should().Be("Corner Spaza");
    }

    [Fact]
    public async Task List_HidesProposalsThatNeverWentLive()
    {
        var f = Create(groupStatus: "pending_approval");

        Data(await f.Ctrl.List("all")).GetArrayLength().Should().Be(0);
    }
}
