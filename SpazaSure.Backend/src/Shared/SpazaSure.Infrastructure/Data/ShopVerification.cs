using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Shop numbers (e.g. SS482913) and the "SpazaSure Verified" green sticker.
/// A shop is verified when an admin has verified it and every required compliance
/// document is approved and not expired.
/// </summary>
public static class ShopVerification
{
    public const string Prefix = "SS";

    /// <summary>Documents a shop needs approved to be verified. Each entry lists the accepted doc type keys.</summary>
    public static readonly (string Label, string[] DocTypes)[] RequiredDocuments =
    [
        ("Business permit", ["business_permit"]),
        ("Health certificate", ["health_certificate", "health_cert"]),
        ("Owner ID document", ["id_document"]),
    ];

    /// <summary>The public verify page the QR code on the sticker opens.</summary>
    public static string VerifyUrl(string shopCode) => $"https://portal.spazasure.co.za/verify/{shopCode}";

    /// <summary>
    /// Turns what a person typed or scanned into a shop number: accepts "SS482913", "ss-482 913",
    /// "482913" or a verify-page URL. Returns null if it can't be a shop number.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var value = input.Trim();
        var slash = value.LastIndexOf('/');
        if (slash >= 0) value = value[(slash + 1)..];
        var compact = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (compact.StartsWith(Prefix)) compact = compact[Prefix.Length..];
        return compact.Length == 6 && compact.All(char.IsAsciiDigit) ? Prefix + compact : null;
    }

    /// <summary>Gives the shop a unique number if it doesn't have one yet. Does not save.</summary>
    public static async Task<string> EnsureCodeAsync(SpazaSureDbContext db, SpazaShop shop, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(shop.ShopCode)) return shop.ShopCode;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = Prefix + RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            if (!await db.SpazaShops.AnyAsync(s => s.ShopCode == code, ct))
            {
                shop.ShopCode = code;
                return code;
            }
        }
        throw new InvalidOperationException("Could not allocate a unique shop number.");
    }

    public sealed record Result(bool Verified, string Summary, IReadOnlyList<string> Outstanding, DateOnly? ValidUntil);

    /// <summary>Works out whether a shop earns the green sticker, and what is still outstanding if not.</summary>
    public static Result Evaluate(SpazaShop shop, IEnumerable<ShopDocument> documents, DateOnly today)
    {
        var docs = documents.ToList();
        var outstanding = new List<string>();

        if (shop.Status == "suspended")
            return new Result(false, "This shop's SpazaSure account is suspended.", ["Account suspended"], null);
        if (shop.Status != "verified")
            outstanding.Add("Shop verification by SpazaSure");

        DateOnly? validUntil = null;
        foreach (var (label, types) in RequiredDocuments)
        {
            var doc = docs.Where(d => types.Contains(d.DocType))
                .OrderByDescending(d => d.Status == "approved").ThenByDescending(d => d.UpdatedAt)
                .FirstOrDefault();
            if (doc is null || doc.Status != "approved")
                outstanding.Add(doc?.Status == "pending" ? $"{label} (waiting for review)" : label);
            else if (doc.ExpiryDate is DateOnly expiry && expiry < today)
                outstanding.Add($"{label} (expired)");
            else if (doc.ExpiryDate is DateOnly valid && (validUntil is null || valid < validUntil))
                validUntil = valid;
        }

        return outstanding.Count == 0
            ? new Result(true, "This shop is SpazaSure Verified: its business permit, health certificate and owner ID have been checked.", outstanding, validUntil)
            : new Result(false, "This shop is registered with SpazaSure but is not verified right now.", outstanding, null);
    }
}
