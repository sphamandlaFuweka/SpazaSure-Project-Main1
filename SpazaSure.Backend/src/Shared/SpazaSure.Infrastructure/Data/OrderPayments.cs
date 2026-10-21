using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Marks a Paystack checkout's orders as paid once Paystack has confirmed the payment. Used by the
/// Paystack webhook, and as a fallback when the shop comes back from paying or a supplier accepts an
/// order, so a missed or late webhook never leaves a paid order looking unpaid.
/// </summary>
public static class OrderPayments
{
    /// <summary>
    /// Whether Paystack's confirmed amount (in cents) covers the orders. More is fine: Paystack can
    /// add its fees on top when they are passed to the customer.
    /// </summary>
    public static bool CoversOrders(long paidMinorUnits, IEnumerable<Order> orders) =>
        paidMinorUnits >= decimal.ToInt64(decimal.Round(orders.Sum(o => o.TotalAmount) * 100m, 0, MidpointRounding.AwayFromZero));

    /// <summary>All orders paid for in one Paystack checkout, with what notifications need.</summary>
    public static Task<List<Order>> ForReferenceAsync(SpazaSureDbContext db, string reference, CancellationToken ct) =>
        db.Orders.Include(o => o.Supplier).Include(o => o.Shop)
            .Where(o => o.PaymentProviderReference == reference)
            .ToListAsync(ct);

    /// <summary>
    /// Marks the unpaid orders paid, takes their stock, saves, and tells each supplier and the shop.
    /// Returns the orders that were newly marked paid (none if they already were).
    /// </summary>
    public static async Task<List<Order>> MarkPaidAsync(
        SpazaSureDbContext db, EventPublisher events, IReadOnlyCollection<Order> orders, CancellationToken ct)
    {
        var unpaid = orders.Where(o => o.PaymentStatus != "paid").ToList();
        if (unpaid.Count == 0) return unpaid;

        foreach (var order in unpaid)
        {
            var heldStock = OrderStock.Holds(order);
            order.PaymentMethod = "paystack";
            order.PaymentStatus = "paid";
            if (!string.IsNullOrWhiteSpace(order.PaystackSplitCode))
            {
                order.PaidOutAt = DateTime.UtcNow;
                order.PayoutReference = order.PaymentProviderReference;
            }
            // Card orders take their stock once the payment is confirmed
            await OrderStock.SyncAsync(db, order, heldStock, ct);
        }
        await db.SaveChangesAsync(ct);

        foreach (var order in unpaid)
        {
            events.PublishNotification(
                supplierId: order.Supplier.UserId.ToString(),
                type: "order",
                title: "New Order Received",
                message: $"{order.Shop.ShopName} placed order {order.OrderNumber}. Product share: R{order.Subtotal:F2}."
                    + (string.IsNullOrWhiteSpace(order.PaystackSplitCode)
                        ? ""
                        : " Your share was allocated to your Paystack subaccount."),
                priority: order.Subtotal > 1000 ? "high" : "normal",
                referenceId: order.Id.ToString(),
                routingKey: "order.placed");
        }
        // Tell the shop its payment went through (one message per checkout)
        events.PublishNotification(
            supplierId: unpaid[0].Shop.UserId.ToString(),
            type: "order",
            title: "Payment received",
            message: $"We received your payment of R{unpaid.Sum(o => o.TotalAmount):F2} for {string.Join(", ", unpaid.Select(o => o.OrderNumber))}. Your supplier has been notified.",
            referenceId: unpaid[0].Id.ToString(),
            routingKey: "order.paid");
        return unpaid;
    }
}
