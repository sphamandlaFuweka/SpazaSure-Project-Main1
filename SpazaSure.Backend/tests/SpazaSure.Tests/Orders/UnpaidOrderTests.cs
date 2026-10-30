using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.OrderService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>A card order whose payment failed: the shop can pay again or cancel it, but never pay twice.</summary>
public class UnpaidOrderTests
{
    private const string Reference = "SPZ-unpaid-reference";

    /// <param name="paidOnPaystack">Whether Paystack says the last checkout was paid after all.</param>
    private static (SpazaSureDbContext Db, ShopOrdersController Controller, Order Order) Setup(bool paidOnPaystack)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var shopUser = new User { Phone = "0820004000", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "spaza_owner").Id };
        var supplier = new Supplier { UserId = Guid.NewGuid(), CompanyName = "Fresh Foods", ContactPerson = "Bob", Phone = "0820001000", Email = "s@test.com" };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Unpaid Spaza", OwnerName = "Zanele", Phone = "0820004000", Address = "4 Test St" };
        var order = new Order
        {
            SupplierId = supplier.Id, ShopId = shop.Id, OrderNumber = "ORD-2001", Status = "pending", TotalAmount = 250m, Subtotal = 230m,
            DeliveryType = "standard", PaymentMethod = "paystack", PaymentStatus = "pending", PaymentProviderReference = Reference,
        };
        db.AddRange(shopUser, supplier, shop, order);
        db.SaveChanges();

        var handler = new FakePaystack(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"status\":true,\"data\":{\"status\":\"" + (paidOnPaystack ? "success" : "abandoned") + "\",\"reference\":\"" + Reference +
                "\",\"amount\":25000,\"currency\":\"ZAR\"}}", Encoding.UTF8, "application/json"),
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "sk_test_x",
        }).Build();
        var controller = new ShopOrdersController(db, new OrderPaystackPaymentService(new HttpClient(handler), config),
            new EventPublisher("__nonexistent__", "x", "x"), new PaystackSplitPaymentService(new HttpClient(handler), config))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, shopUser.Id.ToString())], "Test")),
                },
            },
        };
        return (db, controller, order);
    }

    [Fact]
    public async Task AnUnpaidCardOrder_CanBeCancelled()
    {
        var (db, controller, order) = Setup(paidOnPaystack: false);

        var result = await controller.CancelUnpaid(order.Id);

        result.Should().BeOfType<OkObjectResult>();
        db.Orders.Single().Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task AnOrderPaidAfterAll_IsNotCancelled_ButMarkedPaid()
    {
        var (db, controller, order) = Setup(paidOnPaystack: true);

        var result = await controller.CancelUnpaid(order.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        db.Orders.Single().Should().Match<Order>(o => o.Status == "pending" && o.PaymentStatus == "paid");
    }

    [Fact]
    public async Task PayingAgain_ForAnOrderPaidAfterAll_DoesNotOpenANewCheckout()
    {
        var (db, controller, order) = Setup(paidOnPaystack: true);

        var result = await controller.PayAgain(order.Id);

        System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value).Should().Contain("\"paid\":true");
        db.Orders.Single().PaymentProviderReference.Should().Be(Reference);
    }

    [Fact]
    public async Task OrdersThatAreNotWaitingForACardPayment_CantBeCancelledHere()
    {
        var (db, controller, order) = Setup(paidOnPaystack: false);
        order.Status = "processing";
        db.SaveChanges();

        (await controller.CancelUnpaid(order.Id)).Should().BeOfType<BadRequestObjectResult>();
    }

    private sealed class FakePaystack(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
