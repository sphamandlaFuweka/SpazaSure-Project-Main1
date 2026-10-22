using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SpazaSure.DeliveryService.Controllers;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.PaymentService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using SpazaSure.VerificationService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>Drivers and tracking (delivery service), invoices (payment service) and their public checks.</summary>
public class DeliveryInvoiceTests
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "test-secret-test-secret-test-secret",
            ["PublicApiUrl"] = "https://api.example.test",
        })
        .Build();

    private sealed record Fixture(SpazaSureDbContext Db, SpazaShop Shop, Supplier Supplier, Order Order);

    private static Fixture Create(string? vatNumber = "4123456789")
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "spaza_owner");
        var shopUser = new User { Email = "track@test.com", Phone = "0820002100", PasswordHash = "h", RoleId = role.Id };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Track Spaza", OwnerName = "Zola", Phone = "0820002100", Address = "5 Test St", Latitude = -26.2, Longitude = 27.9 };
        var supplierUser = new User { Email = "inv@test.com", Phone = "0820002200", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "supplier").Id };
        var supplier = new Supplier
        {
            UserId = supplierUser.Id, CompanyName = "Acme Foods", ContactPerson = "Ann", Phone = "0820002200", Email = "inv@test.com",
            VatNumber = vatNumber, Address = "9 Depot Rd", City = "Johannesburg", Province = "Gauteng", Latitude = -26.1, Longitude = 28.0,
        };
        var product = new Product { SupplierId = supplier.Id, Name = "Sugar 2kg", Sku = "SUG2", Unit = "bag", Price = 115m, StockQty = 10, IsAvailable = true, IsApproved = true };
        var order = new Order
        {
            OrderNumber = "SPZ-TEST-" + Guid.NewGuid().ToString("N")[..6], ShopId = shop.Id, SupplierId = supplier.Id,
            Status = "confirmed", Subtotal = 1150m, DeliveryFee = 150m, PlatformCommission = 57.5m, TotalAmount = 1357.5m,
            PaymentMethod = "cash_on_delivery", PaymentStatus = "pending", DeliveryAddress = shop.Address,
            Items = [new OrderItem { ProductId = product.Id, Quantity = 10, OriginalUnitPrice = 115m, UnitPrice = 115m, LineTotal = 1150m }],
        };
        db.AddRange(shopUser, shop, supplierUser, supplier, product, order);
        db.SaveChanges();
        return new Fixture(db, shop, supplier, order);
    }

    private static ControllerContext As(Guid userId, string role) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "Test")),
        },
    };

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((ObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("data");

    [Fact]
    public async Task AdminRegistersDriver_AllocatesThem_AndTheShopSeesThemOnTheMapWhileDispatched()
    {
        var f = Create();
        var admin = As(Guid.NewGuid(), "admin");
        var drivers = new DriversController(f.Db, Config) { ControllerContext = admin };

        var driver = Data(await drivers.Create(new DriverRequest("Thabo Nkosi", "082 555 1234", "Bakkie", "nd 123-456", "Gauteng")));
        var driverId = driver.GetProperty("id").GetGuid();
        driver.GetProperty("vehicleRegistration").GetString().Should().Be("ND 123-456");
        driver.GetProperty("badgeCode").GetString().Should().MatchRegex("^DRV-[A-Z2-9]{5}$");
        var link = driver.GetProperty("driverLink").GetString()!;
        link.Should().StartWith("https://api.example.test/driver/");

        var deliveries = new DeliveriesController(f.Db, new EventPublisher("__nonexistent__", "x", "x")) { ControllerContext = admin };
        (await deliveries.Assign(f.Order.Id, new AssignDriverRequest(driverId))).Should().BeOfType<OkObjectResult>();

        // The driver shares their location from their link
        var token = link[(link.LastIndexOf('/') + 1)..];
        var portal = new DriverPortalController(f.Db);
        (await portal.Location(token, new DriverLocationRequest(-26.15, 27.95))).Should().BeOfType<OkObjectResult>();
        (await portal.Location("not-a-real-token-not-a-real-token-xx", new DriverLocationRequest(-26.15, 27.95))).Should().BeOfType<NotFoundObjectResult>();

        var tracking = new TrackingController(f.Db) { ControllerContext = As(f.Shop.UserId, "spaza_owner") };
        var beforeDispatch = Data(await tracking.Get(f.Order.Id));
        beforeDispatch.GetProperty("driver").GetProperty("fullName").GetString().Should().Be("Thabo Nkosi");
        beforeDispatch.GetProperty("driverLocation").ValueKind.Should().Be(JsonValueKind.Null);

        f.Order.Status = "dispatched";
        await f.Db.SaveChangesAsync();
        var onTheRoad = Data(await tracking.Get(f.Order.Id));
        onTheRoad.GetProperty("driverLocation").GetProperty("latitude").GetDouble().Should().Be(-26.15);

        // Another shop can't follow this order
        var stranger = new TrackingController(f.Db) { ControllerContext = As(Guid.NewGuid(), "spaza_owner") };
        (await stranger.Get(f.Order.Id)).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Invoice_IsNumberedPerSupplier_ShowsVat_AndRendersAsPdf()
    {
        var f = Create();
        var invoices = new InvoiceService(f.Db, Config);

        var (first, error) = await invoices.GetOrIssueAsync(f.Order.Id);
        error.Should().BeNull();
        first!.Invoice.InvoiceNumber.Should().Be("INV-ACME-000001");
        first.Invoice.IsTaxInvoice.Should().BeTrue();
        first.Invoice.VatAmount.Should().Be(150m); // 15/115 of R1,150

        // Asking again returns the same invoice, never a second number
        (await invoices.GetOrIssueAsync(f.Order.Id)).Result!.Invoice.Id.Should().Be(first.Invoice.Id);

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var pdf = InvoicePdf.Render(first.Invoice, (await invoices.LoadOrderAsync(f.Order.Id))!, invoices.VerifyLink(first.Invoice));
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");

        var link = new Uri(invoices.DownloadLink(first.Invoice.Id));
        var query = System.Web.HttpUtility.ParseQueryString(link.Query);
        invoices.IsValidLink(first.Invoice.Id, query["exp"], query["sig"]).Should().BeTrue();
        invoices.IsValidLink(Guid.NewGuid(), query["exp"], query["sig"]).Should().BeFalse();
    }

    [Fact]
    public async Task SuppliersWithoutVat_GetAPlainInvoice_AndPendingOrdersGetNone()
    {
        var f = Create(vatNumber: null);
        var invoices = new InvoiceService(f.Db, Config);
        var (issued, _) = await invoices.GetOrIssueAsync(f.Order.Id);
        issued!.Invoice.IsTaxInvoice.Should().BeFalse();
        issued.Invoice.VatAmount.Should().Be(0m);

        var pending = new Order { OrderNumber = "SPZ-P", ShopId = f.Shop.Id, SupplierId = f.Supplier.Id, Status = "pending" };
        f.Db.Orders.Add(pending);
        await f.Db.SaveChangesAsync();
        var (none, why) = await invoices.GetOrIssueAsync(pending.Id);
        none.Should().BeNull();
        why.Should().Contain("once the supplier confirms");
    }

    [Fact]
    public async Task VerificationService_ConfirmsGenuineInvoicesAndDrivers_Only()
    {
        var f = Create();
        var (issued, _) = await new InvoiceService(f.Db, Config).GetOrIssueAsync(f.Order.Id);
        var driver = new Driver { FullName = "Thabo", Phone = "+27825551234", VehicleType = "Bakkie", VehicleRegistration = "ND1", AccessToken = new string('a', 48), BadgeCode = "DRV-ABCDE" };
        f.Db.Drivers.Add(driver);
        await f.Db.SaveChangesAsync();
        var verify = new VerificationController(f.Db);

        (await verify.Invoice(issued!.Invoice.InvoiceNumber, issued.Invoice.VerificationCode)).Should().BeOfType<OkObjectResult>();
        (await verify.Invoice(issued.Invoice.InvoiceNumber, "wrongcode")).Should().BeOfType<NotFoundObjectResult>();
        (await verify.Driver("abcde")).Should().BeOfType<OkObjectResult>();
        driver.IsActive = false;
        await f.Db.SaveChangesAsync();
        (await verify.Driver("DRV-ABCDE")).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ShopOwner_RepliesToAReview_AndCustomersSeeTheReply()
    {
        var f = Create();
        var review = new ShopReview { ShopId = f.Shop.Id, ReviewerUserId = Guid.NewGuid(), Rating = 2, Comment = "Bread was stale" };
        f.Db.ShopReviews.Add(review);
        await f.Db.SaveChangesAsync();
        var profile = new SpazaSure.UserService.Controllers.ShopProfileController(f.Db, null!, new EventPublisher("__nonexistent__", "x", "x"), null!)
        { ControllerContext = As(f.Shop.UserId, "spaza_owner") };

        (await profile.ReplyToReview(review.Id, new SpazaSure.UserService.Controllers.ReviewReplyRequest("Sorry! We now get bread daily."))).Should().BeOfType<OkObjectResult>();
        f.Db.ShopReviews.Single().OwnerReply.Should().Be("Sorry! We now get bread daily.");

        var customers = new SpazaSureUserService.Controllers.CustomerShopsController(f.Db, null!) { ControllerContext = As(Guid.NewGuid(), "customer") };
        var reviews = Data(await customers.Reviews(f.Shop.Id));
        reviews[0].GetProperty("ownerReply").GetString().Should().Be("Sorry! We now get bread daily.");

        // Another shop's owner can't reply to it
        var other = new SpazaSure.UserService.Controllers.ShopProfileController(f.Db, null!, new EventPublisher("__nonexistent__", "x", "x"), null!)
        { ControllerContext = As(Guid.NewGuid(), "spaza_owner") };
        (await other.ReplyToReview(review.Id, new SpazaSure.UserService.Controllers.ReviewReplyRequest("hi"))).Should().BeOfType<NotFoundObjectResult>();
    }
}
