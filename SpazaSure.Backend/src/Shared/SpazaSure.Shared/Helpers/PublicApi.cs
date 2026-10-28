using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SpazaSure.Shared.Helpers;

public static class PublicApi
{
    /// <summary>The public address of the API (behind the gateway), e.g. https://api.spazasure.co.za.</summary>
    public static string Base(HttpRequest? request, IConfiguration? config = null)
    {
        // No request outside the web pipeline (tests, background work): links stay relative
        if (request is null) return config?["PublicApiUrl"]?.TrimEnd('/') ?? "";
        config ??= request.HttpContext.RequestServices?.GetService(typeof(IConfiguration)) as IConfiguration;
        if (config?["PublicApiUrl"] is { Length: > 0 } configured) return configured.TrimEnd('/');
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        return $"{scheme}://{host}";
    }
}
