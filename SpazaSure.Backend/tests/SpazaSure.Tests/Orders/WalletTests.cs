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
        var supplier = new Supplier { UserId = supplierUser.Id, CompanyName = "Bulk Co", ContactPerson = "Sipho", Phone = "0820008000", Email = "sup@test.com", CommissionRate = 5m };
        var product = new Product { SupplierId = supplier.Id, Name = "Maize meal 10kg", Sku = "MM10", Price = 100m, StockQty = 50, IsAvailable = true, IsApproved = true };
        db.AddRange(shopUser, shop, supplierUser, supplier, product);
        db.SaveChanges();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, shopUser.Id.ToString())], "Test"))
        };
        var config = new ConfigurationBuilder().Build();
        var events = new EventPublisher("__nonexistent__", "x", "x");
        return new Fixture(db, shop, product,
            new ShopWalletController(db, new PaystackPaymentService(new HttpClient(), config)) { ControllerContext = new ControllerContext { HttpContext = http } },
            new ShopOrdersController(db, new OrderPaystackPaymentService(new HttpClient(), config), events) { ControllerContext = new ControllerContext { HttpContext = http } },
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
    public async Task WalletCheckout_PaysImmediately_AndDeductsBalance()
    {
        var f = Create();
        f.Db.ShopWalletTransactions.Add(new ShopWalletTransaction { ShopId = f.Shop.Id, Amount = 1000m, Method = "eft", Status = "approved" });
        await f.Db.SaveChangesAsync();

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
    public async Task WalletCheckout_FailsWhenBalanceIsTooLow()
    {
        var f = Create();
        f.Db.ShopWalletTransactions.Add(new ShopWalletTransaction { ShopId = f.Shop.Id, Amount = 100m, Method = "eft", Status = "approved" });
        await f.Db.SaveChangesAsync();

        var result = await f.Checkout.PlaceOrder(Order(f.Product, 3));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CancelledWalletOrder_IsRefundedToTheWallet()
    {
        var f = Create();
        f.Db.ShopWalletTransactions.Add(new ShopWalletTransaction { ShopId = f.Shop.Id, Amount = 1000m, Method = "eft", Status = "approved" });
        await f.Db.SaveChangesAsync();
        await f.Checkout.PlaceOrder(Order(f.Product, 3));

        var order = await f.Db.Orders.SingleAsync();
        order.Status = "cancelled";
        await f.Db.SaveChangesAsync();

        (await Balance(f)).Should().Be(1000m);
    }

    [Fact]
    public async Task OrdersPaidWithPaystack_DoNotReduceTheWallet()
    {
        var f = Create();
        f.Db.ShopWalletTransactions.Add(new ShopWalletTransaction { ShopId = f.Shop.Id, Amount = 400m, Method = "eft", Status = "approved" });
        f.Db.Orders.Add(new Order
        {
            ShopId = f.Shop.Id, SupplierId = f.Product.SupplierId, OrderNumber = "SPZ-PAYSTACK", Status = "delivered",
            Subtotal = 900m, TotalAmount = 1000m, PaymentMethod = "paystack", PaymentStatus = "paid",
        });
        await f.Db.SaveChangesAsync();

        (await Balance(f)).Should().Be(400m);
    }
}
