using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            new AdminPayoutsController(db, events),
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

    [Fact]
    public async Task SupplierCanProcessUnpaidCashOrder_AndDeliveryMarksItPaid()
    {
        var (orders, _, _, db, supplier) = Create();
        var order = AddOrder(db, supplier, Cod, "pending", "pending");

        // Unlike Paystack orders, cash orders don't wait for an online payment
        foreach (var next in new[] { "processing", "dispatched", "delivered" })
            (await orders.UpdateStatus(order.Id, new UpdateStatusRequest(next, null))).Should().BeOfType<OkObjectResult>();

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

        // SpazaSure collected the cash and owes the supplier products + delivery (R1,150 each)
        data.GetProperty("available").GetDecimal().Should().Be(1150m);
        data.GetProperty("pending").GetDecimal().Should().Be(1150m);
        data.GetProperty("awaitingDelivery").GetDecimal().Should().Be(0m); // unpaid cash orders aren't owed yet
    }

    [Fact]
    public async Task AdminPaysOutCashOrders_LikeAnyOtherOrder()
    {
        var (_, payouts, admin, db, supplier) = Create();
        AddOrder(db, supplier, Cod, "delivered", "paid", DateTime.UtcNow.AddDays(-5));
        AddOrder(db, supplier, "paystack", "delivered", "paid", DateTime.UtcNow.AddDays(-5));

        Data(await admin.List()).GetProperty("totalAvailable").GetDecimal().Should().Be(2300m);

        (await admin.MarkPaid(supplier.Id, new MarkPayoutPaidRequest("EFT-COD-001"))).Should().BeOfType<OkObjectResult>();
        var data = Data(await payouts.Summary());
        data.GetProperty("available").GetDecimal().Should().Be(0m);
        data.GetProperty("paidOut").GetDecimal().Should().Be(2300m);
    }
}
