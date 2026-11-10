using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.UserService.Services;

/// <summary>
/// Paid plans for customers: SpazaSure Plus, and Bulk Buyer (which includes Plus, see
/// <see cref="BulkBuying"/>). A subscription with a Paystack plan code renews by itself; one
/// without was paid once-off for a month and is paid again from the app when it ends.
/// </summary>
public static class CustomerPlus
{
    public const string PlusPlanKey = "plus";
    public const decimal MonthlyPrice = 19.99m;
    public const string RequiredMessage = "This is a SpazaSure Plus feature. Upgrade under Profile > Membership.";

    /// <summary>The plans a customer can pay for.</summary>
    public static bool IsPaidPlan(string? planKey) => planKey is PlusPlanKey or BulkBuying.PlanKey;

    /// <summary>The monthly price of a paid plan.</summary>
    public static decimal PriceOf(string? planKey) =>
        planKey == BulkBuying.PlanKey ? BulkBuying.MonthlyPrice : MonthlyPrice;

    /// <summary>The Paystack plan code setting for a renewing plan.</summary>
    public static string PaystackPlanSetting(string planKey) =>
        planKey == BulkBuying.PlanKey ? BulkBuying.PaystackPlanSetting : "CustomerPlusMonthly";

    public static Task<CustomerSubscription?> LatestAsync(
        SpazaSureDbContext db, Guid customerUserId, CancellationToken ct = default) =>
        db.CustomerSubscriptions
            .Where(s => s.CustomerUserId == customerUserId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>Whether the customer has Plus features: from Plus itself, or from Bulk Buyer.</summary>
    public static async Task<bool> IsActiveAsync(
        SpazaSureDbContext db, Guid customerUserId, CancellationToken ct = default) =>
        IsActive(await LatestAsync(db, customerUserId, ct));

    public static bool IsActive(CustomerSubscription? subscription) =>
        IsPaidPlan(subscription?.PlanKey) &&
        subscription!.Status == "active" &&
        (!subscription.CurrentPeriodEnd.HasValue || subscription.CurrentPeriodEnd.Value > DateTime.UtcNow);

    public static bool AutoRenews(CustomerSubscription? subscription) =>
        !string.IsNullOrWhiteSpace(subscription?.PaymentProviderPlanCode);

    /// <summary>A once-off month that ran out, so the customer can pay for another one.</summary>
    public static bool IsLapsed(CustomerSubscription? subscription) =>
        subscription?.Status == "active" &&
        !AutoRenews(subscription) &&
        subscription.CurrentPeriodEnd.HasValue &&
        subscription.CurrentPeriodEnd.Value <= DateTime.UtcNow;

    public static void Activate(CustomerSubscription subscription, PaystackTransaction transaction)
    {
        subscription.Status = "active";
        subscription.PaymentReference = transaction.Reference;
        subscription.PaymentProviderCustomerCode = transaction.CustomerCode;
        subscription.CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        subscription.CancelAtPeriodEnd = false;
    }

    public static bool IsExpectedPayment(PaystackTransaction transaction, string reference, string? planKey) =>
        transaction.IsSuccessful &&
        string.Equals(transaction.Reference, reference, StringComparison.Ordinal) &&
        string.Equals(transaction.Currency, "ZAR", StringComparison.OrdinalIgnoreCase) &&
        transaction.Amount == decimal.ToInt64(PriceOf(planKey) * 100m);

    /// <summary>
    /// After a customer moves to another plan (e.g. Plus to Bulk Buyer), ends their older paid plans
    /// so they aren't billed twice. Paystack renewals are switched off where possible; a failure is
    /// logged and left for support, since the new plan is already paid for.
    /// </summary>
    public static async Task RetireOlderPlansAsync(
        SpazaSureDbContext db, PaystackPaymentService paystack, CustomerSubscription current, CancellationToken ct = default)
    {
        var older = await db.CustomerSubscriptions
            .Where(s => s.CustomerUserId == current.CustomerUserId && s.Id != current.Id &&
                        (s.Status == "active" || s.Status == "past_due" || s.Status == "unpaid"))
            .ToListAsync(ct);
        foreach (var subscription in older)
        {
            if (AutoRenews(subscription) &&
                !string.IsNullOrWhiteSpace(subscription.PaymentProviderSubscriptionCode) &&
                !string.IsNullOrWhiteSpace(subscription.PaymentProviderEmailToken))
            {
                try
                {
                    await paystack.DisableSubscriptionAsync(
                        subscription.PaymentProviderSubscriptionCode, subscription.PaymentProviderEmailToken, ct);
                }
                catch (Exception error) when (error is HttpRequestException or InvalidOperationException)
                {
                    Console.Error.WriteLine(
                        $"[CustomerPlus] Could not stop Paystack renewal of subscription {subscription.Id} after a plan change: {error.Message}");
                }
            }
            subscription.Status = "cancelled";
            subscription.CancelAtPeriodEnd = false;
            subscription.CurrentPeriodEnd = DateTime.UtcNow;
        }
    }
}
