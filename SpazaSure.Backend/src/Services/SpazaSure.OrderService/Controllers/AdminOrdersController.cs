using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.OrderService.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "admin")]
public class AdminOrdersController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    /// <summary>
    /// SpazaSure's delivery run: orders ready to collect from suppliers, and orders out for delivery,
    /// with both addresses and phone numbers. Pickup orders (the shop collects) aren't included.
    /// </summary>
    [HttpGet("deliveries")]
    public async Task<IActionResult> Deliveries()
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => (o.Status == "ready" || o.Status == "dispatched") && o.DeliveryType != "pickup")
            .OrderBy(o => o.Status == "dispatched").ThenBy(o => o.UpdatedAt)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.Status, o.DeliveryType, o.PaymentMethod, o.PaymentStatus, o.TotalAmount,
                o.UpdatedAt, o.DeliveryCode,
                items = o.Items.Sum(i => i.Quantity),
                supplier = new { o.Supplier.CompanyName, o.Supplier.Phone, o.Supplier.Address, o.Supplier.City },
                shop = new { o.Shop.ShopName, o.Shop.OwnerName, o.Shop.Phone, address = o.DeliveryAddress ?? o.Shop.Address, o.Shop.City },
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(orders));
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var query = db.Orders
            .Include(o => o.Shop)
            .Include(o => o.Supplier)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && status != "all")
            query = query.Where(o => o.Status == status);

        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.Shop.ShopName.ToLower().Contains(term) ||
                o.Supplier.CompanyName.ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.TotalAmount,
                o.Subtotal,
                OriginalSubtotal = o.Items.Sum(i => i.OriginalUnitPrice * i.Quantity),
                TotalDiscount = o.Items.Sum(i => (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity),
                o.DeliveryFee,
                o.PlatformCommission,
                o.DeliveryType,
                o.DeliveryAddress,
                o.DeliveryLatitude,
                o.DeliveryLongitude,
                o.DeliveryDistanceKm,
                o.PaymentMethod,
                o.PaymentStatus,
                o.GroupBuyId,
                IsGroupBuy = o.GroupBuyId.HasValue,
                o.RejectionReason,
                o.CreatedAt,
                o.UpdatedAt,
                ShopId = o.ShopId,
                ShopName = o.Shop.ShopName,
                ShopAddress = o.Shop.Address ?? "",
                SupplierId = o.SupplierId,
                SupplierName = o.Supplier.CompanyName,
                Items = o.Items.Select(i => new
                {
                    i.ProductId,
                    ProductName = i.Product.Name,
                    i.Quantity,
                    i.OriginalUnitPrice,
                    i.UnitPrice,
                    i.DiscountPct,
                    DiscountAmount = i.OriginalUnitPrice - i.UnitPrice,
                    OriginalLineTotal = i.OriginalUnitPrice * i.Quantity,
                    DiscountLineAmount = (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity,
                    i.LineTotal
                })
            })
            .ToListAsync();

        // Summary stats
        var allOrders = await db.Orders.ToListAsync();
        var summary = new
        {
            totalOrders = allOrders.Count,
            pendingCount = allOrders.Count(o => o.Status == "pending"),
            deliveredRevenue = allOrders.Where(o => o.Status == "delivered").Sum(o => o.TotalAmount),
            totalPlatformFees = allOrders.Sum(o => o.PlatformCommission)
        };

        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, items, summary }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var order = await db.Orders
            .Include(o => o.Shop)
            .Include(o => o.Supplier)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        return Ok(ApiResponse<object>.Ok(new
        {
            order.Id,
            order.OrderNumber,
            order.Status,
            order.TotalAmount,
            order.Subtotal,
            OriginalSubtotal = order.Items.Sum(i => i.OriginalUnitPrice * i.Quantity),
            TotalDiscount = order.Items.Sum(i => (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity),
            order.DeliveryFee,
            order.PlatformCommission,
            order.DeliveryType,
            order.DeliveryAddress,
            order.DeliveryLatitude,
            order.DeliveryLongitude,
            order.DeliveryDistanceKm,
            order.PaymentMethod,
            order.PaymentStatus,
            order.DeliveryCode,
            order.GroupBuyId,
            IsGroupBuy = order.GroupBuyId.HasValue,
            order.RejectionReason,
            order.CreatedAt,
            order.UpdatedAt,
            ShopId = order.ShopId,
            ShopName = order.Shop.ShopName,
            ShopPhone = order.Shop.Phone,
            ShopAddress = order.Shop.Address ?? "",
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier.CompanyName,
            SupplierPhone = order.Supplier.Phone,
            Items = order.Items.Select(i => new
            {
                i.ProductId,
                ProductName = i.Product.Name,
                i.Quantity,
                i.OriginalUnitPrice,
                i.UnitPrice,
                i.DiscountPct,
                DiscountAmount = i.OriginalUnitPrice - i.UnitPrice,
                OriginalLineTotal = i.OriginalUnitPrice * i.Quantity,
                DiscountLineAmount = (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity,
                i.LineTotal
            })
        }));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest req, [FromServices] SpazaSure.OrderService.Services.OrderPaystackPaymentService? paystack = null)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));
        var previousStatus = order.Status;

        var validStatuses = new[] { "pending", "processing", "ready", "dispatched", "delivered", "cancelled" };
        if (!validStatuses.Contains(req.Status))
            return BadRequest(ApiResponse.Fail("Invalid status."));

        // Once the supplier has been paid for an order, its status is locked for accounting
        if (order.PaidOutAt is not null && req.Status != order.Status)
            return BadRequest(ApiResponse.Fail("This order has already been paid out to the supplier, so its status can't be changed."));

        // Reopening a cancelled wallet order charges the wallet again, so the shop must still have the money
        if (order.PaymentMethod == ShopWallet.PaymentMethod && order.Status == "cancelled" && req.Status != "cancelled"
            && await ShopWallet.BalanceAsync(db, order.ShopId) < order.TotalAmount)
            return BadRequest(ApiResponse.Fail("The shop's wallet no longer has enough money to pay for this order again."));

        // Shipping an unpaid card order would hand over goods for free
        if (order.PaymentMethod == "paystack" && order.PaymentStatus != "paid" && req.Status is "processing" or "dispatched" or "delivered")
        {
            // Ask Paystack in case the webhook was missed
            if (paystack is not null && !string.IsNullOrEmpty(order.PaymentProviderReference) &&
                await paystack.ConfirmOrdersAsync(db, events, order.PaymentProviderReference, HttpContext.RequestAborted))
                await db.Entry(order).ReloadAsync();
            if (order.PaymentStatus != "paid")
                return BadRequest(ApiResponse.Fail("Paystack hasn't confirmed this card order's payment yet."));
        }

        var heldStock = OrderStock.Holds(order);
        order.Status = req.Status;
        if (req.Status == "dispatched" && order.PaymentMethod == ShopOrdersController.CashOnDelivery)
            order.DeliveryCode ??= DeliveryCodes.Generate();
        if (req.Status == "cancelled") order.RejectionReason = req.Reason;
        // Payouts are timed from delivery; moving an order out of "delivered" clears it
        order.DeliveredAt = req.Status == "delivered" ? order.DeliveredAt ?? DateTime.UtcNow : null;
        // Cash on delivery is only paid once delivered (SpazaSure collects the cash)
        if (order.PaymentMethod == ShopOrdersController.CashOnDelivery)
            order.PaymentStatus = req.Status == "delivered" ? "paid" : "pending";
        await ShopWallet.SyncOrderAsync(db, order);
        await OrderStock.SyncAsync(db, order, heldStock);
        await db.SaveChangesAsync();

        if (previousStatus != order.Status)
        {
            // Both sides hear about changes an admin makes to their order
            var parties = await db.Orders.Where(o => o.Id == order.Id)
                .Select(o => new { ShopUserId = o.Shop.UserId, SupplierUserId = o.Supplier.UserId })
                .FirstAsync();
            var detail = order.Status switch
            {
                "cancelled" => $"was cancelled by SpazaSure{(string.IsNullOrWhiteSpace(req.Reason) ? "." : $": {req.Reason!.Trim()}")}",
                "dispatched" => "has been collected by SpazaSure and is on its way.",
                "delivered" => "has been delivered by SpazaSure.",
                _ => $"was updated to {order.Status} by SpazaSure.",
            };
            var refund = order.Status == "cancelled" && order.PaymentMethod == ShopWallet.PaymentMethod
                ? $" R{order.TotalAmount:F2} is back in your wallet." : "";
            var priority = order.Status == "cancelled" ? "high" : "normal";
            events.PublishNotification(parties.ShopUserId.ToString(), "order", $"Order {order.Status}",
                $"Your order {order.OrderNumber} {detail}{refund}", priority, order.Id.ToString(), $"order.{order.Status}");
            events.PublishNotification(parties.SupplierUserId.ToString(), "order", $"Order {order.Status}",
                $"Order {order.OrderNumber} {detail}", priority, order.Id.ToString(), $"order.{order.Status}");
        }

        return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status }));
    }
}
