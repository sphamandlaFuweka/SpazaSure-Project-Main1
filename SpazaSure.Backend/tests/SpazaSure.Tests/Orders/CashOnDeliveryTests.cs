using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

public class CashOnDeliveryTests
{
    private const string Cod = ShopOrdersController.CashOnDelivery;

    private static (OrdersController Orders, SupplierPayoutsController Payouts, AdminPayoutsController Admin, SpazaSureDbContext Db, Supplier Supplier) Create()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "supplier");
        var user = new User { Email = "cod@test.com", Phone = "0820005000", PasswordHash = "h", RoleId = role.Id };
        var supplier = new Supplier { UserId = user.Id, CompanyName = "Cash Co", ContactPerson = "Musa", Phone = "0820005000", Email = "cod@test.com" };
        var shopUser = new User { Email = "codshop@test.com", Phone = "0820006000", PasswordHash = "h", RoleId = role.Id };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Corner Spaza", OwnerName = "Nomsa", Phone = "0820006000", Address = "3 Test St" };
        db.AddRange(user, supplier, shopUser, shop);
        db.SaveChanges();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test"))
        };
        var events = new EventPublisher("__nonexistent__", "x", "x");
        return (
            new OrdersController(db, events) { ControllerContext = new ControllerContext { HttpContext = http } },
            new SupplierPayoutsController(db) { ControllerContext = new ControllerContext { HttpContext = http } },
            new AdminPayoutsController(db, events, TestPaystack.Transfers()),
            db, supplier);
    }

    private static Order AddOrder(SpazaSureDbContext db, Supplier supplier, string method, string status, string paymentStatus, DateTime? deliveredAt = null)
    {
        var order = new Order
        {
            SupplierId = supplier.Id, ShopId = db.SpazaShops.First().Id,
            OrderNumber = "SPZ-" + Guid.NewGuid().ToString("N")[..8], Status = status,
            Subtotal = 1000m, DeliveryFee = 150m, PlatformCommission = 50m, TotalAmount = 1200m,
            PaymentMethod = method, PaymentStatus = paymentStatus, DeliveredAt = deliveredAt,
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return order;
    }

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    /// <summary>SpazaSure's delivery team (admin) collects and dispatches orders.</summary>
    private static AdminOrdersController CreateAdmin(SpazaSureDbContext db) =>
        new(db, new EventPublisher("__nonexistent__", "x", "x"))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static ShopOrdersController CreateShopController(SpazaSureDbContext db)
    {
        var shop = db.SpazaShops.First();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, shop.UserId.ToString())], "Test"))
        };
        return new ShopOrdersController(db, null!, new EventPublisher("__nonexistent__", "x", "x"),
            new SpazaSure.OrderService.Services.PaystackSplitPaymentService(
                new HttpClient(), new ConfigurationBuilder().Build()))
        { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    [Fact]
    public async Task SpazaSureDispatchingCashOrder_GeneratesDeliveryCode_AndSupplierCannotDispatchOrDeliver()
    {
        var (orders, _, _, db, supplier) = Create();
        var order = AddOrder(db, supplier, Cod, "processing", "pending");

        // The supplier packs the order; collecting and delivering is SpazaSure's job
        (await orders.UpdateStatus(order.Id, new UpdateStatusRequest("dispatched", null))).Should().BeOfType<BadRequestObjectResult>();
        (await orders.UpdateStatus(order.Id, new UpdateStatusRequest("ready", null))).Should().BeOfType<OkObjectResult>();
        (await CreateAdmin(db).UpdateStatus(order.Id, new UpdateStatusRequest("dispatched", null))).Should().BeOfType<OkObjectResult>();

        var saved = await db.Orders.FindAsync(order.Id);
        saved!.DeliveryCode.Should().MatchRegex(@"^\d{6}$");
        var supplierOrder = Data(await orders.Get(order.Id));
        supplierOrder.TryGetProperty("deliveryCode", out _).Should().BeFalse();
        supplierOrder.TryGetProperty("deliveryFee", out _).Should().BeFalse();
        supplierOrder.TryGetProperty("platformCommission", out _).Should().BeFalse();
        supplierOrder.TryGetProperty("totalAmount", out _).Should().BeFalse();

        (await orders.UpdateStatus(order.Id, new UpdateStatusRequest("delivered", null))).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ShopMustEnterCorrectDeliveryCode_ToConfirmCashOrder()
    {
        var (orders, _, _, db, supplier) = Create();
        var order = AddOrder(db, supplier, Cod, "processing", "pending");
        await orders.UpdateStatus(order.Id, new UpdateStatusRequest("ready", null));
        await CreateAdmin(db).UpdateStatus(order.Id, new UpdateStatusRequest("dispatched", null));
        var code = (await db.Orders.FindAsync(order.Id))!.DeliveryCode!;
        var shop = CreateShopController(db);

        (await shop.ConfirmDelivery(order.Id, null)).Should().BeOfType<BadRequestObjectResult>();
        (await shop.ConfirmDelivery(order.Id, new ConfirmDeliveryRequest("000000x"))).Should().BeOfType<BadRequestObjectResult>();
        (await db.Orders.FindAsync(order.Id))!.Status.Should().Be("dispatched");

        (await shop.ConfirmDelivery(order.Id, new ConfirmDeliveryRequest($" {code} "))).Should().BeOfType<OkObjectResult>();
        var saved = await db.Orders.FindAsync(order.Id);
        saved!.Status.Should().Be("delivered");
        saved.PaymentStatus.Should().Be("paid");
        saved.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PrepaidOrder_ConfirmsWithoutDeliveryCode()
    {
        var (_, _, _, db, supplier) = Create();
        var order = AddOrder(db, supplier, "paystack", "dispatched", "paid");

        (await CreateShopController(db).ConfirmDelivery(order.Id, null)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SupplierCanProcessUnpaidCashOrder_AndShopConfirmationMarksItPaid()
    {
        var (orders, _, _, db, supplier) = Create();
        var order = AddOrder(db, supplier, Cod, "pending", "pending");

        // Unlike Paystack orders, cash orders don't wait for an online payment
        foreach (var next in new[] { "processing", "ready" })
            (await orders.UpdateStatus(order.Id, new UpdateStatusRequest(next, null))).Should().BeOfType<OkObjectResult>();
        (await CreateAdmin(db).UpdateStatus(order.Id, new UpdateStatusRequest("dispatched", null))).Should().BeOfType<OkObjectResult>();
        var code = (await db.Orders.FindAsync(order.Id))!.DeliveryCode;
        (await CreateShopController(db).ConfirmDelivery(order.Id, new ConfirmDeliveryRequest(code))).Should().BeOfType<OkObjectResult>();

        var saved = await db.Orders.FindAsync(order.Id);
        saved!.PaymentStatus.Should().Be("paid");
        saved.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UnpaidPaystackOrder_StillCannotBeProcessed()
    {
        var (orders, _, _, db, supplier) = Create();
        var order = AddOrder(db, supplier, "paystack", "pending", "pending");

        var result = await orders.UpdateStatus(order.Id, new UpdateStatusRequest("processing", null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeliveredCashOrder_IsPaidOutToSupplierLikePaystack()
    {
        var (_, payouts, _, db, supplier) = Create();
        AddOrder(db, supplier, Cod, "delivered", "paid", DateTime.UtcNow.AddDays(-5)); // past the 3-day delay
        AddOrder(db, supplier, Cod, "delivered", "paid", DateTime.UtcNow.AddHours(-1)); // still in the delay window
        AddOrder(db, supplier, Cod, "dispatched", "pending");                           // SpazaSure hasn't collected the cash yet

        var data = Data(await payouts.Summary());

        // SpazaSure collected the cash and owes the supplier the products (R1,000 each); it keeps the delivery fee
        data.GetProperty("available").GetDecimal().Should().Be(1000m);
        data.GetProperty("pending").GetDecimal().Should().Be(1000m);
        data.GetProperty("awaitingDelivery").GetDecimal().Should().Be(0m); // unpaid cash orders aren't owed yet
    }

    [Fact]
    public async Task AdminPaysOutCashOrders_LikeAnyOtherOrder()
    {
        var (_, payouts, admin, db, supplier) = Create();
        AddOrder(db, supplier, Cod, "delivered", "paid", DateTime.UtcNow.AddDays(-5));
        AddOrder(db, supplier, "paystack", "delivered", "paid", DateTime.UtcNow.AddDays(-5));

        Data(await admin.List()).GetProperty("totalAvailable").GetDecimal().Should().Be(2000m);

        (await admin.MarkPaid(supplier.Id, new MarkPayoutPaidRequest("EFT-COD-001"))).Should().BeOfType<OkObjectResult>();
        var data = Data(await payouts.Summary());
        data.GetProperty("available").GetDecimal().Should().Be(0m);
        data.GetProperty("paidOut").GetDecimal().Should().Be(2000m);
    }

    [Fact]
    public async Task AdminCanProgressAndCancelAnAlreadySplitPaystackOrder()
    {
        var (_, _, _, db, supplier) = Create();
        var order = AddOrder(db, supplier, "paystack", "pending", "paid");
        order.PaystackSplitCode = "SPL-test";
        order.PaidOutAt = DateTime.UtcNow;
        order.PayoutReference = "SPZ-test";
        await db.SaveChangesAsync();
        var admin = CreateAdmin(db);

        foreach (var status in new[] { "processing", "ready", "dispatched", "delivered" })
            (await admin.UpdateStatus(order.Id, new UpdateStatusRequest(status, null))).Should().BeOfType<OkObjectResult>();

        (await admin.UpdateStatus(order.Id, new UpdateStatusRequest("cancelled", "Customer refund"))).Should().BeOfType<OkObjectResult>();
        var saved = await db.Orders.FindAsync(order.Id);
        saved!.Status.Should().Be("cancelled");
        saved.PaymentStatus.Should().Be("paid");
        saved.PaidOutAt.Should().NotBeNull();
    }
}
