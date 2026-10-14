using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// A shop's prepaid SpazaSure wallet. Shops top it up (Paystack, or a direct bank deposit
/// that an admin approves) and can then pay for orders from it at checkout.
/// </summary>
public static class ShopWallet
{
    /// <summary>Order.PaymentMethod for orders paid from the wallet.</summary>
    public const string PaymentMethod = "wallet";
    /// <summary>ShopWalletTransaction.Method for direct deposits into SpazaSure's bank account.</summary>
    public const string DirectDeposit = "eft";

    /// <summary>ShopWalletTransaction.Type for money a shop takes out of its wallet to its bank account.</summary>
    public const string Withdrawal = "withdrawal";

    /// <summary>Ledger entry types.</summary>
    public static class Entry
    {
        public const string TopUp = "top_up";
        public const string TopUpReversal = "top_up_reversal";
        public const string OrderPayment = "order_payment";
        public const string OrderRefund = "order_refund";
        public const string WithdrawalDebit = "withdrawal";
        public const string WithdrawalReversal = "withdrawal_reversal";
    }

    /// <summary>The wallet balance: the sum of every ledger entry.</summary>
    public static async Task<decimal> BalanceAsync(SpazaSureDbContext db, Guid shopId, CancellationToken ct = default) =>
        await db.ShopWalletLedgerEntries
            .Where(e => e.ShopId == shopId)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

    /// <summary>
    /// Makes the ledger say that <paramref name="sourceId"/> (a top-up, order or withdrawal) is worth
    /// <paramref name="desiredAmount"/> to the wallet, by adding a correcting entry for the difference.
    /// Safe to call repeatedly; nothing is added when the ledger is already right. Does not save.
    /// </summary>
    public static async Task SyncAsync(
        SpazaSureDbContext db, Guid shopId, Guid sourceId, decimal desiredAmount,
        string creditType, string debitType, string description, string? reference = null,
        CancellationToken ct = default)
    {
        var current = await db.ShopWalletLedgerEntries
            .Where(e => e.SourceId == sourceId)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var delta = desiredAmount - current;
        if (delta == 0m) return;
        db.ShopWalletLedgerEntries.Add(new ShopWalletLedgerEntry
        {
            ShopId = shopId,
            Amount = delta,
            Type = delta > 0 ? creditType : debitType,
            SourceId = sourceId,
            Description = description,
            Reference = reference,
        });
    }

    /// <summary>An approved top-up credits the wallet; any other status leaves it untouched.</summary>
    public static Task SyncTopUpAsync(SpazaSureDbContext db, ShopWalletTransaction t, CancellationToken ct = default) =>
        SyncAsync(db, t.ShopId, t.Id, t.Status == "approved" ? t.Amount : 0m,
            Entry.TopUp, Entry.TopUpReversal,
            t.Method == DirectDeposit ? "Direct deposit"
                : t.Method == "paystack" ? "Wallet top-up (Paystack)" : "Wallet top-up (" + t.Method + ")",
            t.Reference, ct);

    /// <summary>A wallet-paid order debits the wallet until it is cancelled, then it is refunded.</summary>
    public static Task SyncOrderAsync(SpazaSureDbContext db, Order o, CancellationToken ct = default) =>
        o.PaymentMethod != PaymentMethod
            ? Task.CompletedTask
            : SyncAsync(db, o.ShopId, o.Id, o.Status == "cancelled" ? 0m : -o.TotalAmount,
                Entry.OrderRefund, Entry.OrderPayment,
                (o.Status == "cancelled" ? "Refund for cancelled order " : "Order ") + o.OrderNumber,
                o.OrderNumber, ct);

    /// <summary>Withdrawal statuses that hold the money out of the wallet.</summary>
    public static bool HoldsFunds(string status) => status is "pending" or "paid";

    /// <summary>A pending or paid withdrawal debits the wallet; a rejected or cancelled one gives the money back.</summary>
    public static Task SyncWithdrawalAsync(SpazaSureDbContext db, ShopWalletTransaction t, CancellationToken ct = default) =>
        SyncAsync(db, t.ShopId, t.Id, HoldsFunds(t.Status) ? -t.Amount : 0m,
            Entry.WithdrawalReversal, Entry.WithdrawalDebit,
            t.Status is "rejected" or "cancelled" ? "Withdrawal returned to wallet" : "Withdrawal to bank account",
            t.Reference, ct);

    /// <summary>
    /// Locks the shop's row until the current transaction ends, so two checkouts at the
    /// same time can't both spend the same balance. No-op for non-relational test databases.
    /// </summary>
    public static Task LockAsync(SpazaSureDbContext db, Guid shopId, CancellationToken ct = default) =>
        db.Database.IsRelational()
            ? db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM spaza_shops WHERE id = {shopId} FOR UPDATE", ct)
            : Task.CompletedTask;

    /// <summary>Short reference the shop puts on its deposit so an admin can match it, e.g. "SS3F9A1C2B".</summary>
    public static string DepositReference(Guid transactionId) => "SS" + transactionId.ToString("N")[..8].ToUpperInvariant();
}
