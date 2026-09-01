using Microsoft.Extensions.Options;

namespace SpazaSure.OrderService.Services;

public sealed class DeliveryPricingOptions
{
    public decimal BaseFee { get; set; } = 50m;
    public decimal PerKmRate { get; set; } = 8m;
}

public sealed record DeliveryPricingResult(decimal DistanceKm, decimal DeliveryFee);

public sealed class DeliveryPricingService
{
    private const double EarthRadiusKm = 6371d;
    private readonly DeliveryPricingOptions _options;

    public DeliveryPricingService(IOptions<DeliveryPricingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Value.BaseFee < 0 || options.Value.PerKmRate < 0)
            throw new ArgumentException("Delivery pricing values cannot be negative.", nameof(options));

        _options = options.Value;
    }

    public DeliveryPricingResult Calculate(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        ValidateCoordinates(originLatitude, originLongitude);
        ValidateCoordinates(destinationLatitude, destinationLongitude);

        var latitudeDelta = ToRadians(destinationLatitude - originLatitude);
        var longitudeDelta = ToRadians(destinationLongitude - originLongitude);
        var originLatitudeRadians = ToRadians(originLatitude);
        var destinationLatitudeRadians = ToRadians(destinationLatitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d)
            + Math.Cos(originLatitudeRadians) * Math.Cos(destinationLatitudeRadians)
            * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        var centralAngle = 2d * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(Math.Max(0d, 1d - haversine)));
        var distanceKm = (decimal)(EarthRadiusKm * centralAngle);
        var fee = _options.BaseFee + distanceKm * _options.PerKmRate;

        return new DeliveryPricingResult(
            decimal.Round(distanceKm, 2, MidpointRounding.AwayFromZero),
            decimal.Round(fee, 2, MidpointRounding.AwayFromZero));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90d or > 90d)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if (!double.IsFinite(longitude) || longitude is < -180d or > 180d)
            throw new ArgumentOutOfRangeException(nameof(longitude));
    }
}
