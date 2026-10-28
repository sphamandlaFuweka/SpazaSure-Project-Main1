using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpazaSure.Infrastructure.Entities;
using WebPush;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Phone notifications for drivers. Drivers have no app account, so these use standard web push:
/// the driver turns alerts on from their driver page once, and new deliveries then pop up on the
/// phone even when the page is closed. The signing keys are made on first use and kept in
/// platform_settings, so nothing has to be configured.
/// </summary>
public class DriverAlerts(SpazaSureDbContext db, IConfiguration config, ILogger<DriverAlerts> logger)
{
    private const string PublicKeyName = "driver_push_public_key";
    private const string PrivateKeyName = "driver_push_private_key";

    /// <summary>The key the driver page subscribes with.</summary>
    public async Task<string> PublicKeyAsync() => (await KeysAsync()).PublicKey;

    /// <summary>Sends to every phone the driver turned alerts on for; forgets phones that are gone.</summary>
    public async Task SendAsync(Driver driver, string title, string body, string? tag = null)
    {
        var subscriptions = await db.DriverPushSubscriptions.Where(s => s.DriverId == driver.Id).ToListAsync();
        if (subscriptions.Count == 0) return;

        var keys = await KeysAsync();
        var subject = config["DriverAlerts:Subject"] ?? "mailto:support@spazasure.co.za";
        var vapid = new VapidDetails(subject, keys.PublicKey, keys.PrivateKey);
        var payload = JsonSerializer.Serialize(new { title, body, tag = tag ?? "delivery", url = $"/driver/{driver.AccessToken}" });
        var client = new WebPushClient();

        foreach (var s in subscriptions)
        {
            try
            {
                await client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, vapid);
            }
            catch (WebPushException ex) when ((int)ex.StatusCode is 404 or 410)
            {
                // The driver cleared the browser or switched alerts off
                db.DriverPushSubscriptions.Remove(s);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Delivery alert to driver {DriverId} failed.", driver.Id);
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Tells the order's driver what changed that affects them: ready to collect, or cancelled.</summary>
    public async Task OrderStatusChangedAsync(Order order)
    {
        if (order.DriverId is not { } driverId) return;
        var (title, body) = order.Status switch
        {
            "ready" => ("Ready to collect", $"Order {order.OrderNumber} is packed. You can collect it from the supplier now."),
            "cancelled" => ("Delivery cancelled", $"Order {order.OrderNumber} was cancelled. Don't collect or deliver it."),
            _ => (null, null),
        };
        if (title is null || body is null) return;
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId && d.IsActive);
        if (driver is not null) await SendAsync(driver, title, body, order.OrderNumber);
    }

    private async Task<(string PublicKey, string PrivateKey)> KeysAsync()
    {
        var stored = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == PublicKeyName || s.Key == PrivateKeyName)
            .ToDictionaryAsync(s => s.Key, s => s.Value);
        if (stored.TryGetValue(PublicKeyName, out var pub) && stored.TryGetValue(PrivateKeyName, out var priv))
            return (pub, priv);

        var generated = VapidHelper.GenerateVapidKeys();
        db.PlatformSettings.Add(new PlatformSetting { Key = PublicKeyName, Value = generated.PublicKey });
        db.PlatformSettings.Add(new PlatformSetting { Key = PrivateKeyName, Value = generated.PrivateKey });
        try
        {
            await db.SaveChangesAsync();
            return (generated.PublicKey, generated.PrivateKey);
        }
        catch (DbUpdateException)
        {
            // Another request made the keys at the same moment: use theirs
            db.ChangeTracker.Clear();
            return await KeysAsync();
        }
    }
}
