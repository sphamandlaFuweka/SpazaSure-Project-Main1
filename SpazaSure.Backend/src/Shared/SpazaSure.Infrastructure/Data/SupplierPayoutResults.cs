using Microsoft.EntityFrameworkCore;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// The outcome of an automatic supplier payout (a Paystack transfer), from the transfer webhook or
/// from checking a transfer that has been pending a while.
/// </summary>
public static class SupplierPayoutResults
{
    /// <summary>
    /// Marks a payout reference whose transfer failed. Those orders aren't retried automatically
    /// (every attempt costs a transfer fee) until the supplier updates their bank details or an
    /// admin retries.
    /// </summary>
    public const string FailedPrefix = "FAILED:";

    public static bool IsFailed(string? payoutReference) =>
        payoutReference?.StartsWith(FailedPrefix, StringComparison.Ordinal) == true;

    /// <summary>The reason a payout failed, for showing to admins.</summary>
    public static string? FailureReason(string? payoutReference) =>
        IsFailed(payoutReference) ? payoutReference![FailedPrefix.Length..] : null;

    public static async Task ApplyAsync(
        SpazaSureDbContext db, EventPublisher events, string reference, bool succeeded, string? reason, CancellationToken ct)
    {
        var orders = await db.Orders.Include(o => o.Supplier)
            .Where(o => o.PayoutReference == reference && o.PaidOutAt == null)
            .ToListAsync(ct);
        if (orders.Count == 0) return; // already applied, or unknown

        var supplier = orders[0].Supplier;
        var total = orders.Sum(o => o.Subtotal);
        var now = DateTime.UtcNow;
        if (succeeded)
        {
            foreach (var o in orders)
            {
                o.PaidOutAt = now;
                o.UpdatedAt = now;
            }
            await db.SaveChangesAsync(ct);
            events.PublishNotification(
                supplierId: supplier.UserId.ToString(),
                type: "order",
                title: "Payout sent",
                message: $"SpazaSure paid you R{total:N2} for {orders.Count} delivered order{(orders.Count == 1 ? "" : "s")}. It reaches your bank account shortly. Reference: {reference}.",
                referenceId: reference,
                routingKey: "payout.paid");
            return;
        }

        var why = string.IsNullOrWhiteSpace(reason) ? "Paystack could not complete the transfer" : reason.Trim();
        var marker = FailedPrefix + (why.Length > 180 ? why[..180] : why);
        foreach (var o in orders)
        {
            o.PayoutInitiatedAt = null;
            o.PayoutReference = marker;
            o.UpdatedAt = now;
        }
        // The bank details may be wrong; a new recipient is created once they are fixed.
        supplier.PaystackRecipientCode = null;
        await db.SaveChangesAsync(ct);

        events.PublishNotification(
            supplierId: supplier.UserId.ToString(),
            type: "order",
            title: "Payout could not be sent",
            message: $"We couldn't send your payout of R{total:N2}: {why}. Please check your banking details under Profile → Banking; we'll try again once they're updated.",
            priority: "high",
            referenceId: reference,
            routingKey: "payout.failed");
        await AdminAlerts.NotifyAdminsAsync(db, events, "finance", "Supplier payout failed",
            $"The payout of R{total:N2} to {supplier.CompanyName} failed: {why}. It is retried when they update their bank details, or from the Payouts page.",
            supplier.Id.ToString());
    }

    /// <summary>Lets a supplier's failed payouts be tried again (bank details updated, or an admin retried).</summary>
    public static async Task<int> ClearFailuresAsync(SpazaSureDbContext db, Guid supplierId, CancellationToken ct)
    {
        var failed = await db.Orders
            .Where(o => o.SupplierId == supplierId && o.PaidOutAt == null && o.PayoutReference != null &&
                        o.PayoutReference.StartsWith(FailedPrefix))
            .ToListAsync(ct);
        foreach (var o in failed) o.PayoutReference = null;
        return failed.Count;
    }
}
