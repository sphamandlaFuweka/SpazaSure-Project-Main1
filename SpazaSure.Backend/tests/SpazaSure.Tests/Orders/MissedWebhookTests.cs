using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.OrderService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>
/// A shop paid with Paystack but the webhook never arrived: the order must still be confirmed when
/// the supplier accepts it or the shop comes back from paying, by asking Paystack directly.
/// </summary>
public class MissedWebhookTests
{
    private const string Reference = "SPZ-test-reference";
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");

    private static OrderPaystackPaymentService Paystack(string? status, long amountCents)
    {
        var handler = new FakePaystack(_ => status is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"status\":true,\"data\":{\"status\":\"" + status + "\",\"reference\":\"" + Reference +
                    "\",\"amount\":" + amountCents + ",\"currency\":\"ZAR\"}}",
                    Encoding.UTF8, "application/json"),
            });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "sk_test_x",
        }).Build();
        return new OrderPaystackPaymentService(new HttpClient(handler), config);
    }

    private static (SpazaSureDbContext Db, OrdersController Supplier, Order Order) Setup()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "supplier");
        var supplierUser = new User { Email = "s@test.com", Phone = "0820001000", PasswordHash = "h", RoleId = role.Id };
        var shopUser = new User { Phone = "0820002000", PasswordHash = "h", RoleId = role.Id };
        var supplier = new Supplier { UserId = supplierUser.Id, CompanyName = "Fresh Foods", ContactPerson = "Bob", Phone = "0820001000", Email = "s@test.com" };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Mama's Spaza", OwnerName = "Thandi", Phone = "0820002000", Address = "1 Test St" };
        var order = new Order
        {
            SupplierId = supplier.Id, ShopId = shop.Id, OrderNumber = "ORD-1042", Status = "pending", TotalAmount = 250m,
            DeliveryType = "standard", PaymentMethod = "paystack", PaymentStatus = "pending", PaymentProviderReference = Reference,
        };
        db.AddRange(supplierUser, shopUser, supplier, shop, order);
        db.SaveChanges();
        var controller = new OrdersController(db, Events)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, supplierUser.Id.ToString())], "Test")),
                },
            },
        };
        return (db, controller, order);
    }

    [Fact]
    public async Task SupplierAccepting_APaidOrderWhoseWebhookWasMissed_ConfirmsThePaymentAndAccepts()
    {
        var (db, supplier, order) = Setup();

        var result = await supplier.UpdateStatus(order.Id, new UpdateStatusRequest("processing", null), Paystack("success", 25000));

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.Orders.AsNoTracking().SingleAsync();
        saved.PaymentStatus.Should().Be("paid");
        saved.Status.Should().Be("processing");
    }

    [Fact]
    public async Task SplitPaystackPayment_MarksSupplierSharePaidImmediately()
    {
        var (db, _, order) = Setup();
        order.PaystackSplitCode = "SPL-test";
        await db.SaveChangesAsync();

        var confirmed = await Paystack("success", 25000)
            .ConfirmOrdersAsync(db, Events, Reference);

        confirmed.Should().BeTrue();
        var saved = await db.Orders.AsNoTracking().SingleAsync();
        saved.PaymentStatus.Should().Be("paid");
        saved.PaidOutAt.Should().NotBeNull();
        saved.PayoutReference.Should().Be(Reference);
    }

    [Theory]
    [InlineData("abandoned", 25000)] // Paystack says it wasn't paid
    [InlineData("success", 10000)]   // paid less than the order total
    [InlineData(null, 0)]            // Paystack doesn't know the reference
    public async Task SupplierAccepting_AnOrderPaystackDoesNotConfirm_IsStillRefused(string? status, long cents)
    {
        var (db, supplier, order) = Setup();

        var result = await supplier.UpdateStatus(order.Id, new UpdateStatusRequest("processing", null), Paystack(status, cents));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Orders.AsNoTracking().SingleAsync()).PaymentStatus.Should().Be("pending");
    }

    [Fact]
    public void PaystackAddingItsFeesOnTop_StillCoversTheOrder()
    {
        var orders = new[] { new Order { TotalAmount = 250m } };

        OrderPayments.CoversOrders(25000, orders).Should().BeTrue();
        OrderPayments.CoversOrders(25750, orders).Should().BeTrue();
        OrderPayments.CoversOrders(24999, orders).Should().BeFalse();
    }

    [Fact]
    public async Task RefundAsync_RequestsTheExactOrderAmountInMinorUnits()
    {
        HttpMethod? sentMethod = null;
        Uri? sentUri = null;
        string? sentBody = null;
        var handler = new FakePaystack(request =>
        {
            sentMethod = request.Method;
            sentUri = request.RequestUri;
            sentBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"status":true,"data":{"id":12345,"status":"pending"}}""",
                    Encoding.UTF8, "application/json"),
            };
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "sk_test_x",
        }).Build();
        var service = new OrderPaystackPaymentService(new HttpClient(handler), config);

        var refund = await service.RefundAsync(Reference, 12.34m, "Order dispute refund");

        refund.Id.Should().Be("12345");
        refund.Status.Should().Be("pending");
        sentMethod.Should().Be(HttpMethod.Post);
        sentUri!.AbsolutePath.Should().Be("/refund");
        using var payload = JsonDocument.Parse(sentBody!);
        payload.RootElement.GetProperty("transaction").GetString().Should().Be(Reference);
        payload.RootElement.GetProperty("amount").GetInt64().Should().Be(1234);
        payload.RootElement.GetProperty("currency").GetString().Should().Be("ZAR");
    }

    private sealed class FakePaystack(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
