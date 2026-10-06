using System.Globalization;
using System.Text.Json;

namespace SpazaSure.UserService.Services;

/// <summary>
/// Resolves a South African address to coordinates with OpenStreetMap Nominatim.
/// Its usage policy allows at most one request per second, so callers must throttle.
/// </summary>
public sealed class GeocodingService(HttpClient http, ILogger<GeocodingService> logger)
{
    public async Task<(double Lat, double Lng)?> GeocodeAsync(
        string? address, string? city, string? province, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(city)) return null;

        var query = string.Join(", ",
            new[] { address, city, province, "South Africa" }.Where(p => !string.IsNullOrWhiteSpace(p)));
        try
        {
            using var response = await http.GetAsync(
                $"search?format=json&limit=1&countrycodes=za&q={Uri.EscapeDataString(query)}", ct);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var hit = doc.RootElement[0];
            if (double.TryParse(hit.GetProperty("lat").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(hit.GetProperty("lon").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                return (lat, lng);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "Geocoding failed for {Query}", query);
        }
        return null;
    }
}
