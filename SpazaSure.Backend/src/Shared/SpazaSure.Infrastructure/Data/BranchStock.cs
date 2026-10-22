using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Stock per branch. Product.StockQty is the supplier's total; ProductBranchStock rows say how
/// much of it sits at each branch, and the head office holds the rest. Products without rows
/// aren't tracked per branch, so any location can ship the full total.
/// Callers must load Product.BranchStock.
/// </summary>
public static class BranchStock
{
    public static bool IsTracked(Product product) => product.BranchStock.Count > 0;

    /// <summary>How many units the location (null = head office) can ship.</summary>
    public static int AvailableAt(Product product, Guid? branchId)
    {
        if (!IsTracked(product)) return product.StockQty;
        if (branchId is { } id) return product.BranchStock.FirstOrDefault(s => s.SupplierBranchId == id)?.StockQty ?? 0;
        return HeadOfficeQty(product);
    }

    public static int HeadOfficeQty(Product product) =>
        Math.Max(0, product.StockQty - product.BranchStock.Sum(s => s.StockQty));

    /// <summary>Takes units from the location for an order; the caller saves.</summary>
    public static void Take(Product product, Guid? branchId, int quantity)
    {
        product.StockQty = Math.Max(0, product.StockQty - quantity);
        if (branchId is { } id && product.BranchStock.FirstOrDefault(s => s.SupplierBranchId == id) is { } row)
            row.StockQty = Math.Max(0, row.StockQty - quantity);
    }

    /// <summary>Puts units back at the location, e.g. when an order is cancelled; the caller saves.</summary>
    public static void Return(Product product, Guid? branchId, int quantity)
    {
        product.StockQty += quantity;
        if (branchId is { } id && product.BranchStock.FirstOrDefault(s => s.SupplierBranchId == id) is { } row)
            row.StockQty += quantity;
    }

    /// <summary>Where an order should ship from, or why none of the supplier's locations can send it.</summary>
    public sealed record Choice(SupplierReach.Location? From, double? DistanceKm, string? Problem);

    /// <summary>
    /// The nearest location that serves the shop and has every line in stock. When the nearest one
    /// is out of something, a further branch that has it ships instead; when none has it, the
    /// problem names the product and the shop's nearest branch.
    /// </summary>
    public static Choice ChooseLocation(
        SpazaShop shop, Supplier supplier, decimal reachKm, IReadOnlyList<(Product Product, int Quantity)> lines)
    {
        var serving = SupplierReach.ServingLocations(shop, supplier, reachKm);
        if (serving.Count == 0)
            return new Choice(null, null, $"{supplier.CompanyName} doesn't deliver to your area.");

        foreach (var location in serving)
            if (lines.All(l => AvailableAt(l.Product, location.BranchId) >= l.Quantity))
                return new Choice(location, SupplierReach.Distance(shop, location), null);

        var nearest = serving[0];
        var (product, quantity) = lines.First(l => AvailableAt(l.Product, nearest.BranchId) < l.Quantity);
        var available = AvailableAt(product, nearest.BranchId);
        var place = nearest.BranchId is null ? $"{supplier.CompanyName}'s head office" : $"{supplier.CompanyName}'s {nearest.Name}";
        return new Choice(null, null, available == 0
            ? $"'{product.Name}' is out of stock at {place}, which delivers to you, and no other branch near you has it."
            : $"{place} only has {available} of '{product.Name}' (you asked for {quantity}), and no other branch near you has enough.");
    }
}

/// <summary>
/// Delivery fees for normal orders. Distance from the shipping branch decides the fee: the admin's
/// standard fee covers nearby shops, and further shops pay the per-km rate instead when that is
/// more. Express adds the difference between the express and standard fees. Without map
/// positions the flat fees apply, as before.
/// </summary>
public static class DeliveryFees
{
    public static decimal For(PlatformRules rules, string? deliveryType, double? distanceKm)
    {
        var type = deliveryType?.Trim().ToLowerInvariant();
        if (type == "pickup") return 0m;

        var standard = rules.StandardDeliveryFee;
        if (distanceKm is { } km)
            standard = Math.Max(standard, Math.Round((decimal)km * rules.DeliveryPerKmRate, 2, MidpointRounding.AwayFromZero));
        return type == "express"
            ? standard + Math.Max(0m, rules.ExpressDeliveryFee - rules.StandardDeliveryFee)
            : standard;
    }
}
