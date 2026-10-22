using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.OrderService.Services;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>Orders ship from the nearest branch that has the stock, and pay for the distance.</summary>
public class BranchCheckoutTests
{
    private sealed record Fixture(
        SpazaSureDbContext Db, SpazaShop Shop, Supplier Supplier, SupplierBranch Jhb, SupplierBranch Durban,
        Product Tracked, Product Shared, ShopOrdersController Checkout);

    // A Cape Town supplier with warehouses in Johannesburg and Durban; the shop is in Soweto
    private static Fixture Create()
    {
        var options = new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new SpazaSureDbContext(options);
        var ownerRole = new Role { Name = "spaza_owner", IsActive = true };
        var supplierRole = new Role { Name = "supplier", IsActive = true };
        db.Roles.AddRange(ownerRole, supplierRole);

        var shopUser = new User { Email = "branch@test.com", Phone = "0820001100", PasswordHash = "h", RoleId = ownerRole.Id };
        var shop = new SpazaShop
        {
            UserId = shopUser.Id, ShopName = "Soweto Spaza", OwnerName = "Lerato", Phone = "0820001100",
            Address = "1 Vilakazi St", City = "Soweto", Province = "Gauteng", Latitude = -26.2485, Longitude = 27.8540,
        };
        var supplierUser = new User { Email = "wh@test.com", Phone = "0820001200", PasswordHash = "h", RoleId = supplierRole.Id };
        var supplier = new Supplier
        {
            UserId = supplierUser.Id, CompanyName = "Warehouse Co", ContactPerson = "Sipho", Phone = "0820001200", Email = "wh@test.com",
            CommissionRate = 5m, Status = "verified", City = "Cape Town", Province = "Western Cape", Latitude = -33.9249, Longitude = 18.4241,
        };
        var jhb = new SupplierBranch { SupplierId = supplier.Id, Name = "JHB warehouse", Address = "1 Main Rd", City = "Johannesburg", Province = "Gauteng", Latitude = -26.2041, Longitude = 28.0473 };
        var durban = new SupplierBranch { SupplierId = supplier.Id, Name = "Durban warehouse", Address = "2 Point Rd", City = "Durban", Province = "KwaZulu-Natal", Latitude = -29.8587, Longitude = 31.0218 };
        var tracked = new Product { SupplierId = supplier.Id, Name = "Cooking oil 2L", Sku = "OIL2", Price = 100m, StockQty = 40, IsAvailable = true, IsApproved = true };
        var shared = new Product { SupplierId = supplier.Id, Name = "Rice 10kg", Sku = "RICE10", Price = 200m, StockQty = 30, IsAvailable = true, IsApproved = true };
        db.AddRange(shopUser, shop, supplierUser, supplier, jhb, durban, tracked, shared,
            // 5 in JHB, 20 in Durban, the other 15 at the head office in Cape Town
            new ProductBranchStock { ProductId = tracked.Id, SupplierBranchId = jhb.Id, StockQty = 5 },
            new ProductBranchStock { ProductId = tracked.Id, SupplierBranchId = durban.Id, StockQty = 20 });
        db.SaveChanges();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, shopUser.Id.ToString())], "Test"))
        };
        var config = new ConfigurationBuilder().Build();
        var checkout = new ShopOrdersController(db, new OrderPaystackPaymentService(new HttpClient(), config),
            new EventPublisher("__nonexistent__", "x", "x"), new PaystackSplitPaymentService(new HttpClient(), config))
        { ControllerContext = new ControllerContext { HttpContext = http } };
        return new Fixture(db, shop, supplier, jhb, durban, tracked, shared, checkout);
    }

    private static PlaceOrderRequest Cod(Product product, int quantity) =>
        new("standard", ShopOrdersController.CashOnDelivery, null, [new PlaceOrderItemRequest(product.Id, quantity, product.Price)]);

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((ObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task Order_ShipsFromTheNearbyBranch_AndTakesStockFromIt()
    {
        var f = Create();

        (await f.Checkout.PlaceOrder(Cod(f.Tracked, 3))).Should().BeOfType<OkObjectResult>();

        var order = await f.Db.Orders.SingleAsync();
        order.SupplierBranchId.Should().Be(f.Jhb.Id);
        // About 20 km from the JHB warehouse at R8/km, which is more than the R150 standard fee
        order.DeliveryDistanceKm.Should().BeInRange(18m, 22m);
        order.DeliveryFee.Should().BeApproximately(order.DeliveryDistanceKm!.Value * 8m, 0.1m);
        var product = await f.Db.Products.Include(p => p.BranchStock).SingleAsync(p => p.Id == f.Tracked.Id);
        product.StockQty.Should().Be(37);
        product.BranchStock.Single(s => s.SupplierBranchId == f.Jhb.Id).StockQty.Should().Be(2);
        product.BranchStock.Single(s => s.SupplierBranchId == f.Durban.Id).StockQty.Should().Be(20);
    }

    [Fact]
    public async Task Order_IsRefused_WhenTheBranchThatDeliversToTheShopRunsOut_AndSaysWhich()
    {
        var f = Create();

        // Durban and Cape Town have plenty, but neither delivers to Soweto
        var result = await f.Checkout.PlaceOrder(Cod(f.Tracked, 10));

        result.Should().BeOfType<BadRequestObjectResult>();
        Data(result).GetProperty("message").GetString().Should()
            .Contain("JHB warehouse").And.Contain("only has 5").And.Contain("Cooking oil 2L");
        (await f.Db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProductsNotTrackedPerBranch_CanShipTheirWholeStockFromAnyLocation()
    {
        var f = Create();
        // 20 is more than any one branch could hold if it were tracked; untracked, any location ships it
        (await f.Checkout.PlaceOrder(Cod(f.Shared, 20))).Should().BeOfType<OkObjectResult>();
        (await f.Db.Products.SingleAsync(p => p.Id == f.Shared.Id)).StockQty.Should().Be(10);
    }

    [Fact]
    public async Task CancellingAnOrder_PutsTheStockBackAtItsBranch()
    {
        var f = Create();
        await f.Checkout.PlaceOrder(Cod(f.Tracked, 3));
        var order = await f.Db.Orders.Include(o => o.Items).SingleAsync();

        var held = OrderStock.Holds(order);
        order.Status = "cancelled";
        await OrderStock.SyncAsync(f.Db, order, held);
        await f.Db.SaveChangesAsync();

        var product = await f.Db.Products.Include(p => p.BranchStock).SingleAsync(p => p.Id == f.Tracked.Id);
        product.StockQty.Should().Be(40);
        product.BranchStock.Single(s => s.SupplierBranchId == f.Jhb.Id).StockQty.Should().Be(5);
    }

    [Fact]
    public async Task Quote_ShowsTheShippingBranch_AndTheStockProblem()
    {
        var f = Create();

        var ok = Data(await f.Checkout.Quote(new QuoteOrderRequest([new PlaceOrderItemRequest(f.Tracked.Id, 2, 0m)], "standard"))).GetProperty("data");
        ok.GetProperty("canCheckout").GetBoolean().Should().BeTrue();
        var supplier = ok.GetProperty("suppliers")[0];
        supplier.GetProperty("shipsFrom").GetProperty("name").GetString().Should().Be("JHB warehouse");
        supplier.GetProperty("deliveryFee").GetDecimal().Should().BeGreaterThan(150m);

        var short_ = Data(await f.Checkout.Quote(new QuoteOrderRequest([new PlaceOrderItemRequest(f.Tracked.Id, 9, 0m)], "standard"))).GetProperty("data");
        short_.GetProperty("canCheckout").GetBoolean().Should().BeFalse();
        short_.GetProperty("suppliers")[0].GetProperty("problem").GetString().Should().Contain("JHB warehouse");
    }

    [Fact]
    public void DeliveryFee_UsesThePerKmRate_WhenItIsMoreThanTheStandardFee()
    {
        var rules = new PlatformRules(); // R150 standard, R250 express, R8 per km
        DeliveryFees.For(rules, "standard", 10).Should().Be(150m);   // 10 km x R8 = R80, so the standard fee
        DeliveryFees.For(rules, "standard", 60).Should().Be(480m);   // 60 km x R8
        DeliveryFees.For(rules, "express", 60).Should().Be(580m);    // plus the R100 express premium
        DeliveryFees.For(rules, "express", null).Should().Be(250m);  // no map positions: flat fees as before
        DeliveryFees.For(rules, "pickup", 60).Should().Be(0m);
    }
}
