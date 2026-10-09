using System.Security.Cryptography;
using System.Text;

namespace SpazaSure.ProductService.Services;

/// <summary>Generates and checks the open code and hidden scratch PIN printed on each unit.</summary>
public static class UnitCodeService
{
    // Crockford base32: no I, L, O or U, so printed codes are hard to misread.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static string RandomString(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    public static string NewCode() => "SZ" + RandomString(14);

    /// <summary>12 characters (60 bits), shown as XXXX-XXXX-XXXX.</summary>
    public static string NewPin() => RandomString(12);

    public static string FormatPin(string pin) => $"{pin[..4]}-{pin[4..8]}-{pin[8..]}";

    public static string NormalizePin(string? pin)
    {
        var cleaned = new string((pin ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        // Treat the usual look-alikes the way Crockford base32 does.
        return cleaned.Replace('O', '0').Replace('I', '1').Replace('L', '1');
    }

    public static string HashPin(string normalizedPin, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedPin)));
    }

    public static bool PinMatches(string normalizedPin, string storedHash, string secret) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(HashPin(normalizedPin, secret)),
            Encoding.UTF8.GetBytes(storedHash));
}
