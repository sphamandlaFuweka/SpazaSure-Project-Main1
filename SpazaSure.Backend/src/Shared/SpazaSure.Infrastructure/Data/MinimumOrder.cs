namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Shops order at least <see cref="PlatformRules.MinOrderAmount"/> (R2,000 unless an admin changes
/// it) from each supplier, counting the products at the prices shops pay, not delivery. Bulk Buyer
/// shoppers have their own minimum (see <see cref="BulkBuying.MinOrderAmount"/>).
/// </summary>
public static class MinimumOrder
{
    /// <summary>Why this supplier's order is too small, or null when it is big enough.</summary>
    public static string? Problem(PlatformRules rules, string supplierName, decimal productsTotal) =>
        Problem(rules.MinOrderAmount, supplierName, productsTotal);

    public static string? Problem(decimal minimum, string supplierName, decimal productsTotal)
    {
        if (minimum <= 0 || productsTotal >= minimum) return null;
        return $"The minimum order from {supplierName} is R{minimum:N2} in products (delivery not included). " +
               $"Add R{minimum - productsTotal:N2} more.";
    }
}
