using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Payments;

public class AdminFinanceTests
{
    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    private static (AdminFinanceController Finance, SpazaSureDbContext Db, Supplier Supplier, SpazaShop Shop) Create()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "supplier");
        var supplierUser = new User { Email = "fin@test.com", Phone = "0820007000", PasswordHash = "h", RoleId = role.Id };
        var supplier = new Supplier { UserId = supplierUser.Id, CompanyName = "Fin Co", ContactPerson = "Musa", Phone = "0820007000", Email = "fin@test.com" };
        var shopUser = new User { Email = "finshop@test.com", Phone = "0820008000", PasswordHash = "h", RoleId = role.Id };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Fin Spaza", OwnerName = "Nomsa", Phone = "0820008000", Address = "1 Test St" };
        db.AddRange(supplierUser, supplier, shopUser, shop);
        db.SaveChanges();
        return (new AdminFinanceController(db), db, supplier, shop);
    }

    private static Order AddOrder(SpazaSureDbContext db, Supplier supplier, SpazaShop shop, string method, string status,
        string paymentStatus, DateTime? deliveredAt = null, DateTime? paidOutAt = null) =>
        db.Orders.Add(new Order
        {
            SupplierId = supplier.Id, ShopId = shop.Id, OrderNumber = "SPZ-" + Guid.NewGuid().ToString("N")[..8],
            Status = status, Subtotal = 1000m, DeliveryFee = 150m, PlatformCommission = 50m, TotalAmount = 1200m,
            PaymentMethod = method, PaymentStatus = paymentStatus, DeliveredAt = deliveredAt,
            PaidOutAt = paidOutAt, PayoutReference = paidOutAt is null ? null : "EFT-1",
        }).Entity;

    /// <summary>Test data is added straight to the database, so write the ledger entries the controllers would have.</summary>
    private static async Task SyncLedger(SpazaSureDbContext db)
    {
        foreach (var t in db.ShopWalletTransactions.ToList())
            await (t.Type == ShopWallet.Withdrawal ? ShopWallet.SyncWithdrawalAsync(db, t) : ShopWallet.SyncTopUpAsync(db, t));
        foreach (var o in db.Orders.ToList()) await ShopWallet.SyncOrderAsync(db, o);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Summary_CountsOnlyConfirmedMoney_AndShowsWhatIsHeldAndOwed()
    {
        var (finance, db, supplier, shop) = Create();
        var now = DateTime.UtcNow;
        db.ShopWalletTransactions.AddRange(
            new ShopWalletTransaction { ShopId = shop.Id, Amount = 500m, Method = "eft", Status = "approved", ApprovedAt = now },
            new ShopWalletTransaction { ShopId = shop.Id, Amount = 300m, Method = "eft", Status = "pending" },
            new ShopWalletTransaction { ShopId = shop.Id, Amount = 99m, Method = "eft", Status = "rejected" },
            new ShopWalletTransaction { ShopId = shop.Id, Amount = 200m, Type = "withdrawal", Method = "eft", Status = "paid", ApprovedAt = now },
            new ShopWalletTransaction { ShopId = shop.Id, Amount = 100m, Type = "withdrawal", Method = "eft", Status = "pending" });
        AddOrder(db, supplier, shop, "paystack", "processing", "paid");
        AddOrder(db, supplier, shop, "paystack", "cancelled", "paid");
        AddOrder(db, supplier, shop, "cash_on_delivery", "delivered", "paid", now);
        AddOrder(db, supplier, shop, "cash_on_delivery", "dispatched", "pending");
        AddOrder(db, supplier, shop, "wallet", "processing", "paid");
        AddOrder(db, supplier, shop, "paystack", "delivered", "paid", now.AddDays(-1), paidOutAt: now);
        db.SaveChanges();
        await SyncLedger(db);

        var data = Data(await finance.Summary(null, null, default));

        // 500 top-up + two confirmed Paystack orders + 1200 cash collected
        data.GetProperty("moneyIn").GetDecimal().Should().Be(4100m);
        data.GetProperty("moneyOut").GetDecimal().Should().Be(1200m); // 1000 supplier payout + 200 shop withdrawal
        data.GetProperty("net").GetDecimal().Should().Be(2900m);
        // 500 top-up - 1200 wallet order - 200 paid withdrawal - 100 withdrawal still pending
        data.GetProperty("walletBalancesHeld").GetDecimal().Should().Be(-1000m);
        data.GetProperty("pendingWithdrawals").GetProperty("amount").GetDecimal().Should().Be(100m);
        data.GetProperty("owedToSuppliers").GetDecimal().Should().Be(1000m);     // delivered cash order, not yet paid out
        data.GetProperty("pendingDeposits").GetProperty("amount").GetDecimal().Should().Be(300m);
        data.GetProperty("codOutstanding").GetProperty("amount").GetDecimal().Should().Be(1200m);
        data.GetProperty("refundsDue").GetProperty("amount").GetDecimal().Should().Be(1200m);
    }

    [Fact]
    public async Task Budget_KeepsDeliveryFees_AndPaysDriversFromThem()
    {
        var (finance, db, supplier, shop) = Create();
        var driver = new Driver { FullName = "Lerato Mokoena", Phone = "0820009000", AccessToken = new string('a', 48), BadgeCode = "DRV001" };
        db.Drivers.Add(driver);
        var delivered = AddOrder(db, supplier, shop, "paystack", "delivered", "paid", DateTime.UtcNow);
        delivered.DriverId = driver.Id;
        db.SaveChanges();

        // Delivered, driver not paid yet: SpazaSure holds the R150 delivery fee
        var due = Data(await finance.DriverPayments("unpaid", null, default));
        due.GetArrayLength().Should().Be(1);
        due[0].GetProperty("deliveryFee").GetDecimal().Should().Be(150m);
        Data(await finance.Summary(null, null, default)).GetProperty("driverPayDue").GetProperty("count").GetInt32().Should().Be(1);

        (await finance.RecordDriverPayment(delivered.Id, new AdminFinanceController.DriverPaymentRequest(110m, "EFT-DRV-1"), default))
            .Should().BeOfType<OkObjectResult>();

        var budget = Data(await finance.Summary(null, null, default)).GetProperty("budget");
        budget.GetProperty("platformFees").GetDecimal().Should().Be(50m);
        budget.GetProperty("deliveryFees").GetDecimal().Should().Be(150m);
        budget.GetProperty("driverPayments").GetDecimal().Should().Be(110m);
        budget.GetProperty("deliveryNet").GetDecimal().Should().Be(40m);
        budget.GetProperty("total").GetDecimal().Should().Be(90m);
        Data(await finance.DriverPayments("unpaid", null, default)).GetArrayLength().Should().Be(0);

        var paid = Data(await finance.Transactions(null, null, "out", "driver_payment", null, 1, 25, default));
        paid.GetProperty("total").GetInt32().Should().Be(1);
        paid.GetProperty("items")[0].GetProperty("party").GetString().Should().Be("Lerato Mokoena");

        // The driver sees the delivery, and the pay, in their history
        var history = Data(await new SpazaSure.DeliveryService.Controllers.DriverPortalController(db).Jobs(driver.AccessToken))
            .GetProperty("history");
        history[0].GetProperty("driverPay").GetDecimal().Should().Be(110m);
        history[0].GetProperty("paid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DriverPayment_IsOnlyRecordedForDeliveredOrdersWithADriver()
    {
        var (finance, db, supplier, shop) = Create();
        var open = AddOrder(db, supplier, shop, "paystack", "dispatched", "paid");
        open.DriverId = Guid.NewGuid();
        var noDriver = AddOrder(db, supplier, shop, "paystack", "delivered", "paid", DateTime.UtcNow);
        db.SaveChanges();

        (await finance.RecordDriverPayment(open.Id, new AdminFinanceController.DriverPaymentRequest(100m, null), default))
            .Should().BeOfType<BadRequestObjectResult>();
        (await finance.RecordDriverPayment(noDriver.Id, new AdminFinanceController.DriverPaymentRequest(100m, null), default))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Transactions_FilterByDirectionAndCategory_AndGroupPayoutsPerBatch()
    {
        var (finance, db, supplier, shop) = Create();
        var paidAt = DateTime.UtcNow;
        AddOrder(db, supplier, shop, "paystack", "delivered", "paid", paidAt.AddDays(-5), paidAt);
        AddOrder(db, supplier, shop, "cash_on_delivery", "delivered", "paid", paidAt.AddDays(-5), paidAt);
        db.SaveChanges();

        var payouts = Data(await finance.Transactions(null, null, "out", null, null, 1, 25, default));
        payouts.GetProperty("total").GetInt32().Should().Be(1);
        var payout = payouts.GetProperty("items")[0];
        payout.GetProperty("amount").GetDecimal().Should().Be(2000m);
        payout.GetProperty("reference").GetString().Should().Be("EFT-1");

        var cash = Data(await finance.Transactions(null, null, "in", "order_cash_on_delivery", null, 1, 25, default));
        cash.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Transactions_RespectsDateRange()
    {
        var (finance, db, _, shop) = Create();
        db.ShopWalletTransactions.AddRange(
            new ShopWalletTransaction { ShopId = shop.Id, Amount = 100m, Method = "eft", Status = "approved", ApprovedAt = DateTime.UtcNow.AddDays(-60) },
            new ShopWalletTransaction { ShopId = shop.Id, Amount = 200m, Method = "eft", Status = "approved", ApprovedAt = DateTime.UtcNow.AddDays(-2) });
        db.SaveChanges();

        var recent = Data(await finance.Transactions(null, null, null, null, null, 1, 25, default));
        recent.GetProperty("total").GetInt32().Should().Be(1);

        var wide = Data(await finance.Transactions(DateTime.UtcNow.AddDays(-90), null, null, null, null, 1, 25, default));
        wide.GetProperty("total").GetInt32().Should().Be(2);
    }
}
