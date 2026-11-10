namespace SpazaSure.Infrastructure.Entities;

/// <summary>The kinds of buyer account stored in spaza_shops.</summary>
public static class ShopAccountTypes
{
    /// <summary>A spaza shop.</summary>
    public const string Shop = "shop";

    /// <summary>A shopper on the Bulk Buyer plan, buying in bulk for their household.</summary>
    public const string BulkBuyer = "bulk_buyer";
}
