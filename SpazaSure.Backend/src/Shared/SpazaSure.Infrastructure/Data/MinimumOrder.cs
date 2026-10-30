namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Shops order at least <see cref="PlatformRules.MinOrderAmount"/> (R2,000 unless an admin changes
/// it) from each supplier, counting the products at the prices shops pay, not delivery.
/// </summary>
public static class MinimumOrder
{
    /// <summary>Why this supplier's order is too small, or null when it is big enough.</summary>
    public static string? Problem(PlatformRules rules, string supplierName, decimal productsTotal)
    {
        if (rules.MinOrderAmount <= 0 || productsTotal >= rules.MinOrderAmount) return null;
        return $"The minimum order from {supplierName} is R{rules.MinOrderAmount:N2} in products (delivery not included). " +
               $"Add R{rules.MinOrderAmount - productsTotal:N2} more.";
    }
}
