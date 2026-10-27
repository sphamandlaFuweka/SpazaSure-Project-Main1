using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Sends admins a high-priority notification (dashboard bell and phone push) when a request
/// crashes. The same error on the same endpoint alerts at most once every 15 minutes, so a
/// crash loop can't flood anyone.
/// </summary>
public sealed partial class AdminErrorAlerts(
    IServiceScopeFactory scopes, EventPublisher events, ILogger<AdminErrorAlerts> logger) : IUnhandledErrorAlerts
{
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromMinutes(15);

    private static readonly string Service =
        (Assembly.GetEntryAssembly()?.GetName().Name ?? "backend").Replace("SpazaSure.", "");

    private readonly ConcurrentDictionary<string, DateTime> _lastSent = new();

    /// <summary>For tests: replaces the clock.</summary>
    internal Func<DateTime> Now { get; init; } = () => DateTime.UtcNow;

    public void Report(string method, string path, Exception exception)
    {
        var endpoint = $"{method} {Ids().Replace(path, "{id}")}";
        if (!ShouldSend($"{exception.GetType().FullName}|{endpoint}")) return;

        var detail = exception.Message.Length > 300 ? exception.Message[..300] + "…" : exception.Message;
        var message = $"{endpoint} failed with {exception.GetType().Name}: {detail} " +
                      $"Check the {Service} logs. Repeats of this error are muted for {QuietPeriod.TotalMinutes:0} minutes.";

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SpazaSureDbContext>();
                await AdminAlerts.NotifyAdminsAsync(db, events, "system", $"Server error in {Service}", message, priority: "high");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not alert admins about an error in {Service}", Service);
            }
        });
    }

    /// <summary>True the first time a key is seen, then again only once the quiet period has passed.</summary>
    internal bool ShouldSend(string key)
    {
        var now = Now();
        if (_lastSent.TryAdd(key, now)) return true;
        var last = _lastSent[key];
        return now - last >= QuietPeriod && _lastSent.TryUpdate(key, now, last);
    }

    // GUIDs and numeric ids, so /orders/123 and /orders/456 count as one endpoint
    [GeneratedRegex(@"(?<=/)([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|\d+)(?=/|$)")]
    private static partial Regex Ids();
}
