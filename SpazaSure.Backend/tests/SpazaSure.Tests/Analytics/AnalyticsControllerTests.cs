using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.AnalyticsService.Controllers;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Analytics;

public class AnalyticsControllerTests
{
    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    private static (AnalyticsController Ctrl, SpazaSureDbContext Db, Supplier Supplier, SpazaShop Shop) Create()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "supplier");
        var user = new User { Email = "stats@test.com", Phone = "0820001100", PasswordHash = "h", RoleId = role.Id };
        var supplier = new Supplier { UserId = user.Id, CompanyName = "Stats Co", ContactPerson = "Lerato", Phone = "0820001100", Email = "stats@test.com" };
        var shopUser = new User { Email = "statshop@test.com", Phone = "0820001200", PasswordHash = "h", RoleId = role.Id };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Stats Spaza", OwnerName = "Sipho", Phone = "0820001200", Address = "2 Test St" };
        db.AddRange(user, supplier, shopUser, shop);
        db.SaveChanges();

        var ctrl = new AnalyticsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test")),
                },
            },
        };
        return (ctrl, db, supplier, shop);
    }

    private static Order AddOrder(SpazaSureDbContext db, Supplier supplier, SpazaShop shop, string status, decimal total,
        DateTime createdAt, params OrderItem[] items)
    {
        var order = new Order
        {
            SupplierId = supplier.Id, ShopId = shop.Id, OrderNumber = "SPZ-" + Guid.NewGuid().ToString("N")[..8],
            Status = status, Subtotal = total, TotalAmount = total, PaymentMethod = "wallet", PaymentStatus = "paid",
            Items = [.. items],
        };
        db.Orders.Add(order);
        db.SaveChanges();
        order.CreatedAt = createdAt;
        db.SaveChanges();
        return order;
    }

    [Fact]
    public async Task Summary_CountsThisMonthsRealOrders_AndComparesWithLastMonth()
    {
        var (ctrl, db, supplier, shop) = Create();
        var now = DateTime.UtcNow;
        var thisMonth = new DateTime(now.Year, now.Month, 1, 12, 0, 0, DateTimeKind.Utc);
        var lastMonth = thisMonth.AddMonths(-1);
        AddOrder(db, supplier, shop, "delivered", 300m, thisMonth);
        AddOrder(db, supplier, shop, "pending", 200m, thisMonth);
        AddOrder(db, supplier, shop, "cancelled", 999m, thisMonth);
        AddOrder(db, supplier, shop, "draft", 999m, thisMonth);
        AddOrder(db, supplier, shop, "delivered", 250m, lastMonth);

        var data = Data(await ctrl.Summary());

        data.GetProperty("totalRevenue").GetDecimal().Should().Be(500m);
        data.GetProperty("totalOrders").GetInt32().Should().Be(2);
        data.GetProperty("revenueGrowth").GetDouble().Should().Be(100.0);
        data.GetProperty("ordersGrowth").GetDouble().Should().Be(100.0);
        data.GetProperty("pendingOrders").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Summary_ReportsNoGrowth_WhenLastMonthHadNoSales()
    {
        var (ctrl, db, supplier, shop) = Create();
        AddOrder(db, supplier, shop, "delivered", 100m, DateTime.UtcNow);

        var data = Data(await ctrl.Summary());

        data.GetProperty("revenueGrowth").GetDouble().Should().Be(0);
    }

    [Fact]
    public async Task Summary_IgnoresOtherSuppliersOrders()
    {
        var (ctrl, db, _, shop) = Create();
        var other = new Supplier { UserId = Guid.NewGuid(), CompanyName = "Other", ContactPerson = "X", Phone = "0820009999", Email = "o@test.com" };
        db.Suppliers.Add(other);
        db.SaveChanges();
        AddOrder(db, other, shop, "delivered", 5000m, DateTime.UtcNow);

        var data = Data(await ctrl.Summary());

        data.GetProperty("totalRevenue").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Revenue_GroupsByDay_AndSkipsCancelledAndDraftOrders()
    {
        var (ctrl, db, supplier, shop) = Create();
        var day = DateTime.UtcNow.Date.AddDays(-2).AddHours(10);
        AddOrder(db, supplier, shop, "delivered", 100m, day);
        AddOrder(db, supplier, shop, "confirmed", 50m, day.AddHours(1));
        AddOrder(db, supplier, shop, "cancelled", 999m, day);
        AddOrder(db, supplier, shop, "draft", 999m, day);
        AddOrder(db, supplier, shop, "delivered", 70m, DateTime.UtcNow.AddMonths(-3));

        var rows = Data(await ctrl.Revenue("month")).EnumerateArray().ToList();

        rows.Should().ContainSingle();
        rows[0].GetProperty("date").GetString().Should().Be(day.ToString("yyyy-MM-dd"));
        rows[0].GetProperty("revenue").GetDecimal().Should().Be(150m);
        rows[0].GetProperty("orders").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task TopProducts_RanksByRevenue_AndLeavesOutDraftAndCancelledOrders()
    {
        var (ctrl, db, supplier, shop) = Create();
        var sugar = new Product { SupplierId = supplier.Id, Name = "Sugar 2kg", Sku = "SUG2", Unit = "bag", Price = 40m, StockQty = 10, IsAvailable = true, IsApproved = true };
        var rice = new Product { SupplierId = supplier.Id, Name = "Rice 5kg", Sku = "RIC5", Unit = "bag", Price = 90m, StockQty = 10, IsAvailable = true, IsApproved = true };
        db.Products.AddRange(sugar, rice);
        db.SaveChanges();
        var now = DateTime.UtcNow;
        AddOrder(db, supplier, shop, "delivered", 400m, now,
            new OrderItem { ProductId = sugar.Id, Quantity = 10, OriginalUnitPrice = 40m, UnitPrice = 40m, LineTotal = 400m });
        AddOrder(db, supplier, shop, "delivered", 180m, now,
            new OrderItem { ProductId = rice.Id, Quantity = 2, OriginalUnitPrice = 90m, UnitPrice = 90m, LineTotal = 180m });
        AddOrder(db, supplier, shop, "draft", 9000m, now,
            new OrderItem { ProductId = rice.Id, Quantity = 100, OriginalUnitPrice = 90m, UnitPrice = 90m, LineTotal = 9000m });
        AddOrder(db, supplier, shop, "cancelled", 9000m, now,
            new OrderItem { ProductId = rice.Id, Quantity = 100, OriginalUnitPrice = 90m, UnitPrice = 90m, LineTotal = 9000m });

        var rows = Data(await ctrl.TopProducts()).EnumerateArray().ToList();

        rows.Select(r => r.GetProperty("name").GetString()).Should().Equal("Sugar 2kg", "Rice 5kg");
        rows[1].GetProperty("unitsSold").GetInt32().Should().Be(2);
        rows[1].GetProperty("revenue").GetDecimal().Should().Be(180m);
    }

    [Fact]
    public async Task Endpoints_ReturnNotFound_ForAUserWithoutASupplierProfile()
    {
        var (ctrl, _, _, _) = Create();
        ctrl.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test"));

        (await ctrl.Summary()).Should().BeOfType<NotFoundObjectResult>();
        (await ctrl.Revenue()).Should().BeOfType<NotFoundObjectResult>();
        (await ctrl.TopProducts()).Should().BeOfType<NotFoundObjectResult>();
    }
}
