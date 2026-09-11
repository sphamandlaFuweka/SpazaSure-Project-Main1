using Microsoft.Extensions.Configuration;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// Every service previously hardcoded its CORS check to
/// `new Uri(origin).Host == "localhost"` (or, in the Gateway's case, allowed
/// every origin with no restriction at all). That meant a real deployed
/// frontend domain would get silently blocked by every service except the
/// Gateway — and the Gateway itself was wide open. This reads an explicit
/// allow-list from configuration instead, so it works the same way in dev,
/// QA, and production: just set `Cors:AllowedOrigins` in appsettings (or the
/// equivalent environment variable, e.g. `Cors__AllowedOrigins__0`).
/// </summary>
public static class CorsHelper
{
    private static readonly string[] DefaultDevOrigins =
    [
        "http://localhost:3000",
        "http://localhost:5173",
    ];

    public static Func<string, bool> BuildOriginPredicate(IConfiguration config)
    {
        var configured = config.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var allowed = (configured is { Length: > 0 } ? configured : DefaultDevOrigins)
            .Select(o => o.TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return origin => allowed.Contains(origin.TrimEnd('/'));
    }
}
