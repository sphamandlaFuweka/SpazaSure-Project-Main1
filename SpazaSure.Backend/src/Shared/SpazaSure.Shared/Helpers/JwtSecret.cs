using Microsoft.Extensions.Configuration;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// Reads Jwt:Secret and refuses to start outside Development when it is missing, short, or one of
/// the placeholder values committed to appsettings*.json. The base appsettings.json ships a known
/// dev secret; if the real one were ever left unset in QA/Prod, every service would silently fall
/// back to it and anyone could forge admin tokens, signed invoice links and private-file links
/// (all of which are keyed from this secret).
/// </summary>
public static class JwtSecret
{
    public const int MinLength = 32;

    private static readonly string[] KnownPlaceholders =
    [
        "SpazaSure_JWT_Secret_Key_32Chars_Min!",
        "REPLACE_WITH",
        "change_me",
        "CHANGE_ME",
    ];

    public static string Require(IConfiguration config, bool isDevelopment)
    {
        var secret = config["Jwt:Secret"];
        if (isDevelopment && !string.IsNullOrEmpty(secret)) return secret;

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Jwt:Secret is not configured. Set Jwt__Secret (e.g. PROD_JWT_SECRET in .env.prod).");
        if (secret.Length < MinLength)
            throw new InvalidOperationException($"Jwt:Secret must be at least {MinLength} characters.");
        if (KnownPlaceholders.Any(p => secret.Contains(p, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Jwt:Secret is still a placeholder/default value from source control. Generate a real one, e.g. `openssl rand -base64 48`.");
        return secret;
    }
}
