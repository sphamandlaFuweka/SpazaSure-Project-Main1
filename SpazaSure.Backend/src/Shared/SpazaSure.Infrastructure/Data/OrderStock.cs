using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Keeps product stock in step with orders. An order holds stock once it is committed: straight
/// away for wallet, cash-on-delivery and group-buy (invoice) orders, and once paid for Paystack
/// orders, so abandoned card checkouts don't lock stock. Cancelling gives the stock back, and
/// reinstating a cancelled order takes it again.
/// </summary>
public static class OrderStock
{
    public static bool Holds(Order order) =>
        order.Status != "cancelled" && (order.PaymentMethod != "paystack" || order.PaymentStatus == "paid");

    /// <summary>
    /// Call after changing an order's status or payment, passing whether it held stock before.
    /// Adjusts the products when that changed; the caller saves.
    /// </summary>
    public static async Task SyncAsync(SpazaSureDbContext db, Order order, bool heldBefore, CancellationToken ct = default)
    {
        var holdsNow = Holds(order);
        if (holdsNow == heldBefore) return;

        var items = order.Items.Count > 0
            ? order.Items.ToList()
            : await db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync(ct);
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product)) continue;
            product.StockQty = holdsNow
                ? Math.Max(0, product.StockQty - item.Quantity)
                : product.StockQty + item.Quantity;
        }
    }
}
