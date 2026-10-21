using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.OrderService.Services;

/// <summary>
/// Pays suppliers automatically through Paystack Transfers once their delivered orders have passed
/// the payout waiting period. One transfer per supplier per run, covering everything available.
/// The orders are marked "payout started" before the transfer is sent, so a crash never pays twice;
/// the transfer webhook (or a check after two hours) marks them paid out, or failed.
/// </summary>
public static class AutomaticPayouts
{
    public static readonly TimeSpan CheckPendingAfter = TimeSpan.FromHours(2);
    private static long _lastBalanceAlertTicks;
    private static long _lastOtpAlertTicks;

    public sealed record RunResult(int Started, int Skipped, int Settled);

    public static async Task<RunResult> RunOnceAsync(
        SpazaSureDbContext db, PaystackTransferService transfers, EventPublisher events, DateTime now, CancellationToken ct)
    {
        var settled = await SettleStuckTransfersAsync(db, transfers, events, now, ct);

        var rules = await PlatformSettingsStore.LoadAsync(db);
        var cutoff = now.AddDays(-rules.PayoutDelayDays);
        var candidates = await PayoutRules.Settleable(db.Orders)
            .Where(o => o.PaidOutAt == null && o.PayoutInitiatedAt == null && o.Status == "delivered" && o.DeliveredAt != null)
            .Include(o => o.Supplier)
            .ToListAsync(ct);

        int started = 0, skipped = 0;
        foreach (var group in candidates.Where(o => PayoutRules.Stage(o, cutoff) == "available").GroupBy(o => o.SupplierId))
        {
            var supplier = group.First().Supplier;
            var orders = group.ToList();
            var amount = orders.Sum(PayoutRules.AmountOwed);
            var cents = decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
            if (cents <= 0 || !HasBankDetails(supplier) || supplier.Status is "suspended" or "deleted")
            {
                skipped++;
                continue;
            }

            try
            {
                if (string.IsNullOrEmpty(supplier.PaystackRecipientCode))
                {
                    supplier.PaystackRecipientCode = await transfers.CreateRecipientAsync(
                        supplier.CompanyName, supplier.BankAccount!.Trim(), supplier.BankBranchCode!.Trim(), ct);
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (PaystackTransferException ex)
            {
                // Paystack doesn't accept these bank details: mark failed so it isn't retried every run.
                var failedReference = $"po_{Guid.NewGuid():N}";
                foreach (var o in orders) o.PayoutReference = failedReference;
                await db.SaveChangesAsync(ct);
                await SupplierPayoutResults.ApplyAsync(db, events, failedReference, false,
                    $"Paystack didn't accept the bank details ({ex.Message})", ct);
                skipped++;
                continue;
            }

            // Claim the orders before sending money, so a crash or a second run can't pay them twice.
            var reference = $"po_{Guid.NewGuid():N}";
            foreach (var o in orders)
            {
                o.PayoutInitiatedAt = now;
                o.PayoutReference = reference;
            }
            await db.SaveChangesAsync(ct);

            try
            {
                var transfer = await transfers.InitiateTransferAsync(cents, supplier.PaystackRecipientCode!, reference,
                    $"SpazaSure payout: {orders.Count} order{(orders.Count == 1 ? "" : "s")}", ct);
                switch (transfer.Status)
                {
                    case "success":
                        await SupplierPayoutResults.ApplyAsync(db, events, reference, true, null, ct);
                        break;
                    case "failed":
                        await SupplierPayoutResults.ApplyAsync(db, events, reference, false, null, ct);
                        break;
                    case "otp":
                        if (Interlocked.Exchange(ref _lastOtpAlertTicks, now.Ticks) < now.AddHours(-6).Ticks)
                            await AdminAlerts.NotifyAdminsAsync(db, events, "finance", "Paystack payouts need OTP turned off",
                                "Automatic supplier payouts are waiting for a one-time PIN. Turn off OTP for transfers in the Paystack dashboard (Settings → Preferences), or approve them there.", null);
                        break;
                }
                started++;
            }
            catch (PaystackTransferException ex)
            {
                // Paystack refused before creating a transfer: no fee was charged, so release the orders.
                foreach (var o in orders)
                {
                    o.PayoutInitiatedAt = null;
                    o.PayoutReference = null;
                }
                await db.SaveChangesAsync(ct);
                if (Interlocked.Exchange(ref _lastBalanceAlertTicks, now.Ticks) < now.AddHours(-6).Ticks)
                    await AdminAlerts.NotifyAdminsAsync(db, events, "finance", "Supplier payouts paused",
                        ex.IsInsufficientBalance
                            ? "The Paystack balance is too low to pay suppliers. Top it up or change the settlement schedule; payouts continue automatically."
                            : $"Paystack refused a supplier payout: {ex.Message}. Payouts are retried automatically.", null);
                skipped++;
                if (ex.IsInsufficientBalance) break; // the others won't go through either
            }
        }
        return new RunResult(started, skipped, settled);
    }

    /// <summary>Transfers still pending after a while: ask Paystack, in case the webhook was missed.</summary>
    private static async Task<int> SettleStuckTransfersAsync(
        SpazaSureDbContext db, PaystackTransferService transfers, EventPublisher events, DateTime now, CancellationToken ct)
    {
        var stuck = await db.Orders
            .Where(o => o.PaidOutAt == null && o.PayoutInitiatedAt != null && o.PayoutInitiatedAt < now - CheckPendingAfter &&
                        o.PayoutReference != null && !o.PayoutReference.StartsWith(SupplierPayoutResults.FailedPrefix))
            .Select(o => o.PayoutReference!)
            .Distinct()
            .ToListAsync(ct);
        var settled = 0;
        foreach (var reference in stuck)
        {
            string status;
            try { status = await transfers.VerifyTransferAsync(reference, ct); }
            catch (PaystackTransferException) { continue; }
            if (status == "success") await SupplierPayoutResults.ApplyAsync(db, events, reference, true, null, ct);
            else if (status is "failed" or "reversed") await SupplierPayoutResults.ApplyAsync(db, events, reference, false, $"the transfer was {status}", ct);
            else continue;
            settled++;
        }
        return settled;
    }

    public static bool HasBankDetails(Supplier s) =>
        !string.IsNullOrWhiteSpace(s.BankAccount) && SaValidation.Digits(s.BankBranchCode).Length == 6;
}

/// <summary>Runs <see cref="AutomaticPayouts"/> every 30 minutes while automatic payouts are switched on.</summary>
public sealed class AutomaticPayoutWorker(IServiceScopeFactory scopeFactory, ILogger<AutomaticPayoutWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // let the service finish starting
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var transfers = scope.ServiceProvider.GetRequiredService<PaystackTransferService>();
                if (transfers.AutomaticPayoutsEnabled)
                {
                    var result = await AutomaticPayouts.RunOnceAsync(
                        scope.ServiceProvider.GetRequiredService<SpazaSureDbContext>(), transfers,
                        scope.ServiceProvider.GetRequiredService<EventPublisher>(), DateTime.UtcNow, stoppingToken);
                    if (result.Started + result.Settled > 0)
                        logger.LogInformation("Automatic payouts: {Started} started, {Settled} settled, {Skipped} skipped",
                            result.Started, result.Settled, result.Skipped);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Automatic payout run failed");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }
}
