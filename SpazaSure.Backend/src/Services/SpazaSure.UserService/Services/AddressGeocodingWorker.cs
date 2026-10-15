using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;

namespace SpazaSure.UserService.Services;

/// <summary>Lets a request ask the geocoding worker to run now (e.g. right after an address changed).</summary>
public sealed class AddressGeocodingSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void RequestRun()
    {
        if (_signal.CurrentCount == 0) _signal.Release();
    }

    internal Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);
}

/// <summary>
/// Makes sure every shop and supplier address can be shown on the map: finds addresses saved
/// without coordinates and looks them up in the background, so map pages never wait on it.
/// Addresses that can't be found are retried later rather than on every request.
/// </summary>
public sealed class AddressGeocodingWorker(
    IServiceScopeFactory scopes,
    AddressGeocodingSignal signal,
    ILogger<AddressGeocodingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromHours(12);
    private const int BatchSize = 20;

    /// <summary>When each address last failed to geocode, so it isn't retried every few minutes.</summary>
    private readonly Dictionary<Guid, DateTime> _failedAt = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Address geocoding pass failed");
            }
            try
            {
                await signal.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private bool ShouldTry(Guid id) => !_failedAt.TryGetValue(id, out var at) || DateTime.UtcNow - at > RetryAfter;

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpazaSureDbContext>();
        var geocoder = scope.ServiceProvider.GetRequiredService<GeocodingService>();

        var shops = (await db.SpazaShops
                .Where(s => s.Latitude == null || s.Longitude == null)
                .Where(s => s.Address != "" || s.City != null || s.Province != null || s.PostalCode != null)
                .OrderByDescending(s => s.UpdatedAt)
                .Take(BatchSize * 3)
                .ToListAsync(ct))
            .Where(s => ShouldTry(s.Id)).Take(BatchSize).ToList();

        var suppliers = (await db.Suppliers
                .Where(s => s.Latitude == null || s.Longitude == null)
                .Where(s => s.Address != null || s.City != null || s.Province != null)
                .OrderByDescending(s => s.UpdatedAt)
                .Take(BatchSize * 3)
                .ToListAsync(ct))
            .Where(s => ShouldTry(s.Id)).Take(BatchSize).ToList();

        if (shops.Count == 0 && suppliers.Count == 0) return;

        var found = 0;
        foreach (var shop in shops)
        {
            var point = await geocoder.GeocodeAsync(shop.Address, shop.City, shop.Province, shop.PostalCode, ct);
            if (point is { } p) { shop.Latitude = p.Lat; shop.Longitude = p.Lng; found++; }
            else _failedAt[shop.Id] = DateTime.UtcNow;
        }
        foreach (var supplier in suppliers)
        {
            var point = await geocoder.GeocodeAsync(supplier.Address, supplier.City, supplier.Province, null, ct);
            if (point is { } p) { supplier.Latitude = p.Lat; supplier.Longitude = p.Lng; found++; }
            else _failedAt[supplier.Id] = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Geocoded {Found} of {Total} addresses missing map locations",
            found, shops.Count + suppliers.Count);
    }
}
