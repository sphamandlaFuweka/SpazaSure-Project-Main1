using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.OrderService.Services;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>
/// SpazaSure offers a deal to the supplier, who sets its discount and accepts or declines with a
/// reason; the admin then publishes it. Admins can edit any deal, live ones included.
/// </summary>
public class GroupBuyOfferTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");
    private static readonly DeliveryPricingService Pricing = new(Options.Create(new DeliveryPricingOptions()));

    private sealed record Fixture(SpazaSureDbContext Db, User Admin, User SupplierUser, Product Product, Product Rice)
    {
        public AdminGroupBuyController AdminCtrl() => new(Db, Events) { ControllerContext = As(Admin.Id) };

        public SupplierGroupBuyController SupplierCtrl() => new(Db, Pricing, Events) { ControllerContext = As(SupplierUser.Id) };

        public GroupBuy Deal() => Db.GroupBuys.Include(g => g.Products).Single();

        public GroupBuyProduct Line(Product product) => Deal().Products.Single(p => p.ProductId == product.Id);
    }

    private static ControllerContext As(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")),
        },
    };

    private static string Json(IActionResult result) =>
        System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value,
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    /// <summary>An admin has drafted an offer for maize meal (target 30) with no discount yet.</summary>
    private static async Task<Fixture> Create()
    {
        var options = new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new SpazaSureDbContext(options);
        var supplierRole = new Role { Name = "supplier", IsActive = true };
        var adminRole = new Role { Name = "admin", IsActive = true };
        db.Roles.AddRange(supplierRole, adminRole, new Role { Name = "spaza_owner", IsActive = true });
        var supplierUser = new User { Email = "sup@test.com", PasswordHash = "h", RoleId = supplierRole.Id };
        var admin = new User { Email = "admin@test.com", PasswordHash = "h", RoleId = adminRole.Id };
        var supplier = new Supplier
        {
            UserId = supplierUser.Id, CompanyName = "Bulk Co", ContactPerson = "Sipho", Phone = "0821000000", Email = "sup@test.com",
            City = "Johannesburg", Latitude = -26.2041, Longitude = 28.0473, Status = "verified",
        };
        var maize = new Product { SupplierId = supplier.Id, Name = "Maize Meal 10kg", Sku = "MM10", Price = 200m, StockQty = 500, MinOrderQty = 1, IsApproved = true, IsAvailable = true };
        var rice = new Product { SupplierId = supplier.Id, Name = "Rice 10kg", Sku = "RI10", Price = 300m, StockQty = 500, MinOrderQty = 1, IsApproved = true, IsAvailable = true };
        db.AddRange(supplierUser, admin, supplier, maize, rice,
            new PlatformSetting { Key = PlatformSettingsStore.MinOrderAmountKey, Value = "0" });
        db.SaveChanges();
        var f = new Fixture(db, admin, supplierUser, maize, rice);

        var created = await f.AdminCtrl().Create(new NewGroupBuyRequest("Maize month", null, 10,
            [new NewGroupBuyProduct(maize.Id, 30, 0)], SupplierId: supplier.Id, AreaRadiusKm: 60m, IncludeHeadOffice: true));
        created.Should().BeOfType<OkObjectResult>();
        return f;
    }

    private static Task<IActionResult> Accept(Fixture f, int discount) =>
        f.SupplierCtrl().Respond(f.Deal().Id, new SupplierOfferResponse(true, [new SupplierOfferDiscount(f.Line(f.Product).Id, discount)], null));

    private static EditGroupBuyRequest Edit(Fixture f, params GroupBuyProductEdit[] products) =>
        new("Maize month", null, null, null, null, 60m, [], true, products.ToList());

    [Fact]
    public async Task ANewDeal_IsAnOfferToTheSupplier_NotLive()
    {
        var f = await Create();

        f.Deal().Status.Should().Be("awaiting_supplier");
        var offers = await f.SupplierCtrl().List("offers");
        Json(offers).Should().Contain("Maize month");
    }

    [Fact]
    public async Task TheSupplierSetsItsDiscount_AndAccepts()
    {
        var f = await Create();

        var noDiscount = await f.SupplierCtrl().Respond(f.Deal().Id, new SupplierOfferResponse(true, [], null));
        var accepted = await Accept(f, 12);

        Json(noDiscount).Should().Contain("Enter the discount you offer");
        accepted.Should().BeOfType<OkObjectResult>();
        f.Deal().Status.Should().Be("supplier_accepted");
        f.Line(f.Product).Should().Match<GroupBuyProduct>(p => p.DiscountPct == 12 && p.DiscountPrice == 176m);
    }

    [Fact]
    public async Task TheSupplierCanDecline_ButMustSayWhy()
    {
        var f = await Create();

        var noReason = await f.SupplierCtrl().Respond(f.Deal().Id, new SupplierOfferResponse(false, null, ""));
        var declined = await f.SupplierCtrl().Respond(f.Deal().Id, new SupplierOfferResponse(false, null, "Target too high for one week"));

        Json(noReason).Should().Contain("why you're declining");
        declined.Should().BeOfType<OkObjectResult>();
        f.Deal().Should().Match<GroupBuy>(g => g.Status == "supplier_rejected" && g.SupplierNote == "Target too high for one week");
    }

    [Fact]
    public async Task AfterADecline_TheAdminFixesTheDeal_AndResendsIt()
    {
        var f = await Create();
        await f.SupplierCtrl().Respond(f.Deal().Id, new SupplierOfferResponse(false, null, "Target too high"));

        var edited = await f.AdminCtrl().Edit(f.Deal().Id, Edit(f, new GroupBuyProductEdit(f.Product.Id, 15, 0)));
        var resent = await f.AdminCtrl().SendToSupplier(f.Deal().Id);

        edited.Should().BeOfType<OkObjectResult>();
        resent.Should().BeOfType<OkObjectResult>();
        f.Deal().Should().Match<GroupBuy>(g => g.Status == "awaiting_supplier" && g.SupplierNote == null);
        f.Line(f.Product).TargetQty.Should().Be(15);
    }

    [Fact]
    public async Task Publishing_NeedsADiscount_AndStartsTheCountdown()
    {
        var f = await Create();

        var tooEarly = await f.AdminCtrl().Publish(f.Deal().Id); // the supplier hasn't set a discount
        await Accept(f, 10);
        var published = await f.AdminCtrl().Publish(f.Deal().Id);

        Json(tooEarly).Should().Contain("has no discount yet");
        published.Should().BeOfType<OkObjectResult>();
        var deal = f.Deal();
        deal.Status.Should().Be("active");
        deal.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(10), TimeSpan.FromMinutes(1));
        deal.Products.Single().Status.Should().Be("active");
    }

    [Fact]
    public async Task TheSupplier_NeverSeesTheShopPrice()
    {
        var f = await Create();
        await Accept(f, 10); // R180 to the supplier
        await f.AdminCtrl().Publish(f.Deal().Id);
        var shopPrice = ShopPricing.ShopPrice(180m, ShopPricing.Markup(await PlatformSettingsStore.LoadAsync(f.Db)));

        var seen = Json(await f.SupplierCtrl().Get(f.Deal().Id));

        shopPrice.Should().BeGreaterThan(180m);
        seen.Should().Contain("180").And.NotContain(shopPrice.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task TheSupplier_NeverGetsTheAdvert_PosterOrShopText()
    {
        var f = await Create();
        var deal = f.Deal();
        deal.BannerImageUrl = "/uploads/group-buy-banners/maize-poster.jpg";
        deal.Description = "Save big on maize this month!";
        f.Db.SaveChanges();

        var offer = Json(await f.SupplierCtrl().Get(deal.Id));
        var list = Json(await f.SupplierCtrl().List("all"));
        var adminView = Json(await f.AdminCtrl().Get(deal.Id));

        offer.Should().NotContain("maize-poster").And.NotContain("Save big");
        list.Should().NotContain("maize-poster").And.NotContain("Save big");
        adminView.Should().Contain("maize-poster"); // admins still see their advert
    }

    [Fact]
    public async Task AdminsCanEditALiveDeal_AddingProducts_AndMovingTheEndDate()
    {
        var f = await Create();
        await Accept(f, 10);
        await f.AdminCtrl().Publish(f.Deal().Id);
        var newEnd = DateTime.UtcNow.AddDays(20);

        var edited = await f.AdminCtrl().Edit(f.Deal().Id, Edit(f,
            new GroupBuyProductEdit(f.Product.Id, 40, 15),
            new GroupBuyProductEdit(f.Rice.Id, 20, 8)) with { ExpiresAt = newEnd });

        edited.Should().BeOfType<OkObjectResult>();
        var deal = f.Deal();
        deal.ExpiresAt.Should().Be(newEnd);
        deal.Products.Should().HaveCount(2);
        f.Line(f.Product).Should().Match<GroupBuyProduct>(p => p.TargetQty == 40 && p.DiscountPct == 15 && p.Status == "active");
        f.Line(f.Rice).Status.Should().Be("active");
    }

    [Fact]
    public async Task OnALiveDeal_ProductsShopsJoined_CantBeRemoved_AndPaidShopsKeepTheirPrice()
    {
        var f = await Create();
        await Accept(f, 10);
        await f.AdminCtrl().Publish(f.Deal().Id);
        var line = f.Line(f.Product);
        var shop = new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Spaza", OwnerName = "O", Phone = "0820000001", Address = "1 Rd" };
        var participant = new GroupBuyParticipant
        {
            GroupBuyId = f.Deal().Id, ShopId = shop.Id, Status = "joined", PaymentStatus = "paid", PaymentMethod = "wallet",
            PaymentId = Guid.NewGuid(), AmountPaid = 2000m,
        };
        f.Db.AddRange(shop, participant, new GroupBuyParticipantItem
        {
            ParticipantId = participant.Id, GroupBuyProductId = line.Id, Quantity = 10, ShopUnitPrice = 189m, Status = "joined",
        });
        f.Db.SaveChanges();

        var removal = await f.AdminCtrl().Edit(f.Deal().Id, Edit(f, new GroupBuyProductEdit(f.Rice.Id, 20, 8)));
        var newDiscount = await f.AdminCtrl().Edit(f.Deal().Id, Edit(f, new GroupBuyProductEdit(f.Product.Id, 30, 20)));

        Json(removal).Should().Contain("can't be removed");
        newDiscount.Should().BeOfType<OkObjectResult>();
        f.Line(f.Product).DiscountPct.Should().Be(20);
        f.Db.GroupBuyParticipantItems.Single().ShopUnitPrice.Should().Be(189m); // what the shop paid
    }
}
