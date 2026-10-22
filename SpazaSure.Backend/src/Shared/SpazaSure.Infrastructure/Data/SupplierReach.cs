using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Which suppliers a shop may see and buy from. A supplier delivers from its head office and from
/// each active branch (warehouse); it can serve a shop when any of those places is in the shop's
/// province and within PlatformRules.SupplierReachKm. Without a position on either side the
/// province decides alone, and a side with no province isn't excluded on province.
/// Callers must load Supplier.Branches, or only the head office counts.
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

    /// <summary>A place a supplier delivers from. BranchId is null for the head office.</summary>
    public sealed record Location(Guid? BranchId, string Name, string? City, string? Province, double? Latitude, double? Longitude);

    /// <summary>The head office plus every active branch.</summary>
    public static IEnumerable<Location> Locations(Supplier supplier)
    {
        yield return new Location(null, "Head office", supplier.City, supplier.Province, supplier.Latitude, supplier.Longitude);
        foreach (var b in supplier.Branches.Where(b => b.IsActive))
            yield return new Location(b.Id, b.Name, b.City, b.Province, b.Latitude, b.Longitude);
    }

    public static bool CanServe(SpazaShop shop, Supplier supplier, decimal reachKm) =>
        NearestServing(shop, supplier, reachKm) is not null;

    /// <summary>The closest of the supplier's locations that can serve the shop, or null when none can.</summary>
    public static Location? NearestServing(SpazaShop shop, Supplier supplier, decimal reachKm) =>
        Locations(supplier)
            .Where(l => CanServe(shop, l, reachKm))
            .OrderBy(l => Distance(shop, l) ?? double.MaxValue)
            .FirstOrDefault();

    private static bool CanServe(SpazaShop shop, Location location, decimal reachKm)
    {
        if (!SameProvince(shop.Province, location.Province)) return false;
        return Distance(shop, location) is not { } km || km <= (double)reachKm;
    }

    private static double? Distance(SpazaShop shop, Location location) =>
        shop.Latitude is { } shopLat && shop.Longitude is { } shopLng &&
        location.Latitude is { } lat && location.Longitude is { } lng
            ? DistanceKm(shopLat, shopLng, lat, lng)
            : null;

    /// <summary>Ids of the active suppliers this shop can buy from.</summary>
    public static async Task<HashSet<Guid>> ReachableSupplierIdsAsync(
        SpazaSureDbContext db, SpazaShop shop, decimal reachKm, CancellationToken ct = default)
    {
        var suppliers = await db.Suppliers.AsNoTracking()
            .Where(s => s.Status == "active" || s.Status == "verified")
            .Select(s => new Supplier
            {
                Id = s.Id, City = s.City, Province = s.Province, Latitude = s.Latitude, Longitude = s.Longitude,
                Branches = s.Branches.Where(b => b.IsActive).ToList(),
            })
            .ToListAsync(ct);
        return suppliers.Where(s => CanServe(shop, s, reachKm)).Select(s => s.Id).ToHashSet();
    }

    /// <summary>False only when both provinces are known and differ.</summary>
    public static bool SameProvince(string? a, string? b)
    {
        var first = NormalizeProvince(a);
        var second = NormalizeProvince(b);
        return first.Length == 0 || second.Length == 0 || first == second;
    }

    /// <summary>"KwaZulu-Natal", "Kwazulu Natal" and "KZN" all become "kwazulunatal".</summary>
    public static string NormalizeProvince(string? province)
    {
        var letters = Letters(province);
        return Abbreviations.GetValueOrDefault(letters, letters);
    }

    /// <summary>"Cape Town", "cape-town" and "CAPE TOWN" all become "capetown".</summary>
    public static string NormalizeCity(string? city) => Letters(city);

    private static string Letters(string? text) =>
        new((text ?? "").Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());

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

/// <summary>
/// Where a group buy is open: shops in its province and within its radius of the supplying
/// branch. When either side has no map position, the shop must be in the same city instead.
/// Campaigns from before areas existed fall back to the supplier's normal reach.
/// </summary>
public static class GroupBuyArea
{
    public const decimal MaxRadiusKm = 300m;

    public static bool HasArea(GroupBuy groupBuy) => groupBuy.AreaRadiusKm is not null;

    /// <summary>Needs groupBuy.Supplier (with Branches) loaded for campaigns without an area.</summary>
    public static bool Contains(GroupBuy groupBuy, SpazaShop shop, decimal supplierReachKm)
    {
        if (!HasArea(groupBuy)) return SupplierReach.CanServe(shop, groupBuy.Supplier, supplierReachKm);
        if (!SupplierReach.SameProvince(shop.Province, groupBuy.AreaProvince)) return false;

        if (shop.Latitude is { } shopLat && shop.Longitude is { } shopLng &&
            groupBuy.AreaLatitude is { } lat && groupBuy.AreaLongitude is { } lng)
            return SupplierReach.DistanceKm(shopLat, shopLng, lat, lng) <= (double)groupBuy.AreaRadiusKm!.Value;

        var shopCity = SupplierReach.NormalizeCity(shop.City);
        var areaCity = SupplierReach.NormalizeCity(groupBuy.AreaCity);
        return shopCity.Length > 0 && shopCity == areaCity;
    }

    /// <summary>"shops within 40 km of Durban, KwaZulu-Natal", for messages and the advert page.</summary>
    public static string Describe(GroupBuy groupBuy)
    {
        if (!HasArea(groupBuy)) return "shops near the supplier";
        var place = string.Join(", ", new[] { groupBuy.AreaCity, groupBuy.AreaProvince }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return $"shops within {groupBuy.AreaRadiusKm:0.#} km of {(place.Length > 0 ? place : "the supplier")}";
    }
}
