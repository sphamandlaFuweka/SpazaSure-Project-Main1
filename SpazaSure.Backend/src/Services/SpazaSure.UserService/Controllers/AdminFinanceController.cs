using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// One place for all of SpazaSure's money: what came in (wallet top-ups, Paystack order payments,
/// cash collected on delivery, onboarding fees, supplier subscriptions) and what went out (supplier payouts
/// and shop wallet withdrawals).
/// Customer subscriptions aren't listed because no payment record is stored for them.
/// </summary>
[ApiController]
[Route("api/admin/finance")]
[Authorize(Roles = "admin")]
public class AdminFinanceController(SpazaSureDbContext db) : ControllerBase
{
    private const string CashOnDelivery = "cash_on_delivery";

    public record FinanceEntry(
        string Id, DateTime Date, string Direction, string Category, decimal Amount,
        string Status, string? Reference, string Party, string Description);

    /// <summary>Totals for the period, plus what SpazaSure currently holds and owes.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (start, end) = Range(from, to);
        var entries = await LoadEntriesAsync(start, end, ct);
        var confirmed = entries.Where(e => e.Status == "confirmed").ToList();

        var byCategory = confirmed
            .GroupBy(e => e.Category)
            .Select(g => new { category = g.Key, direction = g.First().Direction, count = g.Count(), amount = g.Sum(e => e.Amount) })
            .OrderBy(g => g.direction).ThenByDescending(g => g.amount)
            .ToList();

        var moneyIn = confirmed.Where(e => e.Direction == "in").Sum(e => e.Amount);
        var moneyOut = confirmed.Where(e => e.Direction == "out").Sum(e => e.Amount);

        // SpazaSure keeps the commission/service fee on delivered orders, plus onboarding fees and subscriptions
        var commission = await db.Orders.AsNoTracking()
            .Where(o => o.Status == "delivered" && o.PaymentStatus == "paid" && o.DeliveredAt >= start && o.DeliveredAt < end)
            .SumAsync(o => (decimal?)o.PlatformCommission, ct) ?? 0m;
        var otherRevenue = confirmed.Where(e => e.Category is "onboarding_fee" or "supplier_subscription").Sum(e => e.Amount);

        var walletBalancesHeld = await db.ShopWalletLedgerEntries.AsNoTracking()
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var owedToSuppliers = await db.Orders.AsNoTracking()
            .Where(o => o.Status == "delivered" && o.PaymentStatus == "paid" && o.PaidOutAt == null)
            .SumAsync(o => (decimal?)(o.Subtotal + o.DeliveryFee), ct) ?? 0m;

