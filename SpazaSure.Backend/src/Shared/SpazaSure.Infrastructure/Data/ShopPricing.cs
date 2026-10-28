namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// What shops pay for a product. SpazaSure's platform fee (and any shop service fee) is added to
/// the supplier's price and shown to shops as part of the price, never as a separate fee. The
/// supplier is paid their own price; the difference is SpazaSure's income.
/// </summary>
public static class ShopPricing
{
    /// <summary>The fraction added to supplier prices, e.g. 0.05 for 5%.</summary>
    public static decimal Markup(PlatformRules rules) =>
        (rules.PlatformFeePercent + rules.ShopServiceFeePercent) / 100m;

    /// <summary>A supplier price as shops see it, rounded to the cent.</summary>
    public static decimal ShopPrice(decimal supplierPrice, decimal markup) =>
        Math.Round(supplierPrice * (1m + markup), 2, MidpointRounding.AwayFromZero);

    public static decimal ShopPrice(decimal supplierPrice, PlatformRules rules) =>
        ShopPrice(supplierPrice, Markup(rules));

    /// <summary>
    /// Prices one order line: the shop pays ShopPrice per unit, the supplier gets their own
    /// price, and the platform fee is exactly what's left, so receipts add up to the cent.
    /// </summary>
    public static (decimal ShopUnitPrice, decimal ShopLineTotal) Line(decimal supplierUnitPrice, int quantity, decimal markup)
    {
        var shopUnit = ShopPrice(supplierUnitPrice, markup);
        return (shopUnit, shopUnit * quantity);
    }
}
