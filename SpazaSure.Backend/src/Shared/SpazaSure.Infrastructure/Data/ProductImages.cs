using System.Text.Json;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Product photos and supplier logos uploaded in the portal are stored in the database as data URIs,
/// often megabytes each. Lists send a link to each photo instead, served (and cached) by
/// ProductImagesController, so a page of products stays small.
/// </summary>
public static class ProductImages
{
    /// <summary>Lists send the stored value as it is while it's this short (web addresses, not photos).</summary>
    public const int InlineLimit = 4096;

    /// <summary>Under the marketplace prefix, which the gateway already sends to ProductService.</summary>
    public const string Route = "/api/shop/marketplace/images";

    private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif",
    };

    // The version changes when the photo does, so the links can be cached for good
    public static string Link(string apiBase, Guid productId, int index, int version) =>
        $"{apiBase}{Route}/{productId}/{index}?v={version}";

    public static string LogoLink(string apiBase, Guid supplierId, int version) =>
        $"{apiBase}{Route}/logos/{supplierId}?v={version}";

    /// <summary>
    /// For list queries, which select the images only when short (else null), their length and the
    /// number of quotes in them, so the photos never leave the database. Long ones become a link
    /// per entry: each entry is one quoted string, and neither base64 nor addresses hold quotes.
    /// </summary>
    public static string ForList(string apiBase, Guid productId, string? inline, int length, int quotes) =>
        inline ?? JsonSerializer.Serialize(
            Enumerable.Range(0, Math.Max(quotes / 2, 1)).Select(i => Link(apiBase, productId, i, length)));

    /// <summary>Same for a supplier logo; length 0 means there is none.</summary>
    public static string? LogoForList(string apiBase, Guid supplierId, string? inline, int length) =>
        length == 0 ? null : inline ?? LogoLink(apiBase, supplierId, length);

    /// <summary>One product's images, with every stored photo swapped for its link.</summary>
    public static List<string> Links(string apiBase, Guid productId, string? images)
    {
        var list = Parse(images);
        var version = images?.Length ?? 0;
        return list.Select((entry, i) => IsData(entry) ? Link(apiBase, productId, i, version) : entry).ToList();
    }

    public static string WithLinks(string apiBase, Guid productId, string? images) =>
        JsonSerializer.Serialize(Links(apiBase, productId, images));

    /// <summary>
    /// What to store when a form sends the images back: links to this product's own photos are
    /// swapped for the photos they point at. Null keeps the current images.
    /// </summary>
    public static string Keep(string? incoming, string existing, Guid productId)
    {
        if (incoming is null) return existing;
        List<string> sent;
        try { sent = JsonSerializer.Deserialize<List<string>>(incoming) ?? []; }
        catch (JsonException) { return incoming; }
        var current = Parse(existing);
        var own = $"{Route}/{productId}/";
        var kept = new List<string>();
        foreach (var entry in sent)
        {
            var at = entry.IndexOf(own, StringComparison.OrdinalIgnoreCase);
            if (at < 0) { kept.Add(entry); continue; }
            var rest = entry[(at + own.Length)..].Split('?')[0];
            if (int.TryParse(rest, out var index) && index >= 0 && index < current.Count) kept.Add(current[index]);
        }
        return JsonSerializer.Serialize(kept);
    }

    public static List<string> Parse(string? images)
    {
        if (string.IsNullOrWhiteSpace(images)) return [];
        try { return (JsonSerializer.Deserialize<List<string>>(images) ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(); }
        catch (JsonException) { return images.StartsWith("http") || IsData(images) ? [images] : []; }
    }

    public static bool IsData(string? entry) => entry?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The bytes of a stored photo. Only plain image types: anything else isn't served.</summary>
    public static (byte[] Bytes, string ContentType)? Decode(string? entry)
    {
        if (!IsData(entry)) return null;
        var comma = entry!.IndexOf(',');
        if (comma < 0) return null;
        var header = entry[5..comma];
        if (!header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
        var type = header[..^7].Split(';')[0].Trim();
        if (!ImageTypes.Contains(type)) return null;
        try { return (Convert.FromBase64String(entry[(comma + 1)..]), type.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : type); }
        catch (FormatException) { return null; }
    }
}
