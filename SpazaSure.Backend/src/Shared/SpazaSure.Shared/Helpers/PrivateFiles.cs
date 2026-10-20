using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// Compliance documents (owner IDs, permits, CIPC certificates) and report photos hold personal
/// information (POPIA s19), so they are not public files. The API hands out links signed for
/// 30 minutes to the people allowed to see them, and the file server refuses any other request.
/// Product images, logos and profile photos stay public.
/// </summary>
public static class PrivateFiles
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(30);

    private static readonly HashSet<string> FileRoots = new(StringComparer.OrdinalIgnoreCase) { "uploads", "compliance-uploads" };
    private static readonly HashSet<string> PrivateFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "shop-docs", "supplier-docs", "report-photos",
    };

    private static byte[]? _key;

    /// <summary>Call once at startup. The key is derived from the JWT secret every service already has.</summary>
    public static void Configure(string? secret) =>
        _key = string.IsNullOrWhiteSpace(secret)
            ? null
            : SHA256.HashData(Encoding.UTF8.GetBytes("spazasure-private-files:" + secret));

    /// <summary>
    /// The file path as the file server sees it: empty segments dropped, so "/uploads//shop-docs/x"
    /// can't slip past the check. Null when the path isn't under a private folder.
    /// </summary>
    private static string? PrivatePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 && FileRoots.Contains(segments[0]) && PrivateFolders.Contains(segments[1])
            ? "/" + string.Join('/', segments)
            : null;
    }

    public static bool IsPrivate(string path) => PrivatePath(path) is not null;

    /// <summary>
    /// A link to a stored file. Private files get a signature valid for <see cref="LinkLifetime"/>;
    /// public files and external (S3) links are returned unchanged.
    /// </summary>
    public static string? Link(string? url) => Link(url, DateTimeOffset.UtcNow);

    public static string? Link(string? url, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith('/')) return url;
        var path = url.Split('?')[0];
        if (PrivatePath(path) is not { } canonical || _key is null) return path;
        var expires = now.Add(LinkLifetime).ToUnixTimeSeconds();
        return $"{path}?exp={expires}&sig={Signature(canonical, expires)}";
    }

    /// <summary>Whether a request for a private file carries a valid, unexpired signature.</summary>
    public static bool IsAllowed(string path, string? exp, string? sig, DateTimeOffset now)
    {
        if (PrivatePath(path) is not { } canonical) return true;
        if (_key is null || string.IsNullOrEmpty(sig) ||
            !long.TryParse(exp, NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            now.ToUnixTimeSeconds() > expires)
            return false;
        byte[] supplied;
        try { supplied = Convert.FromHexString(sig); }
        catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Signature(canonical, expires)), supplied);
    }

    private static string Signature(string canonicalPath, long expires) =>
        Convert.ToHexString(HMACSHA256.HashData(_key!, Encoding.UTF8.GetBytes($"{canonicalPath}\n{expires}"))).ToLowerInvariant();

    /// <summary>Refuses unsigned or expired requests for private files. Call before UseStaticFiles.</summary>
    public static IApplicationBuilder UsePrivateFiles(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            if (!IsAllowed(path, context.Request.Query["exp"], context.Request.Query["sig"], DateTimeOffset.UtcNow))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            if (IsPrivate(path))
            {
                // Don't let browsers or proxies keep copies of ID documents.
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "private, no-store";
                    return Task.CompletedTask;
                });
            }
            await next();
        });
}
