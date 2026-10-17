using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Order numbers like SPZ-2026-K7M4QX. Random rather than a running count, so two checkouts at
/// the same moment can't produce the same number (order numbers are unique in the database).
/// </summary>
public static class OrderNumbers
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O or 1/I look-alikes

    public static async Task<string> NewAsync(SpazaSureDbContext db, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var suffix = string.Create(6, 0, (span, _) =>
            {
                for (var i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            });
            var number = $"SPZ-{DateTime.UtcNow:yyyy}-{suffix}";
            if (attempt >= 5 || !await db.Orders.AnyAsync(o => o.OrderNumber == number, ct)) return number;
        }
    }
}
