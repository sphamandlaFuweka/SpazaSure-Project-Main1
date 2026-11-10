using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// The Bulk Buyer plan for shoppers: R150 a month to see the bulk marketplace and order from
/// suppliers, at the shop price plus <see cref="PlatformRules.BulkBuyerMarkupPercent"/>, with at least
/// <see cref="PlatformRules.BulkBuyerMinOrderAmount"/> per supplier order. It includes SpazaSure Plus.
/// A shopper orders through a buyer account (a spaza_shops row of type bulk_buyer) that holds their
/// delivery address, so orders use the same checkout, delivery and payout flow as shops.
/// </summary>
public static class BulkBuying
{
    public const string PlanKey = "bulk";
    public const decimal MonthlyPrice = 150m;
    /// <summary>The Paystack plan code setting (Paystack:PlanCodes:...) for a renewing Bulk Buyer plan.</summary>
    public const string PaystackPlanSetting = "CustomerBulkMonthly";

    public const string RequiredMessage =
        "Bulk buying is part of the Bulk Buyer plan (R150 a month). Subscribe under Profile > Membership.";
    public const string AddressRequiredMessage =
        "Add the address your bulk orders go to first, so we can show suppliers that deliver to you.";

    /// <summary>The shopper's newest subscription is an active Bulk Buyer plan.</summary>
    public static bool IsActive(CustomerSubscription? subscription) =>
        subscription?.PlanKey == PlanKey &&
        subscription.Status == "active" &&
        (!subscription.CurrentPeriodEnd.HasValue || subscription.CurrentPeriodEnd.Value > DateTime.UtcNow);

    public static async Task<bool> IsActiveAsync(SpazaSureDbContext db, Guid customerUserId, CancellationToken ct = default) =>
        IsActive(await db.CustomerSubscriptions.AsNoTracking()
            .Where(s => s.CustomerUserId == customerUserId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct));

    /// <summary>The shopper's buyer account, or null before they've added a delivery address.</summary>
    public static Task<SpazaShop?> BuyerAccountAsync(SpazaSureDbContext db, Guid customerUserId, bool tracked = true, CancellationToken ct = default)
    {
        var shops = tracked ? db.SpazaShops : db.SpazaShops.AsNoTracking();
        return shops.FirstOrDefaultAsync(s => s.UserId == customerUserId && s.AccountType == ShopAccountTypes.BulkBuyer, ct);
    }

    public static bool IsBulkBuyer(SpazaShop? buyer) => buyer?.AccountType == ShopAccountTypes.BulkBuyer;

    /// <summary>
    /// The fraction added to supplier prices for this buyer: shops pay SpazaSure's fee; bulk buyers
    /// pay the shop price plus the Bulk Buyer markup on top.
    /// </summary>
    public static decimal Markup(SpazaShop? buyer, PlatformRules rules)
    {
        var shop = ShopPricing.Markup(rules);
        return IsBulkBuyer(buyer) ? (1m + shop) * (1m + rules.BulkBuyerMarkupPercent / 100m) - 1m : shop;
    }

    /// <summary>The smallest order this buyer may place with one supplier.</summary>
    public static decimal MinOrderAmount(SpazaShop? buyer, PlatformRules rules) =>
        IsBulkBuyer(buyer) ? rules.BulkBuyerMinOrderAmount : rules.MinOrderAmount;
}
