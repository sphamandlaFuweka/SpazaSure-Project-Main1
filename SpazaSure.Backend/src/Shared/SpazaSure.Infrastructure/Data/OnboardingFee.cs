using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// The once-off fee a shop pays to be onboarded (admin-set, R150 by default). Group buying is
/// for onboarded shops only.
/// </summary>
public static class OnboardingFee
{
    public const string Key = "shop_onboarding_fee_amount";
    public const decimal Default = 150m;

    public static async Task<decimal> AmountAsync(SpazaSureDbContext db)
    {
        var value = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == Key).Select(s => s.Value).SingleOrDefaultAsync();
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : Default;
    }

    public static string NeededForGroupBuys(decimal amount) =>
        $"Pay the once-off R{amount.ToString("0.00", CultureInfo.InvariantCulture)} SpazaSure onboarding fee to start buying in groups.";
}
