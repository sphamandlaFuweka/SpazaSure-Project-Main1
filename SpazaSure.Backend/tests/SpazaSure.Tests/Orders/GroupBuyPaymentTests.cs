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
/// Shops pay when they join a group buy, at least 10 of a product and no more than it still needs.
/// SpazaSure holds the money until the supplier accepts; what never reaches its target goes back
/// to the shop's wallet. Payment is final.
/// </summary>
public class GroupBuyPaymentTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");

    private sealed record Fixture(SpazaSureDbContext Db, SpazaShop Shop, Supplier Supplier, User SupplierUser, GroupBuy GroupBuy, GroupBuyProduct Product)
    {
        public ShopGroupBuyController ShopController(SpazaShop? shop = null) =>
            new(Db, NullLogger<ShopGroupBuyController>.Instance, Pricing,
                new OrderPaystackPaymentService(new HttpClient(), new ConfigurationBuilder().Build()), Events)
            { ControllerContext = As((shop ?? Shop).UserId) };

        public SupplierGroupBuyController SupplierController() =>
            new(Db, Pricing, Events) { ControllerContext = As(SupplierUser.Id) };

        public Task<decimal> Wallet(SpazaShop? shop = null) => ShopWallet.BalanceAsync(Db, (shop ?? Shop).Id);

        public SpazaShop AddShop(string name, decimal walletBalance)
        {
            var user = new User { Phone = "0820000" + Db.Users.Count(), PasswordHash = "h", RoleId = Db.Roles.First(r => r.Name == "spaza_owner").Id };
            var shop = new SpazaShop
            {
                UserId = user.Id, ShopName = name, OwnerName = "Owner", Phone = user.Phone, Address = "5 Test St, Soweto",
                Latitude = -26.2485, Longitude = 27.8540, OnboardingFeePaid = true,
            };
            Db.AddRange(user, shop);
            Db.ShopWalletLedgerEntries.Add(new ShopWalletLedgerEntry
            {
                ShopId = shop.Id, Amount = walletBalance, Type = ShopWallet.Entry.TopUp, SourceId = Guid.NewGuid(), Description = "Top-up",
            });
            Db.SaveChanges();
            return shop;
        }
    }

    private static readonly DeliveryPricingService Pricing = new(Options.Create(new DeliveryPricingOptions()));

    private static ControllerContext As(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")),
        },
    };

    private static Fixture Create(int targetQty = 30, decimal walletBalance = 10_000m)
    {
        var options = new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new SpazaSureDbContext(options);
        var supplierRole = new Role { Name = "supplier", IsActive = true };
        db.Roles.AddRange(supplierRole, new Role { Name = "spaza_owner", IsActive = true });
        var supplierUser = new User { Email = "sup@test.com", PasswordHash = "h", RoleId = supplierRole.Id };
        var supplier = new Supplier
        {
            UserId = supplierUser.Id, CompanyName = "Bulk Co", ContactPerson = "Sipho", Phone = "0821000000", Email = "sup@test.com",
            Latitude = -26.2041, Longitude = 28.0473, CommissionRate = 5m,
        };
        var product = new Product
        {
            SupplierId = supplier.Id, Name = "Maize Meal 10kg", Sku = "MM10", Price = 100m,
            StockQty = 500, MinOrderQty = 1, IsApproved = true, IsAvailable = true,
        };
        var groupBuy = new GroupBuy
        {
            Title = "Maize month", SupplierId = supplier.Id, ProductId = product.Id, Status = "active",
            ExpiresAt = DateTime.UtcNow.AddDays(5), CreatedByRole = "admin",
            OriginalPrice = 100m, DiscountPct = 10, DiscountPrice = 90m, TargetQty = targetQty,
            AreaLatitude = -26.2041, AreaLongitude = 28.0473, AreaRadiusKm = 100m, AreaCity = "Johannesburg",
        };
        var gbProduct = new GroupBuyProduct
        {
            GroupBuyId = groupBuy.Id, ProductId = product.Id, OriginalPrice = 100m, DiscountPct = 10, DiscountPrice = 90m,
            TargetQty = targetQty, Status = "active",
        };
        db.AddRange(supplierUser, supplier, product, groupBuy, gbProduct);
        // These tests are about other rules; the minimum order has its own tests
        db.PlatformSettings.Add(new PlatformSetting { Key = PlatformSettingsStore.MinOrderAmountKey, Value = "0" });
        db.SaveChanges();
        var fixture = new Fixture(db, null!, supplier, supplierUser, groupBuy, gbProduct);
        return fixture with { Shop = fixture.AddShop("Corner Spaza", walletBalance) };
    }

    private static JoinGroupBuyRequest Join(Fixture f, int quantity, string? method = "wallet") => new()
    {
        Items = [new JoinGroupBuyItemRequest { GroupBuyProductId = f.Product.Id, Quantity = quantity }],
        PaymentMethod = method,
    };

    private static string Message(IActionResult result) =>
        System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value);

    [Fact]
    public async Task JoiningWithTheWallet_PaysForItemsAndDelivery_AndCountsTowardsTheTarget()
    {
        var f = Create();

        var result = await f.ShopController().Join(f.GroupBuy.Id, Join(f, 12));

        result.Should().BeOfType<OkObjectResult>();
        var participant = f.Db.GroupBuyParticipants.Include(p => p.Items).Single();
        participant.Status.Should().Be("joined");
        participant.PaymentStatus.Should().Be("paid");
        participant.DeliveryFee.Should().BeGreaterThan(0m);
        var itemPrice = participant.Items.Single().ShopUnitPrice;
        itemPrice.Should().BeGreaterThanOrEqualTo(90m); // the group price, plus SpazaSure's fee
        participant.AmountPaid.Should().Be(itemPrice * 12 + participant.DeliveryFee);
        (await f.Wallet()).Should().Be(10_000m - participant.AmountPaid);
        f.Db.GroupBuyProducts.Single().CurrentQty.Should().Be(12);
    }

    [Theory]
    [InlineData(9, "at least 10 items in total")] // fewer than 10
    [InlineData(31, "Only 30 more")]               // more than the target still needs
    public async Task Quantities_MustBeAtLeastTen_AndNoMoreThanStillNeeded(int quantity, string expected)
    {
        var f = Create(targetQty: 30);

        var result = await f.ShopController().Join(f.GroupBuy.Id, Join(f, quantity));

        result.Should().BeOfType<BadRequestObjectResult>();
        Message(result).Should().Contain(expected);
        f.Db.GroupBuyParticipants.Should().BeEmpty();
        (await f.Wallet()).Should().Be(10_000m);
    }

    [Fact]
    public async Task TheTenItems_CanBeAMixOfProducts()
    {
        var f = Create();
        var beans = AddProduct(f, "Sugar Beans 1kg");
        JoinGroupBuyRequest Mix(int maize, int sugarBeans) => new()
        {
            Items =
            [
                new JoinGroupBuyItemRequest { GroupBuyProductId = f.Product.Id, Quantity = maize },
                new JoinGroupBuyItemRequest { GroupBuyProductId = beans.Id, Quantity = sugarBeans },
            ],
            PaymentMethod = "wallet",
        };

        var tooFew = await f.ShopController().Join(f.GroupBuy.Id, Mix(3, 6));
        var mixed = await f.ShopController().Join(f.GroupBuy.Id, Mix(3, 7));

        Message(tooFew).Should().Contain("at least 10 items in total");
        mixed.Should().BeOfType<OkObjectResult>();
        f.Db.GroupBuyProducts.Single(p => p.Id == f.Product.Id).CurrentQty.Should().Be(3);
        f.Db.GroupBuyProducts.Single(p => p.Id == beans.Id).CurrentQty.Should().Be(7);
    }

    [Fact]
    public async Task WhenFewerThanTenAreLeft_TheLastShopTakesExactlyWhatIsLeft()
    {
        var f = Create(targetQty: 26);
        (await f.ShopController().Join(f.GroupBuy.Id, Join(f, 20))).Should().BeOfType<OkObjectResult>();
        var second = f.AddShop("Second Spaza", 10_000m);

        var tooFew = await f.ShopController(second).Join(f.GroupBuy.Id, Join(f, 5));
        var exactlyLeft = await f.ShopController(second).Join(f.GroupBuy.Id, Join(f, 6));

        Message(tooFew).Should().Contain("take all 6");
        exactlyLeft.Should().BeOfType<OkObjectResult>();
        f.Db.GroupBuyProducts.Single().Status.Should().Be("qualified");
    }

    [Fact]
    public async Task GroupBuys_HaveTheMinimumOrderToo_UnlessTheShopCompletesTheTarget()
    {
        var f = Create(targetQty: 15);
        f.Db.PlatformSettings.Single(s => s.Key == PlatformSettingsStore.MinOrderAmountKey).Value = "2000";
        f.Db.SaveChanges();

        var tooSmall = await f.ShopController().Join(f.GroupBuy.Id, Join(f, 12));          // about R1,100
        var completesTarget = await f.ShopController().Join(f.GroupBuy.Id, Join(f, 15));   // about R1,400, but everything left

        Message(tooSmall).Should().Contain("minimum order from Bulk Co");
        completesTarget.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task BothMinimums_AreMetTogether_TenItemsWorthAtLeastR2000()
    {
        var f = Create(targetQty: 100);
        f.Db.PlatformSettings.Single(s => s.Key == PlatformSettingsStore.MinOrderAmountKey).Value = "2000";
        f.Db.SaveChanges();
        var oil = AddProduct(f, "Cooking Oil 20L", price: 250m); // about R236 each to shops
        JoinGroupBuyRequest Buy(GroupBuyProduct product, int quantity) => new()
        {
            Items = [new JoinGroupBuyItemRequest { GroupBuyProductId = product.Id, Quantity = quantity }],
            PaymentMethod = "wallet",
        };

        var tenCheap = await f.ShopController().Join(f.GroupBuy.Id, Buy(f.Product, 10)); // 10 items, about R945
        var tenWorthEnough = await f.ShopController().Join(f.GroupBuy.Id, Buy(oil, 10)); // 10 items, about R2,360

        Message(tenCheap).Should().Contain("minimum order");
        tenWorthEnough.Should().BeOfType<OkObjectResult>(); // no need to spend more than R2,000
    }

    [Fact]
    public async Task ATargetThatIsReached_TakesNoMoreShops()
    {
        var f = Create(targetQty: 10);
        await f.ShopController().Join(f.GroupBuy.Id, Join(f, 10));
        var late = f.AddShop("Late Spaza", 10_000m);

        var result = await f.ShopController(late).Join(f.GroupBuy.Id, Join(f, 10));

        Message(result).Should().Contain("already reached its target");
    }

    [Fact]
    public async Task NotEnoughInTheWallet_IsRefused_AndNothingIsTaken()
    {
        var f = Create(walletBalance: 50m);

        var result = await f.ShopController().Join(f.GroupBuy.Id, Join(f, 10));

        Message(result).Should().Contain("wallet balance");
        (await f.Wallet()).Should().Be(50m);
        f.Db.GroupBuyProducts.Single().CurrentQty.Should().Be(0);
    }

    [Fact]
    public async Task OlderAppsThatDontPay_AreAskedToUpdate()
    {
        var f = Create();

        var result = await f.ShopController().Join(f.GroupBuy.Id, Join(f, 10, method: null));

        Message(result).Should().Contain("update the SpazaSure app");
    }

    [Fact]
    public async Task APaidShop_CannotLeave()
    {
        var f = Create();
        await f.ShopController().Join(f.GroupBuy.Id, Join(f, 10));
        var balance = await f.Wallet();

        var result = await f.ShopController().Leave(f.GroupBuy.Id);

        Message(result).Should().Contain("so you can");
        (await f.Wallet()).Should().Be(balance);
    }

    [Fact]
    public async Task AtItsEndDate_AGroupBuyGetsOneMoreWeek()
    {
        var f = Create(targetQty: 50);
        await f.ShopController().Join(f.GroupBuy.Id, Join(f, 10));
        var paid = await f.Wallet();
        var now = DateTime.UtcNow;
        f.Db.GroupBuys.Single().ExpiresAt = now.AddMinutes(-1);
        f.Db.SaveChanges();

        await GroupBuyExpiry.RunOnceAsync(f.Db, Events, now);

        var groupBuy = f.Db.GroupBuys.Single();
        groupBuy.Status.Should().Be("active");
        groupBuy.ExtensionCount.Should().Be(1);
        groupBuy.ExpiresAt.Should().BeCloseTo(now.AddDays(7), TimeSpan.FromMinutes(2));
        (await f.Wallet()).Should().Be(paid); // the money stays held
    }

    [Fact]
    public async Task MembersAreReminded_TwoDaysBeforeTheEnd_OnceForEachEndDate()
    {
        var f = Create(targetQty: 50);
        await f.ShopController().Join(f.GroupBuy.Id, Join(f, 10));
        var now = DateTime.UtcNow;
        f.Db.GroupBuys.Single().ExpiresAt = now.AddDays(3);
        f.Db.SaveChanges();

        await GroupBuyExpiry.RunOnceAsync(f.Db, Events, now);
        f.Db.GroupBuys.Single().EndingReminderSentAt.Should().BeNull(); // 3 days out: too early

        await GroupBuyExpiry.RunOnceAsync(f.Db, Events, now.AddDays(1).AddMinutes(1));
        var remindedAt = f.Db.GroupBuys.Single().EndingReminderSentAt;
        remindedAt.Should().NotBeNull();

        await GroupBuyExpiry.RunOnceAsync(f.Db, Events, now.AddDays(2));
        f.Db.GroupBuys.Single().EndingReminderSentAt.Should().Be(remindedAt); // not sent twice

        // Extended at the end date: a new reminder is due before the new end
        await GroupBuyExpiry.RunOnceAsync(f.Db, Events, now.AddDays(3).AddMinutes(1));
        f.Db.GroupBuys.Single().EndingReminderSentAt.Should().BeNull();
    }

    [Fact]
    public async Task AGroupBuyThatExpiresAfterItsExtraWeek_GivesTheShopsTheirMoneyBack()
    {
        var f = Create(targetQty: 50);
        await f.ShopController().Join(f.GroupBuy.Id, Join(f, 10));
        var groupBuy = f.Db.GroupBuys.Single();
        groupBuy.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        groupBuy.ExtensionCount = 1;
        f.Db.SaveChanges();

        var closed = await GroupBuyExpiry.RunOnceAsync(f.Db, Events, DateTime.UtcNow);

        closed.Should().Be(1);
        (await f.Wallet()).Should().Be(10_000m);
        f.Db.GroupBuys.Single().Status.Should().Be("expired");
        f.Db.GroupBuyParticipants.Single().Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task WhenTheSupplierAccepts_TheHeldMoneyPaysTheOrder()
    {
        var f = Create(targetQty: 10);
        await f.ShopController().Join(f.GroupBuy.Id, Join(f, 10));
        var paid = f.Db.GroupBuyParticipants.Single().AmountPaid;

        var result = await f.SupplierController().Accept(f.GroupBuy.Id);

        result.Should().BeOfType<OkObjectResult>();
        var order = f.Db.Orders.Single();
        order.PaymentMethod.Should().Be(ShopWallet.PaymentMethod);
        order.PaymentStatus.Should().Be("paid");
        order.TotalAmount.Should().Be(paid);
        order.Subtotal.Should().Be(900m); // the supplier gets its group price
        // Paid once, when joining: accepting moves the money, it doesn't charge again
        (await f.Wallet()).Should().Be(10_000m - paid);

        // An order cancelled later is refunded to the wallet like any wallet order
        order.Status = "cancelled";
        await ShopWallet.SyncOrderAsync(f.Db, order);
        f.Db.SaveChanges();
        (await f.Wallet()).Should().Be(10_000m);
    }

    [Fact]
    public async Task ACardPayment_JoinsTheShop_WhenPaystackConfirmsIt()
    {
        var f = Create();
        var participant = Reserve(f, f.Shop, 10);

        var result = await GroupBuyPayments.ConfirmCardAsync(f.Db, f.GroupBuy, participant, GroupBuyPayments.ToCents(participant.AmountPaid), DateTime.UtcNow);
        f.Db.SaveChanges();

        result.Should().Be(GroupBuyPayments.CardResult.Joined);
        participant.Status.Should().Be("joined");
        f.Db.GroupBuyProducts.Single().CurrentQty.Should().Be(10);
        (await f.Wallet()).Should().Be(10_000m); // the card paid, the wallet is untouched
    }

    [Fact]
    public async Task ACardPayment_ForACampaignThatClosedMeanwhile_GoesToTheWallet()
    {
        var f = Create();
        var participant = Reserve(f, f.Shop, 10);
        f.GroupBuy.Status = "expired";

        var result = await GroupBuyPayments.ConfirmCardAsync(f.Db, f.GroupBuy, participant, GroupBuyPayments.ToCents(participant.AmountPaid), DateTime.UtcNow);
        f.Db.SaveChanges();

        result.Should().Be(GroupBuyPayments.CardResult.RefundedToWallet);
        (await f.Wallet()).Should().Be(10_000m + participant.AmountPaid);
    }

    [Fact]
    public async Task AnOpenCardCheckout_HoldsItsUnitsFromOtherShops()
    {
        var f = Create(targetQty: 20);
        Reserve(f, f.Shop, 15);
        var other = f.AddShop("Other Spaza", 10_000m);

        var result = await f.ShopController(other).Join(f.GroupBuy.Id, Join(f, 10));

        Message(result).Should().Contain("Only 5 more");
    }

    private static GroupBuyProduct AddProduct(Fixture f, string name, decimal price = 50m)
    {
        var product = new Product
        {
            SupplierId = f.Supplier.Id, Name = name, Sku = name[..4], Price = price,
            StockQty = 500, MinOrderQty = 1, IsApproved = true, IsAvailable = true,
        };
        var gbProduct = new GroupBuyProduct
        {
            GroupBuyId = f.GroupBuy.Id, ProductId = product.Id, OriginalPrice = price, DiscountPct = 10, DiscountPrice = price * 0.9m,
            TargetQty = 30, Status = "active",
        };
        f.Db.AddRange(product, gbProduct);
        f.Db.SaveChanges();
        return gbProduct;
    }

    /// <summary>A card checkout the shop hasn't paid yet, as Join leaves it.</summary>
    private static GroupBuyParticipant Reserve(Fixture f, SpazaShop shop, int quantity)
    {
        var participant = new GroupBuyParticipant
        {
            GroupBuyId = f.GroupBuy.Id, ShopId = shop.Id, Status = "pending_payment", PaymentStatus = "pending",
            PaymentMethod = GroupBuyPayments.Card, PaymentId = Guid.NewGuid(), DeliveryFee = 50m,
            AmountPaid = 95m * quantity + 50m, ReservedUntil = DateTime.UtcNow.AddHours(1),
        };
        participant.PaymentReference = GroupBuyPayments.NewReference(participant.Id);
        var item = new GroupBuyParticipantItem
        {
            ParticipantId = participant.Id, GroupBuyProductId = f.Product.Id, Quantity = quantity, ShopUnitPrice = 95m, Status = "reserved",
        };
        f.Db.AddRange(participant, item);
        f.Db.SaveChanges();
        return participant;
    }
}
