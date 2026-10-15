using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.UserService.Services;

/// <summary>
/// Resolves a street address to coordinates through Nominatim (OpenStreetMap, max 1 request/second).
/// OpenStreetMap often doesn't know individual house numbers in townships, so when the exact
/// address isn't found it falls back to the street, then the suburb/area, then the town, so the
/// place can still be shown on the map (approximately).
/// </summary>
public partial class GeocodingService(HttpClient http, ILogger<GeocodingService> logger)
{
    /// <summary>Nominatim's usage policy allows one request per second per application.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastRequest = DateTime.MinValue;

    public async Task<(double Lat, double Lng)?> GeocodeAsync(
        string? address, string? city, string? province, string? postalCode, CancellationToken ct = default)
    {
        foreach (var query in Candidates(address, city, province, postalCode))
        {
            var point = await SearchAsync(query, ct);
            if (point is not null) return point;
        }
        return null;
    }

    /// <summary>Queries from most to least precise, without duplicates.</summary>
    public static IEnumerable<string> Candidates(string? address, string? city, string? province, string? postalCode)
    {
        static string Clean(string? s) => (s ?? "").Trim().Trim(',').Trim();
        var addr = Clean(address);
        var town = Clean(city);
        var prov = Clean(province);
        var code = Clean(postalCode);

        // The street line without a leading house/stand number ("1234 Vilakazi St" -> "Vilakazi St")
        var parts = addr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var street = parts.Count > 0 ? LeadingNumber().Replace(parts[0], "").Trim() : "";
        var area = parts.Count > 1 ? parts[1] : "";

        string Join(params string[] p) =>
            string.Join(", ", p.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));

        var queries = new[]
        {
            Join(addr, town, prov, code),
            Join(addr, town, prov),
            Join(street, area, town, prov),
            Join(area, town, prov),
            Join(town, prov, code),
            Join(code, prov),
        };
        return queries
            .Where(q => q.Length >= 3)
            .Select(q => q + ", South Africa")
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(double Lat, double Lng)?> SearchAsync(string query, CancellationToken ct)
    {
        var url = $"https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=za&q={Uri.EscapeDataString(query)}";
        await Gate.WaitAsync(ct);
        try
        {
            var wait = _lastRequest.AddMilliseconds(1100) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _lastRequest = DateTime.UtcNow;

            using var res = await http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
            var first = doc.RootElement[0];
            var lat = double.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
            var lng = double.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
            return AddressValidation.ValidateCoordinates(lat, lng) is null ? (lat, lng) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Geocoding failed for {Query}", query);
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    [GeneratedRegex(@"^\s*(no\.?\s*)?\d+[a-z]?\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingNumber();
}
