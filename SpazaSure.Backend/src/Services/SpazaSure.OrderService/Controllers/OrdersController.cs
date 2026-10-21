using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SpazaSure.OrderService.Controllers;

[ApiController]
[Route("api/supplier/orders")]
[Authorize]
public class OrdersController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var query = db.Orders
            .Include(o => o.Shop)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.SupplierId == supplier.Id);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                OrderNo = o.OrderNumber,
                o.ShopId,
                ShopName = o.Shop.ShopName,
                ShopAddress = o.Shop.Address,
                o.SupplierId,
                SupplierName = o.Supplier.CompanyName,
                o.GroupBuyId,
                o.GroupBuyParticipantId,
                IsGroupBuy = o.GroupBuyId.HasValue,
                o.Subtotal,
                OriginalSubtotal = o.Items.Sum(i => i.OriginalUnitPrice * i.Quantity),
                TotalDiscount = o.Items.Sum(i => (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity),
                o.Status,
                o.DeliveryType,
                Type = o.DeliveryType,
                o.DeliveryAddress,
                o.DeliveryLatitude,
                o.DeliveryLongitude,
                o.DeliveryDistanceKm,
                o.PaymentMethod,
                o.PaymentStatus,
                DirectPaystackSplit = o.PaystackSplitCode != null,
                o.Notes,
                o.RejectionReason,
                o.CreatedAt,
                o.UpdatedAt,
                Products = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = i.Product.Name,
                    ProductImage = i.Product.Images,
                    i.Quantity,
                    i.OriginalUnitPrice,
                    i.UnitPrice,
                    Price = i.UnitPrice,
                    i.DiscountPct,
                    DiscountAmount = i.OriginalUnitPrice - i.UnitPrice,
                    OriginalLineTotal = i.OriginalUnitPrice * i.Quantity,
                    DiscountLineAmount = (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity,
                    i.LineTotal
                }),
                Items = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = i.Product.Name,
                    ProductImage = i.Product.Images,
                    i.Quantity,
                    i.OriginalUnitPrice,
                    i.UnitPrice,
                    Price = i.UnitPrice,
                    i.DiscountPct,
                    DiscountAmount = i.OriginalUnitPrice - i.UnitPrice,
                    OriginalLineTotal = i.OriginalUnitPrice * i.Quantity,
                    DiscountLineAmount = (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity,
                    i.LineTotal
                })
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, items }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var order = await db.Orders
            .Where(o => o.Id == id && o.SupplierId == supplier.Id)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                OrderNo = o.OrderNumber,
                o.ShopId,
                ShopName = o.Shop.ShopName,
                ShopAddress = o.Shop.Address,
                o.SupplierId,
                SupplierName = o.Supplier.CompanyName,
                o.GroupBuyId,
                o.GroupBuyParticipantId,
                IsGroupBuy = o.GroupBuyId.HasValue,
                o.Subtotal,
                OriginalSubtotal = o.Items.Sum(i => i.OriginalUnitPrice * i.Quantity),
                TotalDiscount = o.Items.Sum(i => (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity),
                o.Status,
                o.DeliveryType,
                Type = o.DeliveryType,
                o.DeliveryAddress,
                o.DeliveryLatitude,
                o.DeliveryLongitude,
                o.DeliveryDistanceKm,
                o.PaymentMethod,
                o.PaymentStatus,
                DirectPaystackSplit = o.PaystackSplitCode != null,
                o.Notes,
                o.RejectionReason,
                o.CreatedAt,
                o.UpdatedAt,
                Products = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = i.Product.Name,
                    ProductImage = i.Product.Images,
                    i.Quantity,
                    i.OriginalUnitPrice,
                    i.UnitPrice,
                    Price = i.UnitPrice,
                    i.DiscountPct,
                    DiscountAmount = i.OriginalUnitPrice - i.UnitPrice,
                    OriginalLineTotal = i.OriginalUnitPrice * i.Quantity,
                    DiscountLineAmount = (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity,
                    i.LineTotal
                }),
                Items = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = i.Product.Name,
                    ProductImage = i.Product.Images,
                    i.Quantity,
                    i.OriginalUnitPrice,
                    i.UnitPrice,
                    Price = i.UnitPrice,
                    i.DiscountPct,
                    DiscountAmount = i.OriginalUnitPrice - i.UnitPrice,
                    OriginalLineTotal = i.OriginalUnitPrice * i.Quantity,
                    DiscountLineAmount = (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity,
                    i.LineTotal
                })
            })
            .FirstOrDefaultAsync();

        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));
        return Ok(ApiResponse<object>.Ok(order));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, UpdateStatusRequest req, [FromServices] SpazaSure.OrderService.Services.OrderPaystackPaymentService? paystack = null)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.SupplierId == supplier.Id);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));
        if (order.PaymentMethod == "paystack" && order.PaymentStatus != "paid" &&
            req.Status is "processing" or "dispatched" or "delivered")
        {
            // The webhook may have been missed or be late: ask Paystack before saying it isn't paid.
            if (paystack is not null && !string.IsNullOrEmpty(order.PaymentProviderReference) &&
                await paystack.ConfirmOrdersAsync(db, events, order.PaymentProviderReference, HttpContext.RequestAborted))
                await db.Entry(order).ReloadAsync();
            if (order.PaymentStatus != "paid")
                return BadRequest(ApiResponse.Fail(
                    "Paystack hasn't confirmed this order's payment yet. If the shop has just paid, try again in a minute."));
        }

        // SpazaSure collects from the supplier and delivers. The supplier accepts, packs the order and
        // marks it ready for collection; only pickup orders (the shop collects) does the supplier complete.
        var allowed = new Dictionary<string, string[]>
        {
            ["pending"]    = ["processing", "cancelled"],
            ["confirmed"]  = ["processing", "cancelled"],
            ["processing"] = ["ready", "cancelled"],
            ["ready"]      = ["delivered"],
            ["dispatched"] = ["delivered"],
        };

        if (!allowed.TryGetValue(order.Status, out var next) || !next.Contains(req.Status))
            return BadRequest(ApiResponse.Fail($"Cannot transition from '{order.Status}' to '{req.Status}'."));
        if (req.Status == "delivered" && order.DeliveryType != "pickup")
            return BadRequest(ApiResponse.Fail("SpazaSure collects this order from you and delivers it, so SpazaSure marks it delivered."));

        // Cash orders are completed by the shop entering the delivery code, not by the supplier
        if (req.Status == "delivered" && order.PaymentMethod == ShopOrdersController.CashOnDelivery)
            return BadRequest(ApiResponse.Fail("Cash on delivery orders are confirmed by the shop using the delivery code you hand over with the goods."));

        var heldStock = OrderStock.Holds(order);
        order.Status = req.Status;
        if (req.Status == "dispatched" && order.PaymentMethod == ShopOrdersController.CashOnDelivery)
            order.DeliveryCode ??= DeliveryCodes.Generate();
        if (req.Status == "cancelled") order.RejectionReason = req.Reason;
        if (req.Status == "delivered")
        {
            order.DeliveredAt ??= DateTime.UtcNow;
            // Cash on delivery: SpazaSure collected the cash at delivery
            if (order.PaymentMethod == ShopOrdersController.CashOnDelivery) order.PaymentStatus = "paid";
        }
        await ShopWallet.SyncOrderAsync(db, order);
        // A cancelled order gives its stock back
        await OrderStock.SyncAsync(db, order, heldStock);
        await db.SaveChangesAsync();

        // Notify the shop owner about the status change
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.Id == order.ShopId);
        if (shop != null)
        {
            var statusMessages = new Dictionary<string, string>
            {
                ["processing"] = $"Your order {order.OrderNumber} is being prepared!",
                ["ready"] = order.DeliveryType == "pickup"
                    ? $"Your order {order.OrderNumber} is packed and ready for you to collect from the supplier."
                    : $"Your order {order.OrderNumber} is packed. SpazaSure will collect it and deliver it to you.",
                ["dispatched"] = $"Your order {order.OrderNumber} has been dispatched and is on its way!",
                ["delivered"] = $"Your order {order.OrderNumber} has been delivered.",
                ["cancelled"] = $"Your order {order.OrderNumber} was cancelled. Reason: {req.Reason ?? "N/A"}"
                    + (order.PaymentMethod == ShopWallet.PaymentMethod ? $" R{order.TotalAmount:F2} is back in your wallet." : ""),
            };
            var msg = statusMessages.GetValueOrDefault(req.Status, $"Your order {order.OrderNumber} status changed to {req.Status}.");
            events.PublishNotification(
                supplierId: shop.UserId.ToString(),
                type: req.Status == "dispatched" ? "delivery" : "order",
                title: req.Status switch
                {
                    "dispatched" => "Order Dispatched",
                    "ready" => "Order packed",
                    _ => $"Order {req.Status.ToUpperInvariant()}",
                },
                message: msg,
                priority: req.Status == "cancelled" ? "high" : "normal",
                referenceId: order.Id.ToString(),
                routingKey: $"order.{req.Status}"
            );
        }

        // SpazaSure's delivery team collects orders that are ready
        if (req.Status == "ready" && order.DeliveryType != "pickup")
        {
            var supplierName = await db.Suppliers.Where(s => s.Id == order.SupplierId).Select(s => s.CompanyName).FirstAsync();
            await AdminAlerts.NotifyAdminsAsync(db, events, "delivery", "Order ready for collection",
                $"{order.OrderNumber} is packed and ready to collect from {supplierName}.", order.Id.ToString());
        }

        return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status }));
    }
}

public record UpdateStatusRequest([Required] string Status, string? Reason);
