using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.OrderService.Services;

/// <summary>
/// Looks after group buys' end dates without waiting for someone to open them: members hear 2
/// days before the end; at the end a campaign gets one more week, and after that it closes and
/// gives shops back the money held for products that didn't reach their target.
/// </summary>
public static class GroupBuyExpiry
{
    /// <summary>Returns how many campaigns reached their end date (extended or closed).</summary>
    public static async Task<int> RunOnceAsync(SpazaSureDbContext db, EventPublisher events, DateTime now, CancellationToken ct = default)
    {
        var due = await db.GroupBuys.AsNoTracking()
            .Where(g => g.Status == "active" && g.ExpiresAt <= now)
            .Select(g => g.Id)
            .ToListAsync(ct);
        foreach (var id in due)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var groupBuy = await GroupBuyPayments.LoadForUpdateAsync(db, id, ct);
            // Someone else may have handled it meanwhile
            if (groupBuy is null || groupBuy.Status != "active" || groupBuy.ExpiresAt > now) continue;
            var end = await GroupBuyPayments.ReachEndAsync(db, groupBuy, now, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            if (end.Extended) GroupBuyNotices.Extended(events, groupBuy);
            else GroupBuyNotices.Refunds(events, groupBuy, end.Refunds);
            db.ChangeTracker.Clear();
        }

        var endingSoon = await db.GroupBuys.AsNoTracking()
            .Where(g => g.Status == "active" && g.ExpiresAt > now &&
                        g.ExpiresAt <= now.Add(GroupBuyPayments.ReminderBefore) && g.EndingReminderSentAt == null)
            .Select(g => g.Id)
            .ToListAsync(ct);
        foreach (var id in endingSoon)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var groupBuy = await GroupBuyPayments.LoadForUpdateAsync(db, id, ct);
            if (groupBuy is null || groupBuy.EndingReminderSentAt is not null) continue;
            groupBuy.EndingReminderSentAt = now;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            GroupBuyNotices.EndingSoon(events, groupBuy, GroupBuyPayments.ItemsStillNeeded(groupBuy, now));
            db.ChangeTracker.Clear();
        }
        return due.Count;
    }
}

public sealed class GroupBuyExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<GroupBuyExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // let the service finish starting
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var closed = await GroupBuyExpiry.RunOnceAsync(
                    scope.ServiceProvider.GetRequiredService<SpazaSureDbContext>(),
                    scope.ServiceProvider.GetRequiredService<EventPublisher>(), DateTime.UtcNow, stoppingToken);
                if (closed > 0) logger.LogInformation("{Count} group buys reached their end date", closed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Group buy expiry run failed");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }
}
