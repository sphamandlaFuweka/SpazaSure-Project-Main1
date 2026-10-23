using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

public record BusinessProfileRequest(
    string? LegalStructure, string? CipcRegistrationNumber, string? SarsTaxNumber, string? VatNumber,
    string? TradingLicenceNumber, DateOnly? StartedTradingOn, string? PremisesType, decimal? MonthlyRent,
    int FullTimeStaff, int PartTimeStaff, string? TradingHours, int TradingDaysPerWeek, string? BankName,
    bool HasBusinessBankAccount, bool AcceptsCardPayments, string? MainSuppliers, string? Goals);
public record StockItemRequest(
    string Name, string? Category, string? Unit, string? Barcode, decimal CostPrice, decimal SellingPrice,
    int Quantity, int ReorderLevel, DateOnly? ExpiryDate);
public record StockMovementRequest(
    string Type, int Quantity, decimal? UnitCost, decimal? UnitPrice, string? Note, DateOnly? OccurredOn,
    bool AddToCashBook = false);
public record CashEntryRequest(string Direction, string Category, decimal Amount, string? Note, DateOnly? OccurredOn);
public record LessonAnswersRequest(int[] Answers);

/// <summary>
/// Shop Academy for spaza owners: business details, a stock book, a cash book, monthly reports,
/// funding readiness, practical lessons, and a Business Record PDF to show funders.
/// </summary>
[ApiController]
[Route("api/shop/academy")]
[Authorize(Roles = "spaza_owner")]
public class ShopAcademyController(SpazaSureDbContext db, ShopAcademyService academy) : ControllerBase
{
    private static readonly string[] LegalStructures = ["sole_proprietor", "partnership", "pty_ltd", "cooperative"];
    private static readonly string[] PremisesTypes = ["owned", "rented", "home"];
    private const decimal MaxAmount = 10_000_000m;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private Task<SpazaShop?> ShopAsync() => db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
    private static IActionResult NoShop() => new NotFoundObjectResult(ApiResponse.Fail("Shop not found."));

    // ── Overview ──

    [HttpGet]
    public async Task<IActionResult> Overview()
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var profile = await db.ShopBusinessProfiles.FirstOrDefaultAsync(p => p.ShopId == shop.Id);
        var months = await academy.MonthsAsync(shop.Id, 3);
        var stock = await academy.StockAsync(shop.Id);
        var readiness = await academy.ReadinessAsync(shop, profile, months, stock);
        var lessonsDone = await db.ShopLessonProgress.CountAsync(p => p.ShopId == shop.Id);

