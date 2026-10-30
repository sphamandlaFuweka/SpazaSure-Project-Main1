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
/// A group buy can be open around several of the supplier's branches: shops near any of them can
/// join, each served by its nearest branch, which its stock and delivery come from.
/// </summary>
public class GroupBuyBranchesTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");
    private static readonly DeliveryPricingService Pricing = new(Options.Create(new DeliveryPricingOptions()));

    private sealed record Fixture(
        SpazaSureDbContext Db, Supplier Supplier, User SupplierUser, User Admin, SupplierBranch Durban, Product Product,
        GroupBuyProduct GroupBuyProduct, GroupBuy GroupBuy)
    {
        public SpazaShop AddShop(string name, double latitude, double longitude)
        {
            var user = new User { Phone = "08200" + Db.Users.Count().ToString("D5"), PasswordHash = "h", RoleId = Db.Roles.First(r => r.Name == "spaza_owner").Id };
            var shop = new SpazaShop
            {
                UserId = user.Id, ShopName = name, OwnerName = "Owner", Phone = user.Phone, Address = "1 Main Rd",
                Latitude = latitude, Longitude = longitude, OnboardingFeePaid = true,
            };
            Db.AddRange(user, shop);
            Db.ShopWalletLedgerEntries.Add(new ShopWalletLedgerEntry
            {
                ShopId = shop.Id, Amount = 10_000m, Type = ShopWallet.Entry.TopUp, SourceId = Guid.NewGuid(), Description = "Top-up",
            });
            Db.SaveChanges();
            return shop;
        }

        public Task<IActionResult> Join(SpazaShop shop, int quantity) =>
            new ShopGroupBuyController(Db, NullLogger<ShopGroupBuyController>.Instance, Pricing,
                new OrderPaystackPaymentService(new HttpClient(), new ConfigurationBuilder().Build()), Events)
            { ControllerContext = As(shop.UserId) }
            .Join(GroupBuy.Id, new JoinGroupBuyRequest
            {
                Items = [new JoinGroupBuyItemRequest { GroupBuyProductId = GroupBuyProduct.Id, Quantity = quantity }],
                PaymentMethod = "wallet",
            });
    }

    private static ControllerContext As(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")),
        },
    };

    /// <summary>Head office in Johannesburg and a branch in Durban (holding 15 of the product); the deal is open around both.</summary>
    private static async Task<Fixture> Create(int targetQty = 50)
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
        var durban = new SupplierBranch
        {
            SupplierId = supplier.Id, Name = "Durban warehouse", Address = "1 Quay Rd", City = "Durban",
            Latitude = -29.8587, Longitude = 31.0218,
        };
        var product = new Product
        {
            SupplierId = supplier.Id, Name = "Maize Meal 10kg", Sku = "MM10", Price = 250m,
            StockQty = 500, MinOrderQty = 1, IsApproved = true, IsAvailable = true,
        };
        db.AddRange(supplierUser, admin, supplier, durban, product,
            new ProductBranchStock { ProductId = product.Id, SupplierBranchId = durban.Id, StockQty = 15 },
            new PlatformSetting { Key = PlatformSettingsStore.MinOrderAmountKey, Value = "0" });
        db.SaveChanges();

        var created = await new AdminGroupBuyController(db, Events) { ControllerContext = As(admin.Id) }
            .Create(new NewGroupBuyRequest("Maize month", null, 7, [new NewGroupBuyProduct(product.Id, targetQty, 10)],
                SupplierId: supplier.Id, AreaRadiusKm: 60m, SupplierBranchIds: [durban.Id], IncludeHeadOffice: true));
        created.Should().BeOfType<OkObjectResult>();
        var groupBuy = db.GroupBuys.Include(g => g.Branches).Include(g => g.Products).Single();
        return new Fixture(db, supplier, supplierUser, admin, durban, product, groupBuy.Products.Single(), groupBuy);
    }

    [Fact]
    public async Task TheDeal_IsOpenAroundEveryChosenBranch()
    {
        var f = await Create();
        var soweto = f.AddShop("Soweto Spaza", -26.2485, 27.8540);
        var umlazi = f.AddShop("Umlazi Spaza", -29.9700, 30.8800);
        var capeTown = f.AddShop("Cape Town Spaza", -33.9249, 18.4241);

        f.GroupBuy.Branches.Should().HaveCount(2);
        GroupBuyArea.Describe(f.GroupBuy).Should().Be("shops within 60 km of Johannesburg or Durban");
        GroupBuyArea.Contains(f.GroupBuy, soweto, 0m).Should().BeTrue();
        GroupBuyArea.Contains(f.GroupBuy, umlazi, 0m).Should().BeTrue();
        GroupBuyArea.Contains(f.GroupBuy, capeTown, 0m).Should().BeFalse();
    }

    [Fact]
    public async Task EachShop_IsServedByItsNearestBranch_AndItsStock()
    {
        var f = await Create();
        var umlazi = f.AddShop("Umlazi Spaza", -29.9700, 30.8800);
        var chatsworth = f.AddShop("Chatsworth Spaza", -29.9100, 30.8800);
        var soweto = f.AddShop("Soweto Spaza", -26.2485, 27.8540);

        var first = await f.Join(umlazi, 12);       // Durban has 15
        var tooMany = await f.Join(chatsworth, 10); // only 3 left in Durban
        var fromJohannesburg = await f.Join(soweto, 10);

        first.Should().BeOfType<OkObjectResult>();
        System.Text.Json.JsonSerializer.Serialize(((ObjectResult)tooMany).Value).Should().Contain("Only 3 more");
        fromJohannesburg.Should().BeOfType<OkObjectResult>(); // the head office has plenty
        var durbanPlace = f.GroupBuy.Branches.Single(b => b.SupplierBranchId == f.Durban.Id);
        var umlaziJoin = f.Db.GroupBuyParticipants.Single(p => p.ShopId == umlazi.Id);
        umlaziJoin.GroupBuyBranchId.Should().Be(durbanPlace.Id);
        umlaziJoin.DeliveryFee.Should().BeLessThan(500m); // about 18 km from Durban, not 500 km from Johannesburg
    }

    [Fact]
    public async Task Accepting_SendsEachOrderFromTheBranchServingTheShop()
    {
        var f = await Create(targetQty: 22);
        var umlazi = f.AddShop("Umlazi Spaza", -29.9700, 30.8800);
        var soweto = f.AddShop("Soweto Spaza", -26.2485, 27.8540);
        await f.Join(umlazi, 12);
        await f.Join(soweto, 10);

        var result = await new SupplierGroupBuyController(f.Db, Pricing, Events) { ControllerContext = As(f.SupplierUser.Id) }
            .Accept(f.GroupBuy.Id);

        result.Should().BeOfType<OkObjectResult>();
        f.Db.Orders.Single(o => o.ShopId == umlazi.Id).SupplierBranchId.Should().Be(f.Durban.Id);
        f.Db.Orders.Single(o => o.ShopId == soweto.Id).SupplierBranchId.Should().BeNull(); // head office
        var stock = f.Db.Products.Include(p => p.BranchStock).Single();
        stock.BranchStock.Single().StockQty.Should().Be(3);   // 15 - 12 from Durban
        BranchStock.HeadOfficeQty(stock).Should().Be(475);    // 485 - 10 from the head office
    }
}
