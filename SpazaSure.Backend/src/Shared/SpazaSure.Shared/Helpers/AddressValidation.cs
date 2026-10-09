namespace SpazaSure.Shared.Helpers;

public static class AddressValidation
{
    // Rough bounding box of South Africa including Lesotho and Eswatini.
    private const double MinLat = -35.0, MaxLat = -22.0, MinLng = 16.0, MaxLng = 33.0;

    /// <summary>Returns an error message, or null when the pair is absent or plausible.</summary>
    public static string? ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude is null && longitude is null) return null;
        if (latitude is null || longitude is null)
            return "Both latitude and longitude are required for a map location.";
        if (!double.IsFinite(latitude.Value) || !double.IsFinite(longitude.Value)
            || latitude < MinLat || latitude > MaxLat || longitude < MinLng || longitude > MaxLng)
            return "The selected address is outside South Africa.";
        return null;
    }
}
