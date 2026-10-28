using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.UserService.Services;

public record ReadinessItem(string Key, string Label, bool Done, int Points, int Earned, string Tip);
public record MonthSummary(
    string Month, decimal Income, decimal StockPurchases, decimal OperatingExpenses,
    decimal NetProfit, decimal StockLosses, int DaysRecorded);
public record StockSummary(int Items, int Units, decimal ValueAtCost, decimal ValueAtSelling, int LowStock, int ExpiringSoon);

/// <summary>
/// The figures behind Shop Academy: stock book changes, monthly trading summaries and the
/// funding readiness score. Shared by the shop's API and the Business Record PDF.
/// </summary>
public class ShopAcademyService(SpazaSureDbContext db, IConfiguration config)
{
    public static readonly string[] MovementTypes = ["received", "sold", "spoiled", "stolen", "own_use", "count"];
    public static readonly string[] LossTypes = ["spoiled", "stolen"];
    public static readonly string[] IncomeCategories = ["cash_sales", "card_sales", "airtime_electricity", "other_income"];
    public static readonly string[] ExpenseCategories =
        ["stock_purchase", "rent", "electricity", "transport", "wages", "airtime_data", "repairs", "licences_fees", "owner_salary", "other_expense"];
    public const int ExpiringSoonDays = 14;
    /// <summary>A month counts towards the trading record once this many days have entries.</summary>
    public const int DaysForRecordedMonth = 8;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2)); // South African time

    // ── Stock book ──

    /// <summary>
    /// Applies a stock movement to the item. Quantity is entered as a positive number; "count"
    /// means the quantity counted on the shelf, and the difference is recorded.
    /// </summary>
    public static ShopStockMovement? Apply(ShopStockItem item, string type, int quantity, decimal? unitCost, decimal? unitPrice,
        string? note, DateOnly occurredOn)
    {
        var change = type switch
        {
            "received" => quantity,
            "count" => quantity - item.Quantity,
            _ => -quantity,
        };
        if (type == "count" && change == 0) return null;
        if (type == "received" && unitCost is > 0) item.CostPrice = unitCost.Value;
        item.Quantity = Math.Max(0, item.Quantity + change);
        return new ShopStockMovement
        {
            ShopId = item.ShopId,
            StockItemId = item.Id,
            Type = type,
            Quantity = change,
            UnitCost = unitCost ?? item.CostPrice,
            UnitPrice = unitPrice ?? item.SellingPrice,
            Note = note,
            OccurredOn = occurredOn,
        };
    }

    /// <summary>Selling price suggested for stock bought through SpazaSure: cost plus 25%.</summary>
    public static decimal SuggestedPrice(decimal cost) => Math.Ceiling(cost * 1.25m * 2) / 2;

    /// <summary>
    /// Books delivered SpazaSure orders into the stock book (and the cost into the cash book), so
    /// stock bought on SpazaSure doesn't have to be typed in. Each order is only booked once.
    /// </summary>
    public async Task<(int Orders, int Lines)> ImportDeliveredOrdersAsync(Guid shopId)
    {
        var booked = await db.ShopStockMovements
            .Where(m => m.ShopId == shopId && m.SourceOrderId != null)
            .Select(m => m.SourceOrderId!.Value).Distinct().ToListAsync();
        var orders = await db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category)
            .Where(o => o.ShopId == shopId && o.Status == "delivered" && !booked.Contains(o.Id))
            .OrderBy(o => o.DeliveredAt ?? o.CreatedAt)
            .ToListAsync();
        if (orders.Count == 0) return (0, 0);

        var items = await db.ShopStockItems.Where(i => i.ShopId == shopId && i.ProductId != null).ToListAsync();
        var lines = 0;
        foreach (var order in orders)
        {
            var day = DateOnly.FromDateTime((order.DeliveredAt ?? order.CreatedAt).AddHours(2));
            foreach (var line in order.Items)
            {
                // What the shop paid per unit (SpazaSure's fee included), not the supplier's price
                var cost = line.ShopUnitPrice > 0 ? line.ShopUnitPrice : line.UnitPrice;
                var item = items.FirstOrDefault(i => i.ProductId == line.ProductId);
                if (item == null)
                {
                    item = new ShopStockItem
                    {
                        ShopId = shopId,
                        ProductId = line.ProductId,
                        Name = line.Product.Name,
                        Category = line.Product.Category?.Name,
                        Unit = line.Product.Unit,
                        Barcode = line.Product.Barcode,
                        SellingPrice = SuggestedPrice(cost),
                    };
                    db.ShopStockItems.Add(item);
                    items.Add(item);
                }
                item.IsActive = true;
                var movement = Apply(item, "received", line.Quantity, cost, null, $"SpazaSure order {order.OrderNumber}", day)!;
                movement.SourceOrderId = order.Id;
                db.ShopStockMovements.Add(movement);
                lines++;
            }
            db.ShopCashEntries.Add(new ShopCashEntry
            {
                ShopId = shopId, Direction = "expense", Category = "stock_purchase", Amount = order.TotalAmount,
                Note = $"SpazaSure order {order.OrderNumber}", OccurredOn = day,
            });
        }
        await db.SaveChangesAsync();
        return (orders.Count, lines);
    }

    public async Task<StockSummary> StockAsync(Guid shopId)
    {
        var items = await db.ShopStockItems.Where(i => i.ShopId == shopId && i.IsActive).ToListAsync();
        var soon = Today.AddDays(ExpiringSoonDays);
        return new StockSummary(
            items.Count,
            items.Sum(i => i.Quantity),
            items.Sum(i => i.Quantity * i.CostPrice),
            items.Sum(i => i.Quantity * i.SellingPrice),
            items.Count(i => i.Quantity <= i.ReorderLevel),
            items.Count(i => i.ExpiryDate is { } d && d <= soon && i.Quantity > 0));
    }

    // ── Trading summaries ──

    /// <summary>One row per month, oldest first, ending with the current month.</summary>
    public async Task<List<MonthSummary>> MonthsAsync(Guid shopId, int months)
    {
        var first = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-(months - 1));
        var cash = await db.ShopCashEntries.Where(c => c.ShopId == shopId && c.OccurredOn >= first).ToListAsync();
        var losses = await db.ShopStockMovements
            .Where(m => m.ShopId == shopId && m.OccurredOn >= first && LossTypes.Contains(m.Type))
            .ToListAsync();
        var stockDays = await db.ShopStockMovements
            .Where(m => m.ShopId == shopId && m.OccurredOn >= first && m.SourceOrderId == null)
            .Select(m => m.OccurredOn).Distinct().ToListAsync();

        var rows = new List<MonthSummary>();
        for (var m = first; m <= Today; m = m.AddMonths(1))
        {
            bool InMonth(DateOnly d) => d.Year == m.Year && d.Month == m.Month;
            var month = cash.Where(c => InMonth(c.OccurredOn)).ToList();
            var income = month.Where(c => c.Direction == "income").Sum(c => c.Amount);
            var stock = month.Where(c => c.Direction == "expense" && c.Category == "stock_purchase").Sum(c => c.Amount);
            var operating = month.Where(c => c.Direction == "expense" && c.Category != "stock_purchase").Sum(c => c.Amount);
            var days = month.Select(c => c.OccurredOn).Concat(stockDays.Where(InMonth)).Distinct().Count();
            rows.Add(new MonthSummary(
                $"{m:yyyy-MM}", income, stock, operating, income - stock - operating,
                losses.Where(l => InMonth(l.OccurredOn)).Sum(l => -l.Quantity * l.UnitCost), days));
        }
        return rows;
    }

    /// <summary>Months (of the last 12) with enough entries to count as a trading record.</summary>
    public static int RecordedMonths(IEnumerable<MonthSummary> months) => months.Count(m => m.DaysRecorded >= DaysForRecordedMonth);

    // ── Funding readiness ──

    public static int ProfileCompleteness(ShopBusinessProfile? p)
    {
        if (p == null) return 0;
        var fields = new bool[]
        {
            !string.IsNullOrWhiteSpace(p.LegalStructure),
            p.StartedTradingOn != null,
            !string.IsNullOrWhiteSpace(p.PremisesType),
            !string.IsNullOrWhiteSpace(p.TradingHours),
            p.FullTimeStaff + p.PartTimeStaff > 0,
            !string.IsNullOrWhiteSpace(p.BankName),
            !string.IsNullOrWhiteSpace(p.MainSuppliers),
            !string.IsNullOrWhiteSpace(p.Goals),
        };
        return fields.Count(f => f) * 100 / fields.Length;
    }

    public async Task<List<ReadinessItem>> ReadinessAsync(SpazaShop shop, ShopBusinessProfile? profile, List<MonthSummary> lastThreeMonths, StockSummary stock)
    {
        var lessonsDone = await db.ShopLessonProgress.CountAsync(p => p.ShopId == shop.Id);
        var lessonsTotal = ShopAcademyLessons.All.Length;
        var recorded = RecordedMonths(lastThreeMonths);
        var completeness = ProfileCompleteness(profile);

        ReadinessItem Item(string key, string label, int points, int earned, string tip) =>
            new(key, label, earned >= points, points, earned, tip);

        return
        [
            Item("verified", "SpazaSure verified shop", 10, shop.Status == "verified" ? 10 : 0,
                "Finish your SpazaSure verification so funders can confirm your shop is real."),
            Item("profile", "Business details filled in", 15, completeness * 15 / 100,
                "Fill in every field under Business details."),
            Item("cipc", "Registered with CIPC", 10, string.IsNullOrWhiteSpace(profile?.CipcRegistrationNumber) ? 0 : 10,
                "Register your business at bizportal.gov.za and add the number."),
            Item("sars", "SARS tax number", 10, string.IsNullOrWhiteSpace(profile?.SarsTaxNumber) ? 0 : 10,
                "Register with SARS for income or turnover tax and add your tax number."),
            Item("bank", "Separate business bank account", 10, profile?.HasBusinessBankAccount == true ? 10 : 0,
                "Open a bank account only for the shop; its statements prove your sales."),
            Item("records", "3 months of cash book records", 25, recorded * 25 / 3,
                $"Record takings and expenses on at least {DaysForRecordedMonth} days every month."),
            Item("stock", "Stock book in use (10+ items)", 10, Math.Min(stock.Items, 10),
                "Add your products to the stock book, or book in your SpazaSure deliveries."),
            Item("lessons", "Shop Academy lessons completed", 10, lessonsDone * 10 / lessonsTotal,
                "Work through the lessons and pass each quiz."),
        ];
    }

    // ── Signed Business Record links, so the app can open the PDF in the phone's browser ──

    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(30);

    private byte[] Key => SHA256.HashData(Encoding.UTF8.GetBytes("spazasure-business-record:" + config["Jwt:Secret"]));

    /// <summary>Path below the API root (e.g. /shop/academy/record/...), for the app to put after its API address.</summary>
    public string RecordPath(Guid shopId)
    {
        var expires = DateTimeOffset.UtcNow.Add(LinkLifetime).ToUnixTimeSeconds();
        return $"/shop/academy/record/{shopId}?exp={expires}&sig={Sign(shopId, expires)}";
    }

    public bool IsValidRecordLink(Guid shopId, long exp, string? sig)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp || string.IsNullOrEmpty(sig)) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(shopId, exp)), Convert.FromHexString(sig));
        }
        catch (FormatException) { return false; }
    }

    private string Sign(Guid shopId, long expires) =>
        Convert.ToHexString(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes($"{shopId:N}\n{expires}"))).ToLowerInvariant();
}
