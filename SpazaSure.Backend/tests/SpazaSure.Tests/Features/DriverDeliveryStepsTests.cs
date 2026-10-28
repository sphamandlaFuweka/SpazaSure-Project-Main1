using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.DeliveryService.Controllers;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>Drivers confirm collection and delivery on their link; delivery only with the order's own code.</summary>
public class DriverDeliveryStepsTests
{
    private sealed record Fixture(SpazaSureDbContext Db, SpazaShop Shop, Driver Driver, Order Order, DriverPortalController Portal);

    private static Fixture Create(string paymentMethod = "cash_on_delivery", string status = "ready", string paymentStatus = "pending",
        string? code = "221032")
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var shopUser = new User { Email = "steps@test.com", Phone = "0820003100", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "spaza_owner").Id };
        var shop = new SpazaShop
        {
            UserId = shopUser.Id, ShopName = "Steps Spaza", OwnerName = "Zola", Phone = "0820003100", Address = "5 Test St",
            Latitude = -26.2000, Longitude = 27.9000,
        };
        var supplierUser = new User { Email = "stepsup@test.com", Phone = "0820003200", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "supplier").Id };
        var supplier = new Supplier { UserId = supplierUser.Id, CompanyName = "Acme Foods", ContactPerson = "Ann", Phone = "0820003200", Email = "stepsup@test.com" };
        var driver = new Driver { FullName = "Thabo Nkosi", Phone = "0825551234", VehicleRegistration = "ND 123-456", AccessToken = new string('b', 48), BadgeCode = "DRV-ABCDE" };
        var order = new Order
        {
            OrderNumber = "SPZ-STEP-1", ShopId = shop.Id, SupplierId = supplier.Id, DriverId = driver.Id,
            Status = status, Subtotal = 1000m, DeliveryFee = 150m, PlatformCommission = 50m, TotalAmount = 1200m,
            PaymentMethod = paymentMethod, PaymentStatus = paymentStatus, DeliveryAddress = shop.Address, DeliveryCode = code,
        };
        db.AddRange(shopUser, shop, supplierUser, supplier, driver, order);
        db.SaveChanges();
        return new Fixture(db, shop, driver, order, new DriverPortalController(db, null, new EventPublisher("__nonexistent__", "x", "x")));
    }

    private static string Token(Fixture f) => f.Driver.AccessToken;

    [Fact]
    public async Task Driver_CollectsThenDelivers_WithTheOrdersDeliveryCode()
    {
        var f = Create();

        (await f.Portal.Collected(Token(f), f.Order.OrderNumber)).Should().BeOfType<OkObjectResult>();
        f.Order.Status.Should().Be("dispatched");
        f.Order.CollectedAt.Should().NotBeNull();

        // Wrong code: refused, and counted
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("123456", true, null, null)))
            .Should().BeOfType<BadRequestObjectResult>();
        f.Order.Status.Should().Be("dispatched");
        f.Order.DeliveryCodeAttempts.Should().Be(1);

        // Scanned from the QR code in the shop's app
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber,
            new(OrderDeliveryCodes.QrPrefix + "221032", true, -26.2001, 27.9002)))
            .Should().BeOfType<OkObjectResult>();
        f.Order.Status.Should().Be("delivered");
        f.Order.DeliveredBy.Should().Be("driver");
        f.Order.PaymentStatus.Should().Be("paid"); // cash collected by the driver
        f.Order.DeliveredLatitude.Should().Be(-26.2001);
    }

    [Fact]
    public async Task TypedCodes_CanHaveSpaces()
    {
        var f = Create(paymentMethod: "wallet", status: "dispatched", paymentStatus: "paid");
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("221 032", false, null, null)))
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task FiveWrongCodes_LockTheOrder()
    {
        var f = Create(paymentMethod: "wallet", status: "dispatched", paymentStatus: "paid");
        for (var i = 0; i < OrderDeliveryCodes.MaxAttempts; i++)
            await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("000000", false, null, null));

        // Even the right code no longer works: SpazaSure has to step in
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("221032", false, null, null)))
            .Should().BeOfType<BadRequestObjectResult>();
        f.Order.Status.Should().Be("dispatched");
    }

    [Fact]
    public async Task CashOrder_NeedsTheDriverToConfirmTheCash()
    {
        var f = Create(status: "dispatched");
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("221032", false, null, null)))
            .Should().BeOfType<BadRequestObjectResult>();
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("221032", true, null, null)))
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
        (await f.Portal.Delivered(Token(f), f.Order.OrderNumber, new("221032", false, null, null)))
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
    public async Task OlderOrderWithoutACode_GetsAUniqueOneWhenCollected()
    {
        var f = Create(paymentMethod: "wallet", paymentStatus: "paid", code: null);
        await f.Portal.Collected(Token(f), f.Order.OrderNumber);
        f.Order.DeliveryCode.Should().MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public async Task Codes_AreUniqueAcrossOrders()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var batch = new HashSet<string>();
        var codes = new List<string>();
        for (var i = 0; i < 300; i++) codes.Add(await OrderDeliveryCodes.NewAsync(db, batch));
        codes.Should().OnlyHaveUniqueItems().And.AllSatisfy(c => c.Should().MatchRegex("^[0-9]{6}$"));
    }

    [Theory]
    [InlineData("221032", "221032")]
    [InlineData(" 221 032 ", "221032")]
    [InlineData("SPAZASURE-DELIVERY:221032", "221032")]
    [InlineData("22103", null)]
    [InlineData("", null)]
    public void Read_TakesTypedOrScannedCodes(string input, string? expected) =>
        OrderDeliveryCodes.Read(input).Should().Be(expected);
}
