using System.Security.Claims;
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
using SpazaSure.Shared.Helpers;
using SpazaSure.UserService.Controllers;
using SpazaSure.UserService.Services;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>Direct-deposit top-ups and paying for orders from the shop wallet.</summary>
public class WalletTests
{
    private sealed record Fixture(
        SpazaSureDbContext Db, SpazaShop Shop, Product Product,
        ShopWalletController Wallet, ShopOrdersController Checkout, AdminController Admin);

    private static Fixture Create()
    {
        // Checkout runs in a transaction; the in-memory provider just ignores it
        var options = new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new SpazaSureDbContext(options);
        var ownerRole = new Role { Name = "spaza_owner", IsActive = true };
        var supplierRole = new Role { Name = "supplier", IsActive = true };
        db.Roles.AddRange(ownerRole, supplierRole);

        var shopUser = new User { Email = "wallet@test.com", Phone = "0820007000", PasswordHash = "h", RoleId = ownerRole.Id };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Wallet Spaza", OwnerName = "Thandi", Phone = "0820007000", Address = "7 Test St", Email = "wallet@test.com" };
        var supplierUser = new User { Email = "sup@test.com", Phone = "0820008000", PasswordHash = "h", RoleId = supplierRole.Id };
        var supplier = new Supplier { UserId = supplierUser.Id, CompanyName = "Bulk Co", ContactPerson = "Sipho", Phone = "0820008000", Email = "sup@test.com", CommissionRate = 5m, Status = "verified" };
        var product = new Product { SupplierId = supplier.Id, Name = "Maize meal 10kg", Sku = "MM10", Price = 100m, StockQty = 50, IsAvailable = true, IsApproved = true };
        db.AddRange(shopUser, shop, supplierUser, supplier, product);
        // These tests are about other rules; the minimum order has its own tests
        db.PlatformSettings.Add(new PlatformSetting { Key = PlatformSettingsStore.MinOrderAmountKey, Value = "0" });
        db.SaveChanges();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, shopUser.Id.ToString())], "Test"))
        };
        var config = new ConfigurationBuilder().Build();
        var events = new EventPublisher("__nonexistent__", "x", "x");
        return new Fixture(db, shop, product,
            new ShopWalletController(db, new PaystackPaymentService(new HttpClient(), config), events) { ControllerContext = new ControllerContext { HttpContext = http } },
            new ShopOrdersController(db, new OrderPaystackPaymentService(new HttpClient(), config), events,
                new SpazaSure.OrderService.Services.PaystackSplitPaymentService(new HttpClient(), config))
            { ControllerContext = new ControllerContext { HttpContext = http } },
            new AdminController(db, null!, events));
    }

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    private static PlaceOrderRequest Order(Product product, int quantity) =>
        new("pickup", ShopWallet.PaymentMethod, null, [new PlaceOrderItemRequest(product.Id, quantity, product.Price)]);

    private static async Task<decimal> Balance(Fixture f) => Data(await f.Wallet.GetWallet()).GetProperty("balance").GetDecimal();

    [Fact]
    public async Task DirectDeposit_ReturnsBankDetailsAndReference_AndCreditsOnlyAfterApproval()
    {
        var f = Create();

        var data = Data(await f.Wallet.RequestTopUp(new WalletTopUpRequest(500m, "eft", null, null)));

        var reference = data.GetProperty("reference").GetString();
        reference.Should().MatchRegex("^SS[0-9A-F]{8}$");
        var account = data.GetProperty("depositAccount");
        account.GetProperty("bankName").GetString().Should().Be("Capitec Business");
        account.GetProperty("accountNumber").GetString().Should().Be("10551015752");
        account.GetProperty("accountHolder").GetString().Should().Be("GEOTX PTY LTD");

        (await Balance(f)).Should().Be(0m); // not credited until SpazaSure sees the money
        Data(await f.Wallet.GetWallet()).GetProperty("pendingDeposits").GetDecimal().Should().Be(500m);

        var id = data.GetProperty("id").GetGuid();
        (await f.Admin.ApproveWalletTopUp(id)).Should().BeOfType<OkObjectResult>();
        (await Balance(f)).Should().Be(500m);
    }

    [Fact]
    public async Task DirectDeposit_RejectsAmountsOutsideLimits()
    {
        var f = Create();
        (await f.Wallet.RequestTopUp(new WalletTopUpRequest(10m, "eft", null, null))).Should().BeOfType<BadRequestObjectResult>();
        (await f.Wallet.RequestTopUp(new WalletTopUpRequest(200_000m, "eft", null, null))).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PaystackTopUps_CannotBeApprovedByHand()
    {
        var f = Create();
        var topUp = new ShopWalletTransaction { ShopId = f.Shop.Id, Amount = 100m, Method = "paystack", Status = "pending" };
        f.Db.ShopWalletTransactions.Add(topUp);
        await f.Db.SaveChangesAsync();

        (await f.Admin.ApproveWalletTopUp(topUp.Id)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OrdersBelowTheMinimum_AreRefused_PerSupplier_NotCountingDelivery()
    {
        var f = Create();
        f.Db.PlatformSettings.Single(s => s.Key == PlatformSettingsStore.MinOrderAmountKey).Value = "2000";
        f.Db.SaveChanges();
        await AddApprovedTopUp(f, 5000m);

        var tooSmall = await f.Checkout.PlaceOrder(Order(f.Product, 3));   // R315 in products
        var bigEnough = await f.Checkout.PlaceOrder(Order(f.Product, 20)); // R2,100 in products

        tooSmall.Should().BeOfType<BadRequestObjectResult>();
        System.Text.Json.JsonSerializer.Serialize(((ObjectResult)tooSmall).Value).Should().Contain("minimum order from Bulk Co");
        bigEnough.Should().BeOfType<OkObjectResult>();
        (await f.Db.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task WalletCheckout_PaysImmediately_AndDeductsBalance()
    {
        var f = Create();
        await AddApprovedTopUp(f, 1000m);

        var result = await f.Checkout.PlaceOrder(Order(f.Product, 3)); // R300 + 5% commission, pickup = R315

        var data = Data(result);
        data.GetProperty("checkoutUrl").ValueKind.Should().Be(JsonValueKind.Null);
        var order = await f.Db.Orders.SingleAsync();
        order.PaymentMethod.Should().Be(ShopWallet.PaymentMethod);
        order.PaymentStatus.Should().Be("paid");
        order.TotalAmount.Should().Be(315m);
        (await Balance(f)).Should().Be(685m);
    }

    [Fact]
    public async Task PlatformFee_IsAFlat5Percent_BuiltIntoShopPrices_AndNeverShownAsAFee()
    {
        var f = Create();
        // A supplier on a cheaper plan: the shop still pays SpazaSure's flat 5%
        (await f.Db.Suppliers.SingleAsync()).CommissionRate = 3m;
        await f.Db.SaveChangesAsync();
        await AddApprovedTopUp(f, 1000m);

        await f.Checkout.PlaceOrder(Order(f.Product, 3));

        var order = await f.Db.Orders.Include(o => o.Items).SingleAsync();
        order.Subtotal.Should().Be(300m);            // what the supplier is paid
        order.PlatformCommission.Should().Be(15m);   // SpazaSure's 5%
        order.TotalAmount.Should().Be(315m);
        var item = order.Items.Single();
        item.UnitPrice.Should().Be(100m);
        item.ShopUnitPrice.Should().Be(105m);
        item.ShopLineTotal.Should().Be(315m);

        // The shop's view of its order: prices include the fee and there is no fee line
        var shopView = Data(await f.Checkout.Get(order.Id));
        shopView.GetProperty("subtotal").GetDecimal().Should().Be(315m);
        shopView.TryGetProperty("platformCommission", out _).Should().BeFalse();
        var line = shopView.GetProperty("items")[0];
        line.GetProperty("unitPrice").GetDecimal().Should().Be(105m);
        line.GetProperty("lineTotal").GetDecimal().Should().Be(315m);
    }

    [Theory]
    [InlineData(100, 1, 105, 5)]
    [InlineData(33.33, 3, 105.00, 5.01)]   // 33.33 x 1.05 = 34.9965 -> 35.00 a unit
    [InlineData(19.99, 7, 146.93, 7.00)]
    public void ShopPricing_ReceiptsAddUpToTheCent(decimal price, int quantity, decimal shopLine, decimal fee)
    {
        var (unit, line) = ShopPricing.Line(price, quantity, 0.05m);
        line.Should().Be(unit * quantity);
        line.Should().Be(shopLine);
        (line - price * quantity).Should().Be(fee);
    }

    [Fact]
    public async Task WalletCheckout_FailsWhenBalanceIsTooLow()
    {
        var f = Create();
        await AddApprovedTopUp(f, 100m);

        var result = await f.Checkout.PlaceOrder(Order(f.Product, 3));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CancelledWalletOrder_IsRefundedToTheWallet()
    {
        var f = Create();
        await AddApprovedTopUp(f, 1000m);
        await f.Checkout.PlaceOrder(Order(f.Product, 3));
        (await Balance(f)).Should().Be(685m);

        var order = await f.Db.Orders.SingleAsync();
        var admin = new AdminOrdersController(f.Db, new EventPublisher("__nonexistent__", "x", "x"));
        (await admin.UpdateStatus(order.Id, new UpdateStatusRequest("cancelled", "Out of stock"))).Should().BeOfType<OkObjectResult>();

        (await Balance(f)).Should().Be(1000m);
        var entries = await f.Db.ShopWalletLedgerEntries.OrderBy(e => e.CreatedAt).Select(e => e.Type).ToListAsync();
        entries.Should().BeEquivalentTo(["top_up", "order_payment", "order_refund"]);

        // Cancelling again doesn't refund twice
        await admin.UpdateStatus(order.Id, new UpdateStatusRequest("cancelled", "Again"));
        (await Balance(f)).Should().Be(1000m);
    }

    [Fact]
    public async Task OrdersPaidWithPaystack_DoNotReduceTheWallet()
    {
        var f = Create();
        await AddApprovedTopUp(f, 400m);
        f.Db.Orders.Add(new Order
        {
            ShopId = f.Shop.Id, SupplierId = f.Product.SupplierId, OrderNumber = "SPZ-PAYSTACK", Status = "delivered",
            Subtotal = 900m, TotalAmount = 1000m, PaymentMethod = "paystack", PaymentStatus = "paid",
        });
        await f.Db.SaveChangesAsync();

        (await Balance(f)).Should().Be(400m);
    }

    private static async Task AddApprovedTopUp(Fixture f, decimal amount)
    {
        var topUp = new ShopWalletTransaction { ShopId = f.Shop.Id, Amount = amount, Method = "eft", Status = "approved", ApprovedAt = DateTime.UtcNow };
        f.Db.ShopWalletTransactions.Add(topUp);
        await ShopWallet.SyncTopUpAsync(f.Db, topUp);
        await f.Db.SaveChangesAsync();
    }

    private static WalletWithdrawalRequest Withdraw(decimal amount) =>
        new(amount, "FNB", "Thandi Spaza", "6201 2345 678", "250655");

    [Fact]
    public async Task Withdrawal_TakesMoneyOutImmediately_AndIsPaidByAdmin()
    {
        var f = Create();
        await AddApprovedTopUp(f, 1000m);

        var data = Data(await f.Wallet.RequestWithdrawal(Withdraw(400m)));
        data.GetProperty("reference").GetString().Should().MatchRegex("^WD[0-9A-F]{8}$");
        (await Balance(f)).Should().Be(600m);
        Data(await f.Wallet.GetWallet()).GetProperty("pendingWithdrawals").GetDecimal().Should().Be(400m);

        // The held money can't be spent twice
        (await f.Wallet.RequestWithdrawal(Withdraw(700m))).Should().BeOfType<BadRequestObjectResult>();

        var id = data.GetProperty("id").GetGuid();
        (await f.Admin.MarkWalletWithdrawalPaid(id, new WalletWithdrawalPaidRequest(" "))).Should().BeOfType<BadRequestObjectResult>();
        (await f.Admin.MarkWalletWithdrawalPaid(id, new WalletWithdrawalPaidRequest("FNB-778"))).Should().BeOfType<OkObjectResult>();

        (await Balance(f)).Should().Be(600m); // paying out doesn't touch the wallet again
        (await f.Db.ShopWalletTransactions.SingleAsync(t => t.Id == id)).Status.Should().Be("paid");
        (await f.Admin.MarkWalletWithdrawalPaid(id, new WalletWithdrawalPaidRequest("FNB-778"))).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Withdrawal_ValidatesAmountAndBankDetails()
    {
        var f = Create();
        await AddApprovedTopUp(f, 1000m);

        (await f.Wallet.RequestWithdrawal(Withdraw(10m))).Should().BeOfType<BadRequestObjectResult>();
        (await f.Wallet.RequestWithdrawal(Withdraw(5000m))).Should().BeOfType<BadRequestObjectResult>();
        (await f.Wallet.RequestWithdrawal(Withdraw(100m) with { AccountNumber = "12" })).Should().BeOfType<BadRequestObjectResult>();
        (await f.Wallet.RequestWithdrawal(Withdraw(100m) with { BankName = " " })).Should().BeOfType<BadRequestObjectResult>();
        (await f.Wallet.RequestWithdrawal(Withdraw(100m) with { BranchCode = "12" })).Should().BeOfType<BadRequestObjectResult>();
        (await Balance(f)).Should().Be(1000m);
    }

    [Fact]
    public async Task DeclinedOrCancelledWithdrawal_ReturnsTheMoney_OnlyOnce()
    {
        var f = Create();
        await AddApprovedTopUp(f, 1000m);
        var first = Data(await f.Wallet.RequestWithdrawal(Withdraw(300m))).GetProperty("id").GetGuid();
        var second = Data(await f.Wallet.RequestWithdrawal(Withdraw(200m))).GetProperty("id").GetGuid();
        (await Balance(f)).Should().Be(500m);

        (await f.Admin.RejectWalletWithdrawal(first, new WalletTopUpRejectRequest("Bank details bounced"))).Should().BeOfType<OkObjectResult>();
        (await f.Wallet.CancelWithdrawal(second)).Should().BeOfType<OkObjectResult>();
        (await Balance(f)).Should().Be(1000m);

        (await f.Admin.RejectWalletWithdrawal(first, new WalletTopUpRejectRequest(null))).Should().BeOfType<BadRequestObjectResult>();
        (await f.Wallet.CancelWithdrawal(second)).Should().BeOfType<BadRequestObjectResult>();
        (await Balance(f)).Should().Be(1000m);
    }

    [Fact]
    public async Task WalletActivity_ShowsLedgerWithRunningBalance_AndWithdrawalsAreNotListedAsTopUps()
    {
        var f = Create();
        await AddApprovedTopUp(f, 1000m);
        await f.Checkout.PlaceOrder(Order(f.Product, 3)); // -315
        await f.Wallet.RequestWithdrawal(Withdraw(100m));

        var activity = Data(await f.Wallet.GetWallet()).GetProperty("transactions").EnumerateArray().ToList();
        activity.Should().HaveCount(3);
        activity.Select(a => a.GetProperty("balanceAfter").GetDecimal()).Should().Equal(585m, 685m, 1000m);
        activity.Select(a => a.GetProperty("type").GetString()).Should().Equal("debit", "debit", "credit");

        var topUps = ((OkObjectResult)await f.Admin.GetWalletTopUps("")).Value!;
        JsonSerializer.SerializeToElement(topUps, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data").GetArrayLength().Should().Be(1);
    }
}
