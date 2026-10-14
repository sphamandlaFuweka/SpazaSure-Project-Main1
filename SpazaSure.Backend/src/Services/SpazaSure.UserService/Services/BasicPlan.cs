using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;

namespace SpazaSure.UserService.Services;

/// <summary>Reads the Basic plan's commission so downgrades use the configured rate.</summary>
public static class BasicPlan
{
    private const decimal FallbackCommissionRate = 5m;

    public static async Task<decimal> CommissionRateAsync(SpazaSureDbContext db, CancellationToken ct = default) =>
        await db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.Tier == "basic")
            .Select(p => (decimal?)p.CommissionRate)
            .FirstOrDefaultAsync(ct)
        ?? FallbackCommissionRate;
}
