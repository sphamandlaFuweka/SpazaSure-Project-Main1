using Microsoft.EntityFrameworkCore;

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

    /// <summary>
    /// Approved top-ups minus orders paid from the wallet. Cancelled orders don't count,
    /// so cancelling a wallet-paid order refunds it to the wallet automatically.
    /// </summary>
    public static async Task<decimal> BalanceAsync(SpazaSureDbContext db, Guid shopId, CancellationToken ct = default)
    {
        var credits = await db.ShopWalletTransactions
            .Where(t => t.ShopId == shopId && t.Type == "top_up" && t.Status == "approved")
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        var debits = await db.Orders
            .Where(o => o.ShopId == shopId && o.PaymentMethod == PaymentMethod && o.Status != "cancelled")
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        return credits - debits;
    }

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
