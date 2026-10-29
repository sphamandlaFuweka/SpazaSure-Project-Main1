using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.UserService.Services;

/// <summary>
/// SpazaSure Plus for customers. A subscription with a Paystack plan code renews by itself;
/// one without was paid once-off for a month and is paid again from the app when it ends.
/// </summary>
public static class CustomerPlus
{
    public const decimal MonthlyPrice = 19.99m;
    public const string RequiredMessage = "This is a SpazaSure Plus feature. Upgrade under Profile > Membership.";

    public static Task<CustomerSubscription?> LatestAsync(
        SpazaSureDbContext db, Guid customerUserId, CancellationToken ct = default) =>
        db.CustomerSubscriptions
            .Where(s => s.CustomerUserId == customerUserId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public static async Task<bool> IsActiveAsync(
        SpazaSureDbContext db, Guid customerUserId, CancellationToken ct = default) =>
        IsActive(await LatestAsync(db, customerUserId, ct));

    public static bool IsActive(CustomerSubscription? subscription) =>
        subscription?.PlanKey == "plus" &&
        subscription.Status == "active" &&
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

    public static bool IsExpectedPayment(PaystackTransaction transaction, string reference) =>
        transaction.IsSuccessful &&
        string.Equals(transaction.Reference, reference, StringComparison.Ordinal) &&
        string.Equals(transaction.Currency, "ZAR", StringComparison.OrdinalIgnoreCase) &&
        transaction.Amount == decimal.ToInt64(MonthlyPrice * 100m);
}
