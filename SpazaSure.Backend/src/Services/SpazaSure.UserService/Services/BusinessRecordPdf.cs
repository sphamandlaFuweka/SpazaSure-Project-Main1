using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.UserService.Services;

/// <summary>
/// The Shop Academy Business Record: an A4 PDF a spaza owner can hand to a funder or bank. It
/// separates what SpazaSure can confirm (verification, purchases through the platform) from what
/// the owner recorded themselves (cash book, stock book, business details).
/// </summary>
public static class BusinessRecordPdf
{
    private static readonly CultureInfo Za = CultureInfo.GetCultureInfo("en-ZA");
    private static string R(decimal amount) => "R " + amount.ToString("N2", Za);
    private static readonly Color Blue = Color.FromHex("#25449A");
    private static readonly Color Muted = Colors.Grey.Darken1;

    private static readonly Dictionary<string, string> LegalNames = new()
    {
        ["sole_proprietor"] = "Sole proprietor", ["partnership"] = "Partnership",
        ["pty_ltd"] = "Private company (Pty) Ltd", ["cooperative"] = "Co-operative",
    };

    public static async Task<byte[]> BuildAsync(SpazaSureDbContext db, ShopAcademyService academy, SpazaShop shop)
    {
        var profile = await db.ShopBusinessProfiles.FirstOrDefaultAsync(p => p.ShopId == shop.Id);
        var months = await academy.MonthsAsync(shop.Id, 6);
        var stock = await academy.StockAsync(shop.Id);
        var readiness = await academy.ReadinessAsync(shop, profile, months.TakeLast(3).ToList(), stock);
        var since = DateTime.UtcNow.AddMonths(-12);
        var purchases = await db.Orders
            .Where(o => o.ShopId == shop.Id && o.Status == "delivered" && o.CreatedAt >= since)
            .Select(o => new { o.TotalAmount, o.CreatedAt })
            .ToListAsync();
        var firstOrder = await db.Orders.Where(o => o.ShopId == shop.Id && o.Status == "delivered")
            .OrderBy(o => o.CreatedAt).Select(o => (DateTime?)o.CreatedAt).FirstOrDefaultAsync();
        var lessonKeys = await db.ShopLessonProgress.Where(p => p.ShopId == shop.Id).Select(p => p.LessonKey).ToListAsync();
        var lessons = ShopAcademyLessons.All.Where(l => lessonKeys.Contains(l.Key)).Select(l => l.Title).ToList();
        var generated = DateTime.UtcNow.AddHours(2);

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Color.FromHex("#17244F")));

            page.Header().Column(col =>
            {
                col.Item().Text("BUSINESS RECORD").FontSize(20).Bold().FontColor(Blue);
                col.Item().Text(shop.ShopName).FontSize(13).SemiBold();
                col.Item().Text($"SpazaSure Shop Academy · generated {generated.ToString("d MMMM yyyy", Za)}").FontColor(Muted);
            });

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(12);

                Section(col, "1. Confirmed by SpazaSure", c =>
                {
                    Pair(c, "Shop number", shop.ShopCode ?? "-");
                    Pair(c, "Verification status", Words(shop.Status));
                    Pair(c, "Compliance status", Words(shop.ComplianceStatus));
                    Pair(c, "Owner identity check", shop.IdentityCheckStatus ?? "Not done");
                    Pair(c, "On SpazaSure since", shop.CreatedAt.ToString("d MMMM yyyy", Za));
                    Pair(c, "Customer rating", shop.RatingCount > 0 ? $"{shop.RatingAvg:0.0} / 5 ({shop.RatingCount} reviews)" : "No reviews yet");
                    Pair(c, "SpazaSure purchases (12 months)", $"{R(purchases.Sum(p => p.TotalAmount))} over {purchases.Count} delivered orders");
                    Pair(c, "First SpazaSure delivery", firstOrder?.ToString("d MMMM yyyy", Za) ?? "-");
                });

                Section(col, "2. Business details (provided by the owner)", c =>
                {
                    Pair(c, "Owner", shop.OwnerName);
                    Pair(c, "Address", string.Join(", ", new[] { shop.Address, shop.City, shop.Province }.Where(s => !string.IsNullOrWhiteSpace(s))));
                    Pair(c, "Phone", shop.Phone);
                    if (profile == null)
                    {
                        c.Item().Text("The owner has not filled in business details yet.").Italic().FontColor(Muted);
                        return;
                    }
                    Pair(c, "Business type", LegalNames.GetValueOrDefault(profile.LegalStructure, profile.LegalStructure));
                    Pair(c, "CIPC registration", profile.CipcRegistrationNumber ?? "Not registered");
                    Pair(c, "SARS tax number", profile.SarsTaxNumber ?? "-");
                    if (profile.VatNumber != null) Pair(c, "VAT number", profile.VatNumber);
                    if (profile.TradingLicenceNumber != null) Pair(c, "Trading permit", profile.TradingLicenceNumber);
                    Pair(c, "Trading since", profile.StartedTradingOn?.ToString("MMMM yyyy", Za) ?? "-");
                    Pair(c, "Premises", Words(profile.PremisesType) + (profile.MonthlyRent is { } rent ? $" ({R(rent)} rent a month)" : ""));
                    Pair(c, "Staff", $"{profile.FullTimeStaff} full-time, {profile.PartTimeStaff} part-time");
                    Pair(c, "Trading hours", $"{profile.TradingHours ?? "-"} · {profile.TradingDaysPerWeek} days a week");
                    Pair(c, "Business bank account", profile.HasBusinessBankAccount ? $"Yes{(profile.BankName != null ? $" ({profile.BankName})" : "")}" : "No");
                    Pair(c, "Card payments", profile.AcceptsCardPayments ? "Yes" : "No");
                    if (profile.MainSuppliers != null) Pair(c, "Main suppliers", profile.MainSuppliers);
                    if (profile.Goals != null) Pair(c, "Plans and goals", profile.Goals);
                });

                Section(col, "3. Trading summary, last 6 months (owner's cash book)", c =>
                {
                    c.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.1f);
                            for (var i = 0; i < 5; i++) cols.RelativeColumn();
                            cols.RelativeColumn(0.7f);
                        });
                        foreach (var h in new[] { "Month", "Money in", "Stock bought", "Other costs", "Net", "Stock losses", "Days" })
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).Text(h).SemiBold();
                        foreach (var m in months)
                        {
                            var label = DateTime.ParseExact(m.Month, "yyyy-MM", CultureInfo.InvariantCulture).ToString("MMM yyyy", Za);
                            foreach (var v in new[] { label, R(m.Income), R(m.StockPurchases), R(m.OperatingExpenses), R(m.NetProfit), R(m.StockLosses), m.DaysRecorded.ToString() })
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3).Text(v);
                        }
                        foreach (var v in new[] { "Total", R(months.Sum(m => m.Income)), R(months.Sum(m => m.StockPurchases)),
                                     R(months.Sum(m => m.OperatingExpenses)), R(months.Sum(m => m.NetProfit)), R(months.Sum(m => m.StockLosses)), "" })
                            table.Cell().PaddingVertical(3).Text(v).Bold();
                    });
                    c.Item().Text($"{ShopAcademyService.RecordedMonths(months)} of these months have records on {ShopAcademyService.DaysForRecordedMonth} or more days.")
                        .FontColor(Muted);
                });

                Section(col, "4. Stock on hand (owner's stock book)", c =>
                {
                    Pair(c, "Products tracked", stock.Items.ToString());
                    Pair(c, "Units on the shelves", stock.Units.ToString("N0", Za));
                    Pair(c, "Stock value at cost", R(stock.ValueAtCost));
                    Pair(c, "Stock value at selling price", R(stock.ValueAtSelling));
                });

                Section(col, "5. Training and funding readiness", c =>
                {
                    Pair(c, "Shop Academy lessons passed", $"{lessons.Count} of {ShopAcademyLessons.All.Length}");
                    if (lessons.Count > 0) c.Item().Text(string.Join(" · ", lessons)).FontColor(Muted);
                    Pair(c, "Funding readiness score", $"{readiness.Sum(r => r.Earned)} / 100");
                    c.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); });
                        foreach (var r in readiness)
                            table.Cell().PaddingVertical(1.5f).Text($"{(r.Done ? "✓" : "○")}  {r.Label} ({r.Earned}/{r.Points})")
                                .FontColor(r.Done ? Color.FromHex("#078C50") : Muted);
                    });
                });
            });

            page.Footer().Column(col =>
            {
                col.Item().Text("Section 1 is confirmed from SpazaSure's records. Sections 2 to 5 come from the shop owner's own records in the " +
                                "SpazaSure app and have not been audited.").FontSize(7.5f).FontColor(Muted);
                col.Item().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Section(ColumnDescriptor col, string title, Action<ColumnDescriptor> body)
    {
        // A section moves to the next page whole rather than splitting
        col.Item().ShowEntire().Column(c =>
        {
            c.Spacing(3);
            c.Item().PaddingBottom(2).BorderBottom(1).BorderColor(Blue).Text(title).FontSize(11).Bold().FontColor(Blue);
            body(c);
        });
    }

    /// <summary>"in_review" becomes "In review".</summary>
    private static string Words(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : char.ToUpperInvariant(value[0]) + value[1..].Replace('_', ' ');

    private static void Pair(ColumnDescriptor c, string label, string value) =>
        c.Item().Row(row =>
        {
            row.ConstantItem(170).Text(label).FontColor(Muted);
            row.RelativeItem().Text(value);
        });
}
