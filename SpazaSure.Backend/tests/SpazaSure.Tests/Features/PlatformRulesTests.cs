using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class PlatformRulesTests
{
    // ── Settings store ──────────────────────────────────────────────────────

    [Fact]
    public async Task Load_WithNothingSaved_ReturnsPreviousHardCodedBehaviour()
    {
        var db = DbContextHelper.CreateInMemoryDb();

        var rules = await PlatformSettingsStore.LoadAsync(db);

        rules.StandardDeliveryFee.Should().Be(150m);
        rules.ExpressDeliveryFee.Should().Be(250m);
        rules.ShopServiceFeePercent.Should().Be(0m);
        rules.PayoutDelayDays.Should().Be(3);
        rules.MaintenanceMode.Should().BeFalse();
        rules.RequireSupplierApproval.Should().BeTrue();
        rules.AutoApproveVerifiedSupplierProducts.Should().BeFalse();
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryValue()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var saved = new PlatformRules
        {
            StandardDeliveryFee = 99.5m, ExpressDeliveryFee = 199m, GroupBuyBaseDeliveryFee = 40m,
            GroupBuyPerKmRate = 6.5m, DeliveryPerKmRate = 9m, ShopServiceFeePercent = 1.5m, PayoutDelayDays = 7, SupplierReachKm = 80m,
            MaintenanceMode = true, MaintenanceMessage = "Back at 10:00", RequireSupplierApproval = false,
            AutoApproveVerifiedSupplierProducts = true, CashOnDeliveryEnabled = false, CashOnDeliveryMaxOrderTotal = 2500m,
            DepositBankName = "FNB Business", DepositAccountHolder = "SpazaSure Holdings", DepositAccountNumber = "62000000001", DepositBranchCode = "250655",
        };

        await PlatformSettingsStore.SaveAsync(db, saved);
        await PlatformSettingsStore.SaveAsync(db, saved); // saving twice updates, never duplicates

        (await PlatformSettingsStore.LoadAsync(db)).Should().BeEquivalentTo(saved);
        (await db.PlatformSettings.CountAsync()).Should().Be(18);
    }

    [Theory]
    [InlineData(-1, 250, 0, 3, "Standard delivery fee")]
    [InlineData(150, 250, 25, 3, "Shop service fee")]
    [InlineData(150, 250, 0, 90, "Payout delay")]
    public void Validate_RejectsOutOfRangeValues(decimal standard, decimal express, decimal fee, int delay, string expected)
    {
        var rules = new PlatformRules
        {
            StandardDeliveryFee = standard, ExpressDeliveryFee = express,
            ShopServiceFeePercent = fee, PayoutDelayDays = delay,
        };

        rules.Validate().Should().StartWith(expected);
    }

    [Theory]
    [InlineData("1055 1015 752")]
    [InlineData("12345")]
    [InlineData("")]
    public void Validate_RejectsInvalidDepositAccountNumbers(string accountNumber)
    {
        new PlatformRules { DepositAccountNumber = accountNumber }.Validate().Should().StartWith("Deposit account number");
    }

    // ── Supplier approval on registration ───────────────────────────────────

    [Theory]
    [InlineData(true, "pending")]
    [InlineData(false, "active")]
    public async Task Register_Supplier_StatusFollowsApprovalSetting(bool requireApproval, string expectedStatus)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        await PlatformSettingsStore.SaveAsync(db, new PlatformRules { RequireSupplierApproval = requireApproval });
        var svc = new AuthenticationService(db, ConfigHelper.Build());

        await svc.RegisterAsync(new RegisterRequest("supplier", "approval@test.com", "0820000055",
            "Password123!", "Approval Co", "Lindiwe", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");

        (await db.Suppliers.SingleAsync()).Status.Should().Be(expectedStatus);
    }

    // ── Payouts ─────────────────────────────────────────────────────────────

    private static (SupplierPayoutsController Supplier, AdminPayoutsController Admin, SpazaSureDbContext Db, Supplier Entity) CreatePayouts()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "supplier");
        var user = new User { Email = "pay@test.com", Phone = "0820003000", PasswordHash = "h", RoleId = role.Id };
        var supplier = new Supplier { UserId = user.Id, CompanyName = "Payout Co", ContactPerson = "Zola", Phone = "0820003000", Email = "pay@test.com", BankAccount = "123" };
        var shopUser = new User { Email = "shop2@test.com", Phone = "0820004000", PasswordHash = "h", RoleId = role.Id };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Shop", OwnerName = "Thabo", Phone = "0820004000", Address = "2 Test St" };
        db.AddRange(user, supplier, shopUser, shop);
        db.SaveChanges();

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
        var supplierCtrl = new SupplierPayoutsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };
        var adminCtrl = new AdminPayoutsController(db, new EventPublisher("__nonexistent__", "x", "x"), TestPaystack.Transfers());
        return (supplierCtrl, adminCtrl, db, supplier);
    }

    private static Order AddOrder(SpazaSureDbContext db, Supplier supplier, string status, DateTime? deliveredAt,
        string paymentMethod = "paystack", string paymentStatus = "paid")
    {
        var order = new Order
        {
            SupplierId = supplier.Id, ShopId = db.SpazaShops.First().Id,
            OrderNumber = "SPZ-" + Guid.NewGuid().ToString("N")[..8], Status = status,
            Subtotal = 1000m, DeliveryFee = 150m, PlatformCommission = 50m, TotalAmount = 1200m,
            PaymentMethod = paymentMethod, PaymentStatus = paymentStatus, DeliveredAt = deliveredAt,
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return order;
    }

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    [Fact]
    public async Task Summary_SplitsMoneyByStage_AndExcludesCommissionAndUnpaidOrders()
    {
        var (supplierCtrl, _, db, supplier) = CreatePayouts();
        AddOrder(db, supplier, "dispatched", null);                           // awaiting delivery
        AddOrder(db, supplier, "delivered", DateTime.UtcNow.AddDays(-1));      // pending (3-day delay)
        AddOrder(db, supplier, "delivered", DateTime.UtcNow.AddDays(-5));      // available
        AddOrder(db, supplier, "delivered", DateTime.UtcNow.AddDays(-5), paymentMethod: "invoice"); // not paid via SpazaSure
        AddOrder(db, supplier, "delivered", DateTime.UtcNow.AddDays(-5), paymentStatus: "pending"); // payment not confirmed

        var data = Data(await supplierCtrl.Summary());

        // Each order owes its Subtotal = R1,000 (SpazaSure delivers, and keeps the delivery fee and commission)
        data.GetProperty("awaitingDelivery").GetDecimal().Should().Be(1000m);
        data.GetProperty("pending").GetDecimal().Should().Be(1000m);
        data.GetProperty("available").GetDecimal().Should().Be(1000m);
        data.GetProperty("paidOut").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task PayoutDelay_IsReadFromSettings()
    {
        var (supplierCtrl, _, db, supplier) = CreatePayouts();
        await PlatformSettingsStore.SaveAsync(db, new PlatformRules { PayoutDelayDays = 0 });
        AddOrder(db, supplier, "delivered", DateTime.UtcNow.AddMinutes(-1));

        var data = Data(await supplierCtrl.Summary());

        data.GetProperty("available").GetDecimal().Should().Be(1000m);
        data.GetProperty("pending").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task MarkPaid_PaysOnlyAvailableOrders_WithReference()
    {
        var (supplierCtrl, adminCtrl, db, supplier) = CreatePayouts();
        var available = AddOrder(db, supplier, "delivered", DateTime.UtcNow.AddDays(-5));
        var pending = AddOrder(db, supplier, "delivered", DateTime.UtcNow.AddDays(-1));

        var result = await adminCtrl.MarkPaid(supplier.Id, new MarkPayoutPaidRequest("EFT-2026-001"));

        result.Should().BeOfType<OkObjectResult>();
        (await db.Orders.FindAsync(available.Id))!.PayoutReference.Should().Be("EFT-2026-001");
        (await db.Orders.FindAsync(pending.Id))!.PaidOutAt.Should().BeNull();

        var data = Data(await supplierCtrl.Summary());
        data.GetProperty("paidOut").GetDecimal().Should().Be(1000m);
        data.GetProperty("available").GetDecimal().Should().Be(0m);

        // Nothing left to pay
        (await adminCtrl.MarkPaid(supplier.Id, new MarkPayoutPaidRequest("EFT-2026-002"))).Should().BeOfType<BadRequestObjectResult>();
    }
}
