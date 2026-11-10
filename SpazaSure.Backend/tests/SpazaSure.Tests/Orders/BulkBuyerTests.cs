using System.Net;
using System.Security.Claims;
using System.Text;
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
using SpazaSure.ProductService.Controllers;
using SpazaSure.Shared.Helpers;
using SpazaSure.UserService.Services;
using SpazaSureUserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>
/// Shoppers on the Bulk Buyer plan (R150 a month) order from suppliers at the shop price plus the
/// Bulk Buyer markup, from R1,000 per supplier, paying by card, delivered or collected.
/// </summary>
public class BulkBuyerTests
{
    private sealed record Fixture(SpazaSureDbContext Db, User Customer, SpazaShop? Account, Product Product, Supplier Supplier);

    // Supplier price R100; shops pay R105 (5% platform fee); bulk buyers R115.50 (+10%)
    private static Fixture Create(bool bulkActive = true, bool withAccount = true, string plan = BulkBuying.PlanKey)
    {
        var db = new SpazaSureDbContext(new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var customerRole = new Role { Name = "customer", IsActive = true };
        var supplierRole = new Role { Name = "supplier", IsActive = true };
        var customer = new User { Email = "thandi@test.com", Phone = "0820009000", PasswordHash = "h", RoleId = customerRole.Id };
        var supplierUser = new User { Email = "wh@test.com", Phone = "0820009100", PasswordHash = "h", RoleId = supplierRole.Id };
        var supplier = new Supplier
        {
            UserId = supplierUser.Id, CompanyName = "Joburg Wholesale", ContactPerson = "Sipho", Phone = "0820009100", Email = "wh@test.com",
            Status = "verified", City = "Johannesburg", Province = "Gauteng", Latitude = -26.2041, Longitude = 28.0473,
            BankAccount = "1234567890", BankBranchCode = "250655", PaystackSubaccountCode = "ACCT_test",
        };
        var product = new Product { SupplierId = supplier.Id, Name = "Rice 10kg", Sku = "RICE10", Price = 100m, StockQty = 100, IsAvailable = true, IsApproved = true };
        db.AddRange(customerRole, supplierRole, customer, supplierUser, supplier, product);
        db.CustomerSubscriptions.Add(new CustomerSubscription
        {
            CustomerUserId = customer.Id, PlanKey = plan, Status = bulkActive ? "active" : "cancelled",
            CurrentPeriodEnd = bulkActive ? DateTime.UtcNow.AddDays(20) : DateTime.UtcNow.AddDays(-1),
        });
        SpazaShop? account = null;
        if (withAccount)
        {
            account = new SpazaShop
            {
                UserId = customer.Id, AccountType = ShopAccountTypes.BulkBuyer, ShopName = "Thandi M", OwnerName = "Thandi M",
                Phone = "0820009000", Address = "12 Vilakazi St", City = "Soweto", Province = "Gauteng",
                Latitude = -26.2485, Longitude = 27.8540, Status = "active",
            };
            db.SpazaShops.Add(account);
        }
        db.SaveChanges();
        return new Fixture(db, customer, account, product, supplier);
    }

    private static HttpContext AsCustomer(User user) => new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, "customer")], "Test")),
    };

    private static ShopOrdersController Checkout(Fixture f)
    {
        var handler = new FakePaystack();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "sk_test_x",
            ["Paystack:CallbackUrls:ShopCheckout"] = "https://app.test/checkout",
        }).Build();
        return new ShopOrdersController(f.Db, new OrderPaystackPaymentService(new HttpClient(handler), config),
            new EventPublisher("__nonexistent__", "x", "x"), new PaystackSplitPaymentService(new HttpClient(handler), config))
        { ControllerContext = new ControllerContext { HttpContext = AsCustomer(f.Customer) } };
    }

    private static PlaceOrderRequest Order(Product product, int quantity, string payment = "paystack", string delivery = "standard") =>
        new(delivery, payment, null, [new PlaceOrderItemRequest(product.Id, quantity, 0m)]);

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((ObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task Quote_UsesTheBulkPrice_AndTheR1000BulkMinimum_NotTheShopMinimum()
    {
        var f = Create();

        // 9 x R115.50 = R1,039.50: over the R1,000 bulk minimum, though under the R2,000 shop minimum
        var ok = Data(await Checkout(f).Quote(new QuoteOrderRequest([new PlaceOrderItemRequest(f.Product.Id, 9, 0m)], "standard")));
        ok.GetProperty("data").GetProperty("canCheckout").GetBoolean().Should().BeTrue();
        ok.GetProperty("data").GetProperty("suppliers")[0].GetProperty("subtotal").GetDecimal().Should().Be(1039.50m);

        var small = Data(await Checkout(f).Quote(new QuoteOrderRequest([new PlaceOrderItemRequest(f.Product.Id, 8, 0m)], "standard")));
        small.GetProperty("data").GetProperty("canCheckout").GetBoolean().Should().BeFalse();
        small.GetProperty("data").GetProperty("suppliers")[0].GetProperty("problem").GetString().Should().Contain("minimum order from Joburg Wholesale");
    }

    [Fact]
    public async Task PlaceOrder_ChargesTheBulkPrice_AndSpazaSureKeepsTheDifference()
    {
        var f = Create();

        var result = await Checkout(f).PlaceOrder(Order(f.Product, 10, delivery: "pickup"));

        result.Should().BeOfType<OkObjectResult>(Data(result).ToString());
        Data(result).GetProperty("data").GetProperty("checkoutUrl").GetString().Should().StartWith("https://checkout.paystack.com/");
        var order = await f.Db.Orders.Include(o => o.Items).SingleAsync();
        order.ShopId.Should().Be(f.Account!.Id);
        order.Items.Single().ShopUnitPrice.Should().Be(115.50m);
        order.Subtotal.Should().Be(1000m);            // the supplier is paid their own price
        order.PlatformCommission.Should().Be(155m);   // SpazaSure's fee plus the Bulk Buyer markup
        order.DeliveryFee.Should().Be(0m);            // collected from the supplier
        order.DeliveryCode.Should().BeNull();
        order.TotalAmount.Should().Be(1155m);
        order.PaymentMethod.Should().Be("paystack");
    }

    [Theory]
    [InlineData(ShopOrdersController.CashOnDelivery)]
    [InlineData("wallet")]
    public async Task PlaceOrder_IsCardOnly_ForBulkBuyers(string payment)
    {
        var f = Create();

        var result = await Checkout(f).PlaceOrder(Order(f.Product, 10, payment));

        result.Should().BeOfType<BadRequestObjectResult>();
        Data(result).GetProperty("message").GetString().Should().Contain("card");
        (await f.Db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShoppersWithoutTheBulkBuyerPlan_CannotOrder_EvenWithPlus()
    {
        var f = Create(plan: CustomerPlus.PlusPlanKey);

        var result = await Checkout(f).PlaceOrder(Order(f.Product, 10));

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await f.Db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ABulkBuyerWithoutAnAddress_IsAskedForOne()
    {
        var f = Create(withAccount: false);

        var result = await Checkout(f).Quote(new QuoteOrderRequest([new PlaceOrderItemRequest(f.Product.Id, 10, 0m)], "standard"));

        result.Should().BeOfType<ConflictObjectResult>();
        Data(result).GetProperty("message").GetString().Should().Contain("address");
    }

    [Fact]
    public async Task ALapsedBulkBuyer_CanStillSeeTheirOrders()
    {
        var f = Create();
        await Checkout(f).PlaceOrder(Order(f.Product, 10));
        var sub = await f.Db.CustomerSubscriptions.SingleAsync();
        sub.Status = "cancelled";
        await f.Db.SaveChangesAsync();

        var result = await Checkout(f).List();

        result.Should().BeOfType<OkObjectResult>();
        Data(result).GetProperty("data").GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Marketplace_ShowsBulkPrices_OnlyToBulkBuyers()
    {
        var bulk = Create();
        var market = new ShopMarketplaceController(bulk.Db) { ControllerContext = new ControllerContext { HttpContext = AsCustomer(bulk.Customer) } };
        var items = Data(await market.Products()).GetProperty("data").GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("price").GetDecimal().Should().Be(115.50m);

        var plus = Create(plan: CustomerPlus.PlusPlanKey, withAccount: false);
        var refused = new ShopMarketplaceController(plus.Db) { ControllerContext = new ControllerContext { HttpContext = AsCustomer(plus.Customer) } };
        (await refused.Products()).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task BulkBuyerAccounts_AreNeverListedAsShops()
    {
        var f = Create();
        f.Db.SpazaShops.Add(new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Real Spaza", OwnerName = "Lerato", Phone = "0820001111", Address = "1 Main Rd", Status = "verified" });
        await f.Db.SaveChangesAsync();
        var directory = new CustomerShopsController(f.Db, new AddressGeocodingSignal())
        { ControllerContext = new ControllerContext { HttpContext = AsCustomer(f.Customer) } };

        var shops = Data(await directory.GetShops(null)).GetProperty("data");

        shops.GetArrayLength().Should().Be(1);
        shops[0].GetProperty("shopName").GetString().Should().Be("Real Spaza");
        (await directory.GetById(f.Account!.Id)).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void BulkBuyer_IncludesPlus()
    {
        var bulk = new CustomerSubscription { PlanKey = BulkBuying.PlanKey, Status = "active", CurrentPeriodEnd = DateTime.UtcNow.AddDays(5) };
        CustomerPlus.IsActive(bulk).Should().BeTrue();
        BulkBuying.IsActive(bulk).Should().BeTrue();
        BulkBuying.IsActive(new CustomerSubscription { PlanKey = CustomerPlus.PlusPlanKey, Status = "active" }).Should().BeFalse();
        CustomerPlus.PriceOf(BulkBuying.PlanKey).Should().Be(150m);
    }

    /// <summary>Paystack: creates a split, then a checkout page for the reference it was given.</summary>
    private sealed class FakePaystack : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            string json;
            if (path.EndsWith("/split"))
                json = "{\"status\":true,\"data\":{\"split_code\":\"SPL_test\"}}";
            else if (path.EndsWith("/transaction/initialize"))
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var reference = body.RootElement.GetProperty("reference").GetString();
                json = "{\"status\":true,\"data\":{\"authorization_url\":\"https://checkout.paystack.com/x\",\"reference\":\"" + reference + "\"}}";
            }
            else
                json = "{\"status\":false,\"message\":\"unexpected\"}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