        var pendingDeposits = await db.ShopWalletTransactions.AsNoTracking()
            .Where(t => t.Type == "top_up" && t.Status == "pending" && t.Method != "paystack")
            .Select(t => t.Amount).ToListAsync(ct);
        var pendingWithdrawals = await db.ShopWalletTransactions.AsNoTracking()
            .Where(t => t.Type == ShopWallet.Withdrawal && t.Status == "pending")
            .Select(t => t.Amount).ToListAsync(ct);
        var codOutstanding = await db.Orders.AsNoTracking()
            .Where(o => o.PaymentMethod == CashOnDelivery && o.Status != "cancelled" && o.PaymentStatus != "paid")
            .Select(o => o.TotalAmount).ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            from = start,
            to = end.AddDays(-1),
            moneyIn,
            moneyOut,
            net = moneyIn - moneyOut,
            platformEarnings = commission + otherRevenue,
            byCategory,
            walletBalancesHeld,
            owedToSuppliers,
            pendingDeposits = new { count = pendingDeposits.Count, amount = pendingDeposits.Sum() },
            pendingWithdrawals = new { count = pendingWithdrawals.Count, amount = pendingWithdrawals.Sum() },
            codOutstanding = new { count = codOutstanding.Count, amount = codOutstanding.Sum() },
            refundsDue = new
            {
                count = entries.Count(e => e.Status == "refund_due"),
                amount = entries.Where(e => e.Status == "refund_due").Sum(e => e.Amount)
            },
        }));
    }

    /// <summary>Every money movement in the period, newest first.</summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? direction, [FromQuery] string? category, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var (start, end) = Range(from, to);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = (await LoadEntriesAsync(start, end, ct)).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(direction)) query = query.Where(e => e.Direction == direction);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(e => e.Category == category);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.Status == status);

        var filtered = query.OrderByDescending(e => e.Date).ToList();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(ApiResponse<object>.Ok(new { total = filtered.Count, page, pageSize, items }));
    }

    /// <summary>The wallet ledger: every credit and debit on shop wallets, newest first. Optionally for one shop or shop name.</summary>
    [HttpGet("wallet-ledger")]
    public async Task<IActionResult> WalletLedger(
        [FromQuery] Guid? shopId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ShopWalletLedgerEntries.AsNoTracking().AsQueryable();
        if (shopId is not null) query = query.Where(e => e.ShopId == shopId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(e => e.Shop.ShopName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var balance = await query.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var items = await query
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new
            {
                e.Id, Date = e.CreatedAt, e.ShopId, ShopName = e.Shop.ShopName,
                e.Type, e.Description, e.Reference, e.Amount
            })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, balance, items }));
    }

    /// <summary>Defaults to the last 30 days; the end date is inclusive.</summary>
    private static (DateTime Start, DateTime EndExclusive) Range(DateTime? from, DateTime? to)
    {
        var end = DateTime.SpecifyKind((to ?? DateTime.UtcNow).Date.AddDays(1), DateTimeKind.Utc);
        var start = DateTime.SpecifyKind((from ?? end.AddDays(-30)).Date, DateTimeKind.Utc);
        return (start, end);
    }

    private async Task<List<FinanceEntry>> LoadEntriesAsync(DateTime start, DateTime end, CancellationToken ct)
    {
        var entries = new List<FinanceEntry>();

        // Wallet top-ups: only approved ones are money received; pending ones are awaiting a deposit
        var topUps = await db.ShopWalletTransactions.AsNoTracking()
            .Where(t => t.Type == "top_up" && (t.ApprovedAt ?? t.CreatedAt) >= start && (t.ApprovedAt ?? t.CreatedAt) < end)
            .Select(t => new { t.Id, Date = t.ApprovedAt ?? t.CreatedAt, t.Amount, t.Status, t.Method, t.Reference, t.Shop.ShopName })
            .ToListAsync(ct);
        entries.AddRange(topUps.Select(t => new FinanceEntry(
            $"topup-{t.Id}", t.Date, "in", "wallet_top_up", t.Amount,
            t.Status == "approved" ? "confirmed" : t.Status, t.Reference, t.ShopName,
            t.Method == "paystack" ? "Wallet top-up (Paystack)" : "Wallet top-up (bank deposit)")));

        // Orders paid online. A cancelled paid order means the money is still held and may need refunding.
        var paystackOrders = await db.Orders.AsNoTracking()
            .Where(o => o.PaymentMethod == "paystack" && o.PaymentStatus == "paid" && o.CreatedAt >= start && o.CreatedAt < end)
            .Select(o => new { o.Id, o.CreatedAt, o.TotalAmount, o.Status, o.OrderNumber, o.PaymentProviderReference, o.Shop.ShopName })
            .ToListAsync(ct);
        entries.AddRange(paystackOrders.Select(o => new FinanceEntry(
            $"order-{o.Id}", o.CreatedAt, "in", "order_paystack", o.TotalAmount,
            o.Status == "cancelled" ? "refund_due" : "confirmed", o.PaymentProviderReference ?? o.OrderNumber,
            o.ShopName, $"Order {o.OrderNumber} paid by Paystack")));

        // Cash on delivery: received once the shop confirms delivery; until then it is still to be collected
        var codOrders = await db.Orders.AsNoTracking()
            .Where(o => o.PaymentMethod == CashOnDelivery && o.Status != "cancelled"
                && (o.PaymentStatus == "paid" ? o.DeliveredAt ?? o.CreatedAt : o.CreatedAt) >= start
                && (o.PaymentStatus == "paid" ? o.DeliveredAt ?? o.CreatedAt : o.CreatedAt) < end)
            .Select(o => new { o.Id, o.CreatedAt, o.DeliveredAt, o.TotalAmount, o.PaymentStatus, o.OrderNumber, o.Shop.ShopName })
            .ToListAsync(ct);
        entries.AddRange(codOrders.Select(o => new FinanceEntry(
            $"cod-{o.Id}", o.PaymentStatus == "paid" ? o.DeliveredAt ?? o.CreatedAt : o.CreatedAt,
            "in", "order_cash_on_delivery", o.TotalAmount, o.PaymentStatus == "paid" ? "confirmed" : "pending",
            o.OrderNumber, o.ShopName,
            o.PaymentStatus == "paid" ? $"Cash collected on delivery of {o.OrderNumber}" : $"Cash still to collect for {o.OrderNumber}")));

        var onboarding = await db.ShopOnboardingPayments.AsNoTracking()
            .Where(p => p.Status == "completed" && p.CompletedAt >= start && p.CompletedAt < end)
            .Select(p => new { p.Id, p.CompletedAt, p.Amount, p.ProviderPaymentId, p.Shop.ShopName })
            .ToListAsync(ct);
        entries.AddRange(onboarding.Select(p => new FinanceEntry(
            $"onboarding-{p.Id}", p.CompletedAt!.Value, "in", "onboarding_fee", p.Amount, "confirmed",
            p.ProviderPaymentId, p.ShopName, "Shop onboarding fee")));

        var subscriptions = await db.SupplierSubscriptions.AsNoTracking()
            .Where(s => s.AmountPaid > 0 && s.StartDate >= start && s.StartDate < end)
            .Select(s => new { s.Id, s.StartDate, s.AmountPaid, s.PaymentReference, s.BillingCycle, Plan = s.Plan.Name, s.Supplier.CompanyName })
            .ToListAsync(ct);
        entries.AddRange(subscriptions.Select(s => new FinanceEntry(
            $"subscription-{s.Id}", s.StartDate, "in", "supplier_subscription", s.AmountPaid, "confirmed",
            s.PaymentReference, s.CompanyName, $"{s.Plan} plan ({s.BillingCycle})")));

        // Supplier payouts are recorded per batch: one PaidOutAt and reference per supplier payout
        var paidOrders = await db.Orders.AsNoTracking()
            .Where(o => o.PaidOutAt != null && o.PaidOutAt >= start && o.PaidOutAt < end)
            .Select(o => new { o.SupplierId, o.PaidOutAt, o.PayoutReference, o.Subtotal, o.DeliveryFee, o.Supplier.CompanyName })
            .ToListAsync(ct);
        entries.AddRange(paidOrders
            .GroupBy(o => (o.SupplierId, o.PaidOutAt, o.PayoutReference))
            .Select(g => new FinanceEntry(
                $"payout-{g.Key.SupplierId}-{g.Key.PaidOutAt:O}", g.Key.PaidOutAt!.Value, "out", "supplier_payout",
                g.Sum(o => o.Subtotal + o.DeliveryFee), "confirmed", g.Key.PayoutReference,
                g.First().CompanyName, $"Payout for {g.Count()} delivered order{(g.Count() == 1 ? "" : "s")}")));

        // Shop withdrawals: money paid out of wallets into shops' bank accounts
        var withdrawals = await db.ShopWalletTransactions.AsNoTracking()
            .Where(t => t.Type == ShopWallet.Withdrawal && (t.ApprovedAt ?? t.CreatedAt) >= start && (t.ApprovedAt ?? t.CreatedAt) < end)
            .Select(t => new { t.Id, Date = t.ApprovedAt ?? t.CreatedAt, t.Amount, t.Status, t.Reference, t.Shop.ShopName })
            .ToListAsync(ct);
        entries.AddRange(withdrawals.Select(t => new FinanceEntry(
            $"withdrawal-{t.Id}", t.Date, "out", "shop_withdrawal", t.Amount,
            t.Status == "paid" ? "confirmed" : t.Status, t.Reference, t.ShopName, "Wallet withdrawal to bank account")));

        return entries;
    }
}
