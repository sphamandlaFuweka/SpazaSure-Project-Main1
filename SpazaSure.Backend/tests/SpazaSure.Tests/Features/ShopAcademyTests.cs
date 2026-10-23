using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using SpazaSure.UserService.Services;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>Shop Academy: stock book, cash book, reports, lessons, funding readiness and the Business Record.</summary>
public class ShopAcademyTests
{
    private sealed record Fixture(SpazaSureDbContext Db, SpazaShop Shop, Product Product, ShopAcademyController Academy);

    private static Fixture Create()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var db = DbContextHelper.CreateInMemoryDb();
        var shopUser = new User { Email = "academy@test.com", Phone = "0820003100", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "spaza_owner").Id };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Academy Spaza", OwnerName = "Lindi", Phone = "0820003100", Address = "7 Test St", Status = "verified", ShopCode = "SS100001" };
        var supplierUser = new User { Email = "sup-acad@test.com", Phone = "0820003200", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "supplier").Id };
        var supplier = new Supplier { UserId = supplierUser.Id, CompanyName = "Acme Foods", ContactPerson = "Ann", Phone = "0820003200", Email = "sup-acad@test.com" };
        var product = new Product { SupplierId = supplier.Id, Name = "Sugar 2kg", Sku = "SUG2", Unit = "bag", Price = 40m, StockQty = 100, IsAvailable = true, IsApproved = true };
        var order = new Order
        {
            OrderNumber = "SPZ-ACAD-1", ShopId = shop.Id, SupplierId = supplier.Id, Status = "delivered", DeliveredAt = DateTime.UtcNow,
            Subtotal = 400m, DeliveryFee = 50m, TotalAmount = 450m, PaymentMethod = "wallet", PaymentStatus = "paid", DeliveryAddress = shop.Address,
            Items = [new OrderItem { ProductId = product.Id, Quantity = 10, OriginalUnitPrice = 40m, UnitPrice = 40m, LineTotal = 400m }],
        };
        db.AddRange(shopUser, shop, supplierUser, supplier, product, order);
        db.SaveChanges();
        return new Fixture(db, shop, product, Controller(db, shopUser.Id));
    }

    private static ShopAcademyController Controller(SpazaSureDbContext db, Guid userId) =>
        new(db, new ShopAcademyService(db, ConfigHelper.Build()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "spaza_owner")], "Test")),
                },
            },
        };

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((ObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("data");

    private static StockMovementRequest Move(string type, int qty) => new(type, qty, null, null, null, null);

    [Fact]
    public async Task DeliveredOrders_AreBookedIntoStockAndCashBook_OnlyOnce()
    {
        var f = Create();

        Data(await f.Academy.ImportOrders()).GetProperty("orders").GetInt32().Should().Be(1);
        var item = f.Db.ShopStockItems.Single();
        item.ProductId.Should().Be(f.Product.Id);
        item.Quantity.Should().Be(10);
        item.CostPrice.Should().Be(40m);
        item.SellingPrice.Should().Be(50m); // cost plus 25%
        var expense = f.Db.ShopCashEntries.Single();
        (expense.Direction, expense.Category, expense.Amount).Should().Be(("expense", "stock_purchase", 450m));

        Data(await f.Academy.ImportOrders()).GetProperty("orders").GetInt32().Should().Be(0);
        f.Db.ShopStockItems.Single().Quantity.Should().Be(10);
        f.Db.ShopCashEntries.Count().Should().Be(1);
    }

    [Fact]
    public async Task StockMovements_KeepTheQuantityRight_AndLossesShowInTheReport()
    {
        var f = Create();
        await f.Academy.ImportOrders();
        var id = f.Db.ShopStockItems.Single().Id;

        Data(await f.Academy.AddMovement(id, Move("sold", 3))).GetProperty("quantity").GetInt32().Should().Be(7);
        (await f.Academy.AddMovement(id, Move("sold", 50))).Should().BeOfType<BadRequestObjectResult>();
        Data(await f.Academy.AddMovement(id, Move("spoiled", 1))).GetProperty("quantity").GetInt32().Should().Be(6);
        // Counted 5 on the shelf: one more went missing than the book shows
        Data(await f.Academy.AddMovement(id, Move("count", 5))).GetProperty("quantity").GetInt32().Should().Be(5);
        f.Db.ShopStockMovements.Single(m => m.Type == "count").Quantity.Should().Be(-1);

        var report = Data(await f.Academy.Report(1));
        report.GetProperty("months")[0].GetProperty("stockLosses").GetDecimal().Should().Be(40m);
    }

    [Fact]
    public async Task CashBook_TotalsTheMonth_AndRejectsBadEntries()
    {
        var f = Create();
        var today = ShopAcademyService.Today;
        (await f.Academy.AddCashEntry(new("income", "cash_sales", 1200m, null, today))).Should().BeOfType<OkObjectResult>();
        (await f.Academy.AddCashEntry(new("expense", "electricity", 200m, "Prepaid", today))).Should().BeOfType<OkObjectResult>();
        (await f.Academy.AddCashEntry(new("income", "rent", 50m, null, today))).Should().BeOfType<BadRequestObjectResult>();
        (await f.Academy.AddCashEntry(new("expense", "rent", 0m, null, today))).Should().BeOfType<BadRequestObjectResult>();
        (await f.Academy.AddCashEntry(new("expense", "rent", 10m, null, today.AddDays(2)))).Should().BeOfType<BadRequestObjectResult>();

        var book = Data(await f.Academy.CashBook(null));
        book.GetProperty("income").GetDecimal().Should().Be(1200m);
        book.GetProperty("expenses").GetDecimal().Should().Be(200m);
        book.GetProperty("net").GetDecimal().Should().Be(1000m);
        book.GetProperty("daysRecorded").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Lessons_CompleteOnlyWhenTheQuizIsPassed()
    {
        var f = Create();
        var failed = Data(await f.Academy.SubmitQuiz("run-as-business", new([0, 1])));
        failed.GetProperty("passed").GetBoolean().Should().BeFalse();
        f.Db.ShopLessonProgress.Should().BeEmpty();

        var passed = Data(await f.Academy.SubmitQuiz("run-as-business", new([1, 0])));
        passed.GetProperty("passed").GetBoolean().Should().BeTrue();
        Data(await f.Academy.Overview()).GetProperty("lessons").GetProperty("done").GetInt32().Should().Be(1);
        (await f.Academy.SubmitQuiz("no-such-lesson", new([0]))).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Readiness_RisesWithBusinessDetails_AndTheBusinessRecordRenders()
    {
        var f = Create();
        var before = Data(await f.Academy.Overview()).GetProperty("readiness").GetProperty("score").GetInt32();

        (await f.Academy.SaveProfile(new("pty_ltd", "2024/123456/07", "9123456789", null, null, new DateOnly(2020, 3, 1), "rented", 2500m,
            1, 2, "07:00-20:00", 7, "Capitec", true, true, "SpazaSure, local wholesaler", "Buy a second fridge"))).Should().BeOfType<OkObjectResult>();
        var after = Data(await f.Academy.Overview()).GetProperty("readiness").GetProperty("score").GetInt32();
        after.Should().BeGreaterThanOrEqualTo(before + 45); // details, CIPC, SARS and bank account

        var path = Data(await f.Academy.RecordLink()).GetProperty("path").GetString()!;
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri("https://x" + path).Query);
        var pdf = await f.Academy.RecordPdf(f.Shop.Id, long.Parse(query["exp"]!), query["sig"]);
        var bytes = pdf.Should().BeOfType<FileContentResult>().Subject.FileContents;
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        (await f.Academy.RecordPdf(f.Shop.Id, long.Parse(query["exp"]!), "00")).Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task AnotherShop_CannotTouchThisShopsStock()
    {
        var f = Create();
        await f.Academy.ImportOrders();
        var id = f.Db.ShopStockItems.Single().Id;

        var otherUser = new User { Email = "other@test.com", Phone = "0820003300", PasswordHash = "h", RoleId = f.Db.Roles.First(r => r.Name == "spaza_owner").Id };
        f.Db.AddRange(otherUser, new SpazaShop { UserId = otherUser.Id, ShopName = "Other", OwnerName = "O", Phone = "0820003300", Address = "1 St" });
        f.Db.SaveChanges();
        var other = Controller(f.Db, otherUser.Id);

        (await other.AddMovement(id, Move("sold", 1))).Should().BeOfType<NotFoundObjectResult>();
        (await other.RemoveStockItem(id)).Should().BeOfType<NotFoundObjectResult>();
        Data(await other.Stock()).GetProperty("items").GetArrayLength().Should().Be(0);
    }
}
