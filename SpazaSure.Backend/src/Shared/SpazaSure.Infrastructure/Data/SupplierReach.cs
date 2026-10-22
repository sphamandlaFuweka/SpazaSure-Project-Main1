using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Which suppliers a shop may see and buy from: the same province, and within
/// PlatformRules.SupplierReachKm when both have a map position. Without a position on either side
/// the province decides alone, and a side with no province isn't excluded on province.
/// </summary>
public static class SupplierReach
{
    private const double EarthRadiusKm = 6371d;

    /// <summary>Province abbreviations people type instead of the full name.</summary>
    private static readonly Dictionary<string, string> Abbreviations = new()
    {
        ["gp"] = "gauteng", ["kzn"] = "kwazulunatal", ["wc"] = "westerncape", ["ec"] = "easterncape",
        ["nc"] = "northerncape", ["nw"] = "northwest", ["fs"] = "freestate", ["lp"] = "limpopo",
        ["mp"] = "mpumalanga",
    };

    public static bool CanServe(SpazaShop shop, Supplier supplier, decimal reachKm)
    {
        var shopProvince = NormalizeProvince(shop.Province);
        var supplierProvince = NormalizeProvince(supplier.Province);
        if (shopProvince.Length > 0 && supplierProvince.Length > 0 && shopProvince != supplierProvince)
            return false;

        if (shop.Latitude is { } shopLat && shop.Longitude is { } shopLng &&
            supplier.Latitude is { } supplierLat && supplier.Longitude is { } supplierLng)
            return DistanceKm(shopLat, shopLng, supplierLat, supplierLng) <= (double)reachKm;

        return true;
    }

    /// <summary>Ids of the active suppliers this shop can buy from.</summary>
    public static async Task<HashSet<Guid>> ReachableSupplierIdsAsync(
        SpazaSureDbContext db, SpazaShop shop, decimal reachKm, CancellationToken ct = default)
    {
        var suppliers = await db.Suppliers.AsNoTracking()
            .Where(s => s.Status == "active" || s.Status == "verified")
            .Select(s => new Supplier { Id = s.Id, Province = s.Province, Latitude = s.Latitude, Longitude = s.Longitude })
            .ToListAsync(ct);
        return suppliers.Where(s => CanServe(shop, s, reachKm)).Select(s => s.Id).ToHashSet();
    }

    /// <summary>"KwaZulu-Natal", "Kwazulu Natal" and "KZN" all become "kwazulunatal".</summary>
    public static string NormalizeProvince(string? province)
    {
        var letters = new string((province ?? "").Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
        return Abbreviations.GetValueOrDefault(letters, letters);
    }

    /// <summary>Straight-line (haversine) distance in km.</summary>
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double degrees) => degrees * Math.PI / 180d;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Pow(Math.Sin(dLat / 2d), 2d) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLng / 2d), 2d);
        return EarthRadiusKm * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0d, 1d - a)));
    }
}
