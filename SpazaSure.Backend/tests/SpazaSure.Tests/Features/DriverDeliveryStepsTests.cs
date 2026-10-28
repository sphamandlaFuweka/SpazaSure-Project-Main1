using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.DeliveryService.Controllers;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>Drivers confirm collection and delivery on their link; delivery only at the right shop.</summary>
public class DriverDeliveryStepsTests
{
    private sealed record Fixture(SpazaSureDbContext Db, SpazaShop Shop, Driver Driver, Order Order, DriverPortalController Portal);

    private static Fixture Create(string paymentMethod = "cash_on_delivery", string status = "ready", string paymentStatus = "pending")
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var shopUser = new User { Email = "steps@test.com", Phone = "0820003100", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "spaza_owner").Id };
        var shop = new SpazaShop
        {
            UserId = shopUser.Id, ShopName = "Steps Spaza", OwnerName = "Zola", Phone = "0820003100", Address = "5 Test St",
            ShopCode = "SS482913", Latitude = -26.2000, Longitude = 27.9000,
        };
        var supplierUser = new User { Email = "stepsup@test.com", Phone = "0820003200", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "supplier").Id };
        var supplier = new Supplier { UserId = supplierUser.Id, CompanyName = "Acme Foods", ContactPerson = "Ann", Phone = "0820003200", Email = "stepsup@test.com" };
        var driver = new Driver { FullName = "Thabo Nkosi", Phone = "0825551234", VehicleRegistration = "ND 123-456", AccessToken = new string('b', 48), BadgeCode = "DRV-ABCDE" };
        var order = new Order
        {
            OrderNumber = "SPZ-STEP-1", ShopId = shop.Id, SupplierId = supplier.Id, DriverId = driver.Id,
            Status = status, Subtotal = 1000m, DeliveryFee = 150m, PlatformCommission = 50m, TotalAmount = 1200m,
            PaymentMethod = paymentMethod, PaymentStatus = paymentStatus, DeliveryAddress = shop.Address,
        };
        db.AddRange(shopUser, shop, supplierUser, supplier, driver, order);
        db.SaveChanges();
        return new Fixture(db, shop, driver, order, new DriverPortalController(db, null, new EventPublisher("__nonexistent__", "x", "x")));
    }

    private string Token(Fixture f) => f.Driver.AccessToken;

    [Fact]
    public async Task Driver_CollectsThenDelivers_AfterConfirmingTheShopNumber()
    {
        var f = Create();

        (await f.Portal.Collected(Token(f), f.Order.OrderNumber)).Should().BeOfType<OkObjectResult>();
        f.Order.Status.Should().Be("dispatched");
        f.Order.CollectedAt.Should().NotBeNull();

        // Wrong shop: refused
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("SS000001", true, null, null)))
            .Should().BeOfType<BadRequestObjectResult>();
        f.Order.Status.Should().Be("dispatched");

        // The sticker's QR is a verify link; typed numbers can have spaces and lower case
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber,
            new("https://portal.spazasure.co.za/verify/SS482913", true, -26.2001, 27.9002)))
            .Should().BeOfType<OkObjectResult>();
        f.Order.Status.Should().Be("delivered");
        f.Order.DeliveredBy.Should().Be("driver");
        f.Order.DeliveredAt.Should().NotBeNull();
        f.Order.PaymentStatus.Should().Be("paid"); // cash collected by the driver
        f.Order.DeliveredLatitude.Should().Be(-26.2001);
    }

    [Fact]
    public async Task CashOrder_NeedsTheDriverToConfirmTheCash()
    {
        var f = Create(status: "dispatched");
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("ss 482 913", false, null, null)))
            .Should().BeOfType<BadRequestObjectResult>();
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("ss 482 913", true, null, null)))
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UnpaidCardOrder_CantBeCollected()
    {
        var f = Create(paymentMethod: "paystack", paymentStatus: "pending");
        (await f.Portal.Collected(Token(f), f.Order.OrderNumber)).Should().BeOfType<BadRequestObjectResult>();
        f.Order.Status.Should().Be("ready");
    }

    [Fact]
    public async Task OrderNotYetAccepted_CantBeCollected()
    {
        var f = Create(status: "pending");
        (await f.Portal.Collected(Token(f), f.Order.OrderNumber)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeliveringBeforeCollecting_IsRefused()
    {
        var f = Create(paymentMethod: "wallet", paymentStatus: "paid");
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("SS482913", false, null, null)))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AnotherDriversOrder_IsNotFound()
    {
        var f = Create();
        var other = new Driver { FullName = "Other", Phone = "0825550000", VehicleRegistration = "X", AccessToken = new string('c', 48), BadgeCode = "DRV-ZZZZZ" };
        f.Db.Drivers.Add(other);
        await f.Db.SaveChangesAsync();
        (await f.Portal.Collected(other.AccessToken, f.Order.OrderNumber)).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ShopWithoutANumber_GetsOneWhenTheOrderIsCollected()
    {
        var f = Create(paymentMethod: "wallet", paymentStatus: "paid");
        f.Shop.ShopCode = null;
        await f.Db.SaveChangesAsync();

        await f.Portal.Collected(Token(f), f.Order.OrderNumber);

        f.Shop.ShopCode.Should().MatchRegex("^SS[0-9]{6}$");
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new(f.Shop.ShopCode, false, null, null)))
            .Should().BeOfType<OkObjectResult>();
    }
}