        var soon = ShopAcademyService.Today.AddDays(ShopAcademyService.ExpiringSoonDays);
        var alerts = await db.ShopStockItems
            .Where(i => i.ShopId == shop.Id && i.IsActive &&
                        (i.Quantity <= i.ReorderLevel || (i.ExpiryDate != null && i.ExpiryDate <= soon && i.Quantity > 0)))
            .OrderBy(i => i.Quantity).Take(10)
            .Select(i => new { i.Id, i.Name, i.Quantity, i.ReorderLevel, i.ExpiryDate, i.Unit })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            ShopName = shop.ShopName,
            ThisMonth = months[^1],
            LastMonth = months.Count > 1 ? months[^2] : null,
            Stock = stock,
            StockAlerts = alerts,
            ProfileCompleteness = ShopAcademyService.ProfileCompleteness(profile),
            RecordedMonths = ShopAcademyService.RecordedMonths(months),
            Readiness = new { Score = readiness.Sum(r => r.Earned), Items = readiness },
            Lessons = new { Done = lessonsDone, Total = ShopAcademyLessons.All.Length },
            Categories = new { Income = ShopAcademyService.IncomeCategories, Expense = ShopAcademyService.ExpenseCategories },
        }));
    }

    // ── Business details ──

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var profile = await db.ShopBusinessProfiles.FirstOrDefaultAsync(p => p.ShopId == shop.Id)
                      ?? new ShopBusinessProfile { ShopId = shop.Id };
        return Ok(ApiResponse<object>.Ok(ProfileDto(profile)));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> SaveProfile([FromBody] BusinessProfileRequest req)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var legal = req.LegalStructure ?? "sole_proprietor";
        var premises = req.PremisesType ?? "rented";
        if (!LegalStructures.Contains(legal)) return BadRequest(ApiResponse.Fail("Choose a valid business type."));
        if (!PremisesTypes.Contains(premises)) return BadRequest(ApiResponse.Fail("Choose owned, rented or home premises."));
        if (req.FullTimeStaff is < 0 or > 500 || req.PartTimeStaff is < 0 or > 500)
            return BadRequest(ApiResponse.Fail("Enter a realistic number of staff."));
        if (req.TradingDaysPerWeek is < 1 or > 7) return BadRequest(ApiResponse.Fail("Trading days must be 1 to 7."));
        if (req.MonthlyRent is < 0 or > MaxAmount) return BadRequest(ApiResponse.Fail("Enter a valid monthly rent."));
        if (req.StartedTradingOn is { } started && started > ShopAcademyService.Today)
            return BadRequest(ApiResponse.Fail("The start date can't be in the future."));

        var profile = await db.ShopBusinessProfiles.FirstOrDefaultAsync(p => p.ShopId == shop.Id);
        if (profile == null)
        {
            profile = new ShopBusinessProfile { ShopId = shop.Id };
            db.ShopBusinessProfiles.Add(profile);
        }
        profile.LegalStructure = legal;
        profile.CipcRegistrationNumber = Clean(req.CipcRegistrationNumber, 30);
        profile.SarsTaxNumber = Clean(req.SarsTaxNumber, 20);
        profile.VatNumber = Clean(req.VatNumber, 20);
        profile.TradingLicenceNumber = Clean(req.TradingLicenceNumber, 50);
        profile.StartedTradingOn = req.StartedTradingOn;
        profile.PremisesType = premises;
        profile.MonthlyRent = premises == "rented" ? req.MonthlyRent : null;
        profile.FullTimeStaff = req.FullTimeStaff;
        profile.PartTimeStaff = req.PartTimeStaff;
        profile.TradingHours = Clean(req.TradingHours, 100);
        profile.TradingDaysPerWeek = req.TradingDaysPerWeek;
        profile.BankName = Clean(req.BankName, 60);
        profile.HasBusinessBankAccount = req.HasBusinessBankAccount;
        profile.AcceptsCardPayments = req.AcceptsCardPayments;
        profile.MainSuppliers = Clean(req.MainSuppliers, 500);
        profile.Goals = Clean(req.Goals, 1000);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(ProfileDto(profile), "Business details saved."));
    }

    private static object ProfileDto(ShopBusinessProfile p) => new
    {
        p.LegalStructure, p.CipcRegistrationNumber, p.SarsTaxNumber, p.VatNumber, p.TradingLicenceNumber,
        p.StartedTradingOn, p.PremisesType, p.MonthlyRent, p.FullTimeStaff, p.PartTimeStaff, p.TradingHours,
        p.TradingDaysPerWeek, p.BankName, p.HasBusinessBankAccount, p.AcceptsCardPayments, p.MainSuppliers, p.Goals,
        Completeness = ShopAcademyService.ProfileCompleteness(p),
    };

    // ── Stock book ──

    [HttpGet("stock")]
    public async Task<IActionResult> Stock()
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var soon = ShopAcademyService.Today.AddDays(ShopAcademyService.ExpiringSoonDays);
        var items = await db.ShopStockItems
            .Where(i => i.ShopId == shop.Id && i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync();
        var since = ShopAcademyService.Today.AddDays(-30);
        var sold = await db.ShopStockMovements
            .Where(m => m.ShopId == shop.Id && m.Type == "sold" && m.OccurredOn >= since)
            .GroupBy(m => m.StockItemId)
            .Select(g => new { g.Key, Units = -g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(g => g.Key, g => g.Units);

        return Ok(ApiResponse<object>.Ok(new
        {
            Summary = await academy.StockAsync(shop.Id),
            Items = items.Select(i => new
            {
                i.Id, i.ProductId, i.Name, i.Category, i.Unit, i.Barcode, i.CostPrice, i.SellingPrice,
                i.Quantity, i.ReorderLevel, i.ExpiryDate,
                MarkupPct = i.CostPrice > 0 ? Math.Round((i.SellingPrice - i.CostPrice) / i.CostPrice * 100, 1) : (decimal?)null,
                IsLow = i.Quantity <= i.ReorderLevel,
                ExpiresSoon = i.ExpiryDate is { } d && d <= soon && i.Quantity > 0,
                SoldLast30Days = sold.GetValueOrDefault(i.Id),
            }),
        }));
    }

    [HttpPost("stock")]
    public async Task<IActionResult> AddStockItem([FromBody] StockItemRequest req)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        if (ValidateItem(req) is { } problem) return BadRequest(ApiResponse.Fail(problem));

        var item = new ShopStockItem { ShopId = shop.Id };
        Fill(item, req);
        db.ShopStockItems.Add(item);
        if (req.Quantity > 0)
        {
            db.ShopStockMovements.Add(new ShopStockMovement
            {
                ShopId = shop.Id, StockItemId = item.Id, Type = "count", Quantity = req.Quantity,
                UnitCost = item.CostPrice, UnitPrice = item.SellingPrice, Note = "Opening stock",
                OccurredOn = ShopAcademyService.Today,
            });
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { item.Id }, "Item added to your stock book."));
    }

    /// <summary>Edits an item's details. The quantity changes only through stock movements.</summary>
    [HttpPut("stock/{id:guid}")]
    public async Task<IActionResult> UpdateStockItem(Guid id, [FromBody] StockItemRequest req)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var item = await db.ShopStockItems.FirstOrDefaultAsync(i => i.Id == id && i.ShopId == shop.Id && i.IsActive);
        if (item == null) return NotFound(ApiResponse.Fail("Stock item not found."));
        if (ValidateItem(req) is { } problem) return BadRequest(ApiResponse.Fail(problem));
        var quantity = item.Quantity;
        Fill(item, req);
        item.Quantity = quantity;
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Item updated."));
    }

    /// <summary>Removes the item from the stock book; its history stays in past reports.</summary>
    [HttpDelete("stock/{id:guid}")]
    public async Task<IActionResult> RemoveStockItem(Guid id)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var item = await db.ShopStockItems.FirstOrDefaultAsync(i => i.Id == id && i.ShopId == shop.Id);
        if (item == null) return NotFound(ApiResponse.Fail("Stock item not found."));
        item.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Item removed from your stock book."));
    }

    [HttpPost("stock/{id:guid}/movements")]
    public async Task<IActionResult> AddMovement(Guid id, [FromBody] StockMovementRequest req)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var item = await db.ShopStockItems.FirstOrDefaultAsync(i => i.Id == id && i.ShopId == shop.Id && i.IsActive);
        if (item == null) return NotFound(ApiResponse.Fail("Stock item not found."));
        if (!ShopAcademyService.MovementTypes.Contains(req.Type)) return BadRequest(ApiResponse.Fail("Choose what happened to the stock."));
        if (req.Quantity < 0 || req.Quantity > 100_000 || (req.Quantity == 0 && req.Type != "count"))
            return BadRequest(ApiResponse.Fail("Enter a valid quantity."));
        if (req.UnitCost is < 0 or > MaxAmount || req.UnitPrice is < 0 or > MaxAmount)
            return BadRequest(ApiResponse.Fail("Enter a valid price."));
        var day = req.OccurredOn ?? ShopAcademyService.Today;
        if (day > ShopAcademyService.Today) return BadRequest(ApiResponse.Fail("The date can't be in the future."));
        if (req.Type is "sold" or "spoiled" or "stolen" or "own_use" && req.Quantity > item.Quantity)
            return BadRequest(ApiResponse.Fail($"You only have {item.Quantity} {item.Name} in your stock book. Do a stock count first if that's wrong."));

        var movement = ShopAcademyService.Apply(item, req.Type, req.Quantity, req.UnitCost, req.UnitPrice, Clean(req.Note, 300), day);
        if (movement == null) return Ok(ApiResponse<object>.Ok(new { item.Quantity }, "Count matches your stock book."));
        db.ShopStockMovements.Add(movement);

        if (req.AddToCashBook && req.Type is "received" or "sold")
        {
            var received = req.Type == "received";
            db.ShopCashEntries.Add(new ShopCashEntry
            {
                ShopId = shop.Id,
                Direction = received ? "expense" : "income",
                Category = received ? "stock_purchase" : "cash_sales",
                Amount = req.Quantity * (received ? movement.UnitCost : movement.UnitPrice),
                Note = $"{req.Quantity} × {item.Name}",
                OccurredOn = day,
            });
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { item.Quantity }, "Stock book updated."));
    }

    [HttpGet("stock/{id:guid}/movements")]
    public async Task<IActionResult> Movements(Guid id)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var list = await db.ShopStockMovements
            .Where(m => m.StockItemId == id && m.ShopId == shop.Id)
            .OrderByDescending(m => m.OccurredOn).ThenByDescending(m => m.CreatedAt)
            .Take(100)
            .Select(m => new { m.Id, m.Type, m.Quantity, m.UnitCost, m.UnitPrice, m.Note, m.OccurredOn })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(list));
    }

    /// <summary>Books delivered SpazaSure orders into the stock book and cash book.</summary>
    [HttpPost("stock/import-orders")]
    public async Task<IActionResult> ImportOrders()
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var (orders, lines) = await academy.ImportDeliveredOrdersAsync(shop.Id);
        var message = orders == 0
            ? "No new delivered SpazaSure orders to book in."
            : $"Booked in {orders} delivered order{(orders == 1 ? "" : "s")} ({lines} product line{(lines == 1 ? "" : "s")}).";
        return Ok(ApiResponse<object>.Ok(new { Orders = orders, Lines = lines }, message));
    }

    // ── Cash book ──

    [HttpGet("cashbook")]
    public async Task<IActionResult> CashBook([FromQuery] string? month)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var start = DateOnly.TryParseExact((month ?? "") + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : new DateOnly(ShopAcademyService.Today.Year, ShopAcademyService.Today.Month, 1);
        var end = start.AddMonths(1);
        var entries = await db.ShopCashEntries
            .Where(c => c.ShopId == shop.Id && c.OccurredOn >= start && c.OccurredOn < end)
            .OrderByDescending(c => c.OccurredOn).ThenByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Direction, c.Category, c.Amount, c.Note, c.OccurredOn })
            .ToListAsync();
        var income = entries.Where(e => e.Direction == "income").Sum(e => e.Amount);
        var expenses = entries.Where(e => e.Direction == "expense").Sum(e => e.Amount);
        return Ok(ApiResponse<object>.Ok(new
        {
            Month = $"{start:yyyy-MM}",
            Income = income,
            Expenses = expenses,
            Net = income - expenses,
            DaysRecorded = entries.Select(e => e.OccurredOn).Distinct().Count(),
            ExpenseByCategory = entries.Where(e => e.Direction == "expense")
                .GroupBy(e => e.Category).Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) })
                .OrderByDescending(g => g.Amount),
            Entries = entries,
        }));
    }

    [HttpPost("cashbook")]
    public async Task<IActionResult> AddCashEntry([FromBody] CashEntryRequest req)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var categories = req.Direction switch
        {
            "income" => ShopAcademyService.IncomeCategories,
            "expense" => ShopAcademyService.ExpenseCategories,
            _ => null,
        };
        if (categories == null) return BadRequest(ApiResponse.Fail("Choose money in or money out."));
        if (!categories.Contains(req.Category)) return BadRequest(ApiResponse.Fail("Choose a category."));
        if (req.Amount <= 0 || req.Amount > MaxAmount) return BadRequest(ApiResponse.Fail("Enter an amount above R0."));
        var day = req.OccurredOn ?? ShopAcademyService.Today;
        if (day > ShopAcademyService.Today) return BadRequest(ApiResponse.Fail("The date can't be in the future."));

        var entry = new ShopCashEntry
        {
            ShopId = shop.Id, Direction = req.Direction, Category = req.Category,
            Amount = Math.Round(req.Amount, 2), Note = Clean(req.Note, 300), OccurredOn = day,
        };
        db.ShopCashEntries.Add(entry);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { entry.Id }, "Saved to your cash book."));
    }

    [HttpDelete("cashbook/{id:guid}")]
    public async Task<IActionResult> DeleteCashEntry(Guid id)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var entry = await db.ShopCashEntries.FirstOrDefaultAsync(c => c.Id == id && c.ShopId == shop.Id);
        if (entry == null) return NotFound(ApiResponse.Fail("Entry not found."));
        db.ShopCashEntries.Remove(entry);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Entry deleted."));
    }

    // ── Reports ──

    [HttpGet("report")]
    public async Task<IActionResult> Report([FromQuery] int months = 6)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var rows = await academy.MonthsAsync(shop.Id, Math.Clamp(months, 1, 12));
        return Ok(ApiResponse<object>.Ok(new
        {
            Months = rows,
            Totals = new
            {
                Income = rows.Sum(r => r.Income),
                StockPurchases = rows.Sum(r => r.StockPurchases),
                OperatingExpenses = rows.Sum(r => r.OperatingExpenses),
                NetProfit = rows.Sum(r => r.NetProfit),
                StockLosses = rows.Sum(r => r.StockLosses),
            },
            RecordedMonths = ShopAcademyService.RecordedMonths(rows),
        }));
    }

    // ── Lessons ──

    [HttpGet("lessons")]
    public async Task<IActionResult> Lessons()
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var done = await db.ShopLessonProgress.Where(p => p.ShopId == shop.Id).ToDictionaryAsync(p => p.LessonKey);
        return Ok(ApiResponse<object>.Ok(ShopAcademyLessons.All.Select(l => new
        {
            l.Key, l.Title, l.Category, l.Minutes, l.Summary,
            Completed = done.ContainsKey(l.Key),
            Score = done.TryGetValue(l.Key, out var p) ? p.QuizScore : (int?)null,
            Total = l.Quiz.Length,
        })));
    }

    /// <summary>The lesson with its quiz questions (answers are checked on the server).</summary>
    [HttpGet("lessons/{key}")]
    public async Task<IActionResult> Lesson(string key)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var lesson = ShopAcademyLessons.Find(key);
        if (lesson == null) return NotFound(ApiResponse.Fail("Lesson not found."));
        var progress = await db.ShopLessonProgress.FirstOrDefaultAsync(p => p.ShopId == shop.Id && p.LessonKey == key);
        return Ok(ApiResponse<object>.Ok(new
        {
            lesson.Key, lesson.Title, lesson.Category, lesson.Minutes, lesson.Summary, lesson.Sections, lesson.DoThisWeek,
            Quiz = lesson.Quiz.Select(q => new { q.Question, q.Options }),
            Completed = progress != null,
            Score = progress?.QuizScore,
        }));
    }

    [HttpPost("lessons/{key}/quiz")]
    public async Task<IActionResult> SubmitQuiz(string key, [FromBody] LessonAnswersRequest req)
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        var lesson = ShopAcademyLessons.Find(key);
        if (lesson == null) return NotFound(ApiResponse.Fail("Lesson not found."));
        if (req.Answers == null || req.Answers.Length != lesson.Quiz.Length)
            return BadRequest(ApiResponse.Fail("Answer every question."));

        var correct = lesson.Quiz.Select((q, i) => q.Answer == req.Answers[i]).ToArray();
        var score = correct.Count(c => c);
        var passed = ShopAcademyLessons.Passed(score, lesson.Quiz.Length);
        if (passed)
        {
            var progress = await db.ShopLessonProgress.FirstOrDefaultAsync(p => p.ShopId == shop.Id && p.LessonKey == key);
            if (progress == null)
                db.ShopLessonProgress.Add(new ShopLessonProgress { ShopId = shop.Id, LessonKey = key, QuizScore = score, QuizTotal = lesson.Quiz.Length });
            else if (score > progress.QuizScore)
                progress.QuizScore = score;
            await db.SaveChangesAsync();
        }
        return Ok(ApiResponse<object>.Ok(new
        {
            Passed = passed, Score = score, Total = lesson.Quiz.Length, Correct = correct,
            Answers = lesson.Quiz.Select(q => q.Answer),
        }, passed ? "Lesson complete. Well done!" : "Not quite. Read the lesson again and retry."));
    }

    // ── Business Record PDF ──

    /// <summary>A short-lived link to the Business Record PDF, for opening in the phone's browser.</summary>
    [HttpGet("record-link")]
    public async Task<IActionResult> RecordLink()
    {
        var shop = await ShopAsync();
        if (shop == null) return NoShop();
        return Ok(ApiResponse<object>.Ok(new { Path = academy.RecordPath(shop.Id) }));
    }

    [AllowAnonymous]
    [HttpGet("record/{shopId:guid}")]
    public async Task<IActionResult> RecordPdf(Guid shopId, [FromQuery] long exp, [FromQuery] string? sig)
    {
        if (!academy.IsValidRecordLink(shopId, exp, sig))
            return Unauthorized(ApiResponse.Fail("This link has expired. Open the Business Record again from the app."));
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.Id == shopId);
        if (shop == null) return NotFound(ApiResponse.Fail("Shop not found."));
        var pdf = await BusinessRecordPdf.BuildAsync(db, academy, shop);
        return File(pdf, "application/pdf", $"SpazaSure-Business-Record-{shop.ShopCode ?? "shop"}.pdf");
    }

    // ── Helpers ──

    private static string? ValidateItem(StockItemRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Trim().Length > 200) return "Enter the product name.";
        if (req.CostPrice < 0 || req.CostPrice > MaxAmount || req.SellingPrice < 0 || req.SellingPrice > MaxAmount)
            return "Enter valid prices.";
        if (req.Quantity < 0 || req.Quantity > 100_000) return "Enter a valid quantity.";
        if (req.ReorderLevel < 0 || req.ReorderLevel > 100_000) return "Enter a valid reorder level.";
        return null;
    }

    private static void Fill(ShopStockItem item, StockItemRequest req)
    {
        item.Name = req.Name.Trim();
        item.Category = Clean(req.Category, 60);
        item.Unit = Clean(req.Unit, 30) ?? "unit";
        item.Barcode = Clean(req.Barcode, 50);
        item.CostPrice = Math.Round(req.CostPrice, 2);
        item.SellingPrice = Math.Round(req.SellingPrice, 2);
        item.Quantity = req.Quantity;
        item.ReorderLevel = req.ReorderLevel;
        item.ExpiryDate = req.ExpiryDate;
    }

    private static string? Clean(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }
}
