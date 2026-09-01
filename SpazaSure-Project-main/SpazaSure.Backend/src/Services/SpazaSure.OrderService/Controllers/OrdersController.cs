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
                o.DeliveryFee,
                o.PlatformCommission,
                o.TotalAmount,
                Total = o.TotalAmount,
                o.Status,
                o.DeliveryType,
                Type = o.DeliveryType,
                o.DeliveryAddress,
                o.DeliveryLatitude,
                o.DeliveryLongitude,
                o.DeliveryDistanceKm,
                o.PaymentMethod,
                o.PaymentStatus,
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
                o.DeliveryFee,
                o.PlatformCommission,
                o.TotalAmount,
                Total = o.TotalAmount,
                o.Status,
                o.DeliveryType,
                Type = o.DeliveryType,
                o.DeliveryAddress,
                o.DeliveryLatitude,
                o.DeliveryLongitude,
                o.DeliveryDistanceKm,
                o.PaymentMethod,
                o.PaymentStatus,
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
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateStatusRequest req)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.SupplierId == supplier.Id);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        var allowed = new Dictionary<string, string[]>
        {
            ["pending"]    = ["processing", "cancelled"],
            ["confirmed"]  = ["processing", "cancelled"],
            ["processing"] = ["dispatched", "cancelled"],
            ["dispatched"] = ["delivered"],
        };

        if (!allowed.TryGetValue(order.Status, out var next) || !next.Contains(req.Status))
            return BadRequest(ApiResponse.Fail($"Cannot transition from '{order.Status}' to '{req.Status}'."));

        order.Status = req.Status;
        if (req.Status == "cancelled") order.RejectionReason = req.Reason;
        await db.SaveChangesAsync();

        // Notify the shop owner about the status change
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.Id == order.ShopId);
        if (shop != null)
        {
            var statusMessages = new Dictionary<string, string>
            {
                ["processing"] = $"Your order {order.OrderNumber} is being prepared!",
                ["dispatched"] = $"Your order {order.OrderNumber} has been dispatched and is on its way!",
                ["delivered"] = $"Your order {order.OrderNumber} has been delivered.",
                ["cancelled"] = $"Your order {order.OrderNumber} was cancelled. Reason: {req.Reason ?? "N/A"}",
            };
            var msg = statusMessages.GetValueOrDefault(req.Status, $"Your order {order.OrderNumber} status changed to {req.Status}.");
            events.PublishNotification(
                supplierId: shop.UserId.ToString(),
                type: req.Status == "dispatched" ? "delivery" : "order",
                title: req.Status == "dispatched" ? "Order Dispatched" : $"Order {req.Status.ToUpperInvariant()}",
                message: msg,
                priority: req.Status == "cancelled" ? "high" : "normal",
                referenceId: order.Id.ToString(),
                routingKey: $"order.{req.Status}"
            );
        }

        return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status }));
    }
}

public record UpdateStatusRequest([Required] string Status, string? Reason);
