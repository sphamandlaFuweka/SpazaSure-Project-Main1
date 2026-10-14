using System.Text;
using System.Text.Json;

namespace SpazaSure.Gateway;

/// <summary>
/// When an admin turns on maintenance mode (Admin > Settings > Operations), shops and
/// suppliers get a 503 with the admin's message. These keep working:
///   - any request with an admin token (admins can keep managing the platform),
///   - sign-in / token refresh (so admins can log in),
///   - Paystack webhooks (payments must never be dropped),
///   - the public settings endpoint (so apps can show the message), and non-API files.
/// The flag is read from the user service's public settings and cached briefly; if that
/// call fails, the last known value is kept (and the platform stays open by default).
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<MaintenanceModeMiddleware> logger)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);
    private static readonly SemaphoreSlim Refresh = new(1, 1);
    private static DateTime _checkedAt = DateTime.MinValue;
    private static bool _enabled;
    private static string _message = "SpazaSure is undergoing scheduled maintenance. Please try again shortly.";

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExempt(context) || !await IsEnabledAsync(context.RequestAborted))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "300";
        await context.Response.WriteAsJsonAsync(new { success = false, maintenance = true, message = _message });
    }

    private static bool IsExempt(HttpContext context)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api")) return true;
        if (path.StartsWithSegments("/api/settings")) return true;
        if (path.StartsWithSegments("/api/admin")) return true;
        if (path.Value!.Contains("/auth/", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Value!.EndsWith("/webhook", StringComparison.OrdinalIgnoreCase)) return true;
        return HasAdminToken(context);
    }

    /// <summary>
    /// Reads the role from the bearer token without verifying it. That is safe here: it only
    /// decides whether to skip the maintenance block, and every service still validates the
    /// token, so a forged "admin" token gets a 401 downstream.
    /// </summary>
    private static bool HasAdminToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = header["Bearer ".Length..].Trim().Split('.');
        if (parts.Length != 3) return false;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            foreach (var claim in new[] { "role", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" })
            {
                if (!doc.RootElement.TryGetProperty(claim, out var role)) continue;
                if (role.ValueKind == JsonValueKind.String && role.GetString() == "admin") return true;
                if (role.ValueKind == JsonValueKind.Array && role.EnumerateArray().Any(r => r.GetString() == "admin")) return true;
            }
        }
        catch (Exception e) when (e is FormatException or JsonException) { }
        return false;
    }

    private async Task<bool> IsEnabledAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow - _checkedAt < CacheFor) return _enabled;
        if (!await Refresh.WaitAsync(0, ct)) return _enabled; // another request is refreshing
        try
        {
            var baseUrl = config["Maintenance:SettingsBaseUrl"]
                ?? config.GetSection("ReverseProxy:Clusters:user-cluster:Destinations").GetChildren()
                    .Select(d => d["Address"]).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            if (string.IsNullOrWhiteSpace(baseUrl)) return _enabled;

            var client = httpClientFactory.CreateClient("maintenance");
            using var doc = JsonDocument.Parse(await client.GetStringAsync($"{baseUrl.TrimEnd('/')}/api/settings/public", ct));
            var data = doc.RootElement.GetProperty("data");
            _enabled = data.TryGetProperty("maintenanceMode", out var on) && on.ValueKind == JsonValueKind.True;
            if (data.TryGetProperty("maintenanceMessage", out var msg) && !string.IsNullOrWhiteSpace(msg.GetString()))
                _message = msg.GetString()!;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("Could not read maintenance mode setting: {Error}", e.Message);
        }
        finally
        {
            _checkedAt = DateTime.UtcNow;
            Refresh.Release();
        }
        return _enabled;
    }
}
