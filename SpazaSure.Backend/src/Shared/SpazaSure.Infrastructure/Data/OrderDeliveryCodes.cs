using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Each delivery order's 6-digit code (e.g. 221032). The shop sees it in its app, as digits and a
/// QR code; the driver types or scans it at the shop, which proves the goods reached the right
/// shop. No two orders share a code.
/// </summary>
public static class OrderDeliveryCodes
{
    /// <summary>What the shop's QR code holds; the driver page reads the code back out of it.</summary>
    public const string QrPrefix = "SPAZASURE-DELIVERY:";

    /// <summary>After this many wrong codes the driver can't confirm the order; SpazaSure sorts it out.</summary>
    public const int MaxAttempts = 5;

    /// <summary>A code no other order has. Pass the codes given to orders not saved yet (one checkout, many orders).</summary>
    public static async Task<string> NewAsync(SpazaSureDbContext db, ISet<string>? takenInThisBatch = null)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            if (takenInThisBatch?.Contains(code) == true) continue;
            if (await db.Orders.AnyAsync(o => o.DeliveryCode == code)) continue;
            takenInThisBatch?.Add(code);
            return code;
        }
        throw new InvalidOperationException("Could not find an unused delivery code.");
    }

    /// <summary>The 6 digits from what the driver typed ("221 032") or scanned (the shop's QR code).</summary>
    public static string? Read(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var value = input.Trim();
        if (value.StartsWith(QrPrefix, StringComparison.OrdinalIgnoreCase)) value = value[QrPrefix.Length..];
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 6 ? digits : null;
    }

    public static bool Matches(string? expected, string? supplied) =>
        !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(supplied) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
}
