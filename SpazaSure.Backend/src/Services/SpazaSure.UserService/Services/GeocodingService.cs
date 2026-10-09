using System.Globalization;
using System.Text.Json;

namespace SpazaSure.UserService.Services;

/// <summary>Resolves a street address to coordinates through Nominatim (max 1 request/second).</summary>
public class GeocodingService(HttpClient http, ILogger<GeocodingService> logger)
{
    public async Task<(double Lat, double Lng)?> GeocodeAsync(
        string? address, string? city, string? province, string? postalCode, CancellationToken ct = default)
    {
        var parts = new[] { address, city, province, postalCode }
            .Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (parts.Count == 0) return null;

        var query = string.Join(", ", parts) + ", South Africa";
        var url = $"https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=za&q={Uri.EscapeDataString(query)}";

        try
        {
            using var res = await http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
            var first = doc.RootElement[0];
            var lat = double.Parse(first.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
            var lng = double.Parse(first.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
            return (lat, lng);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Geocoding failed for {Query}", query);
            return null;
        }
    }
}
