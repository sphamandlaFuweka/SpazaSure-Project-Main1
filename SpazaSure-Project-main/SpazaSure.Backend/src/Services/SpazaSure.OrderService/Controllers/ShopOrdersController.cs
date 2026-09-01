using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.OrderService.Controllers;

[ApiController]
[Route("api/shop/orders")]
[Authorize(Roles = "spaza_owner")]
public class ShopOrdersController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Place a new order from the spaza shop mobile app
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest req)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        if (req.Items is null || req.Items.Count == 0)
            return BadRequest(ApiResponse.Fail("Order must have at least one item."));

        // Load products and validate
        var productIds = req.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products
            .Include(p => p.Supplier)
            .Where(p => productIds.Contains(p.Id) && p.IsAvailable && p.IsApproved)
            .ToListAsync();

        if (products.Count != req.Items.Count)
            return BadRequest(ApiResponse.Fail("One or more products are unavailable."));

        // Group items by supplier (one order per supplier)
        var grouped = req.Items.GroupBy(i => products.First(p => p.Id == i.ProductId).SupplierId);
        var createdOrders = new List<object>();

        foreach (var group in grouped)
        {
            var supplier = products.First(p => p.SupplierId == group.Key).Supplier;
            var orderItems = new List<OrderItem>();
            decimal subtotal = 0;

            foreach (var item in group)
            {
                var product = products.First(p => p.Id == item.ProductId);
                if (item.Quantity < product.MinOrderQty)
                    return BadRequest(ApiResponse.Fail($"Minimum order for '{product.Name}' is {product.MinOrderQty}."));

                var unitPrice = item.UnitPrice > 0 ? item.UnitPrice : product.Price;
                var lineTotal = unitPrice * item.Quantity;
                subtotal += lineTotal;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    OriginalUnitPrice = unitPrice,
                    UnitPrice = unitPrice,
                    DiscountPct = 0m,
                    LineTotal = lineTotal
                });
            }

            // Calculate fees
            var deliveryFee = req.DeliveryType == "express" ? 200m : 150m;
            var platformCommission = Math.Round(subtotal * (supplier.CommissionRate / 100m), 2);
            var totalAmount = subtotal + deliveryFee + platformCommission;

            // Generate order number
            var orderCount = await db.Orders.CountAsync() + 1;
            var orderNumber = $"SPZ-{DateTime.UtcNow:yyyy}-{orderCount:D4}";

            var order = new Order
            {
                OrderNumber = orderNumber,
                ShopId = shop.Id,
                SupplierId = group.Key,
                Status = "pending",
                DeliveryType = req.DeliveryType ?? "standard",
                DeliveryAddress = req.DeliveryAddress ?? shop.Address,
                Subtotal = subtotal,
                DeliveryFee = deliveryFee,
                PlatformCommission = platformCommission,
                TotalAmount = totalAmount,
                PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "invoice" : req.PaymentMethod,
                PaymentStatus = "pending",
                Items = orderItems
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            // Notify the supplier about the new order
            events.PublishNotification(
                supplierId: supplier.UserId.ToString(),
                type: "order",
                title: "New Order Received",
                message: $"{shop.ShopName} placed order {orderNumber} for R{totalAmount:F2}",
                priority: totalAmount > 1000 ? "high" : "normal",
                referenceId: order.Id.ToString(),
                routingKey: "order.placed"
            );

            createdOrders.Add(new
            {
                order.Id,
                order.OrderNumber,
                order.Status,
                order.TotalAmount,
                supplierName = supplier.CompanyName
            });
        }

        // Return the first order number (for single-supplier orders this is the only one)
        var firstOrder = createdOrders.First();
        return Ok(ApiResponse<object>.Ok(new
        {
            orders = createdOrders,
            orderNumber = (firstOrder as dynamic).OrderNumber
        }));
    }

    /// <summary>
    /// Get all orders for the logged-in shop owner
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var query = db.Orders
            .Include(o => o.Supplier)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.ShopId == shop.Id);

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
                    i.UnitPrice,
                    Price = i.UnitPrice,
                    i.LineTotal
                }),
                Items = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = i.Product.Name,
                    ProductImage = i.Product.Images,
                    i.Quantity,
                    i.UnitPrice,
                    Price = i.UnitPrice,
                    i.LineTotal
                })
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, items }));
    }

    /// <summary>
    /// Get a single order by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var order = await db.Orders
            .Where(o => o.Id == id && o.ShopId == shop.Id)
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
                    i.UnitPrice,
                    Price = i.UnitPrice,
                    i.LineTotal
                }),
                Items = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = i.Product.Name,
                    ProductImage = i.Product.Images,
                    i.Quantity,
                    i.UnitPrice,
                    Price = i.UnitPrice,
                    i.LineTotal
                })
            })
            .FirstOrDefaultAsync();

        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        return Ok(ApiResponse<object>.Ok(order));
    }

    /// <summary>
    /// Confirm delivery of an order (marks as "delivered")
    /// </summary>
    [HttpPatch("{id:guid}/confirm-delivery")]
    public async Task<IActionResult> ConfirmDelivery(Guid id)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.ShopId == shop.Id);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        if (order.Status != "dispatched")
            return BadRequest(ApiResponse.Fail("Only dispatched orders can be confirmed as delivered."));

        order.Status = "delivered";
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status }));
    }
}

// ── Request DTOs ────────────────────────────────────────────────────────────

public record PlaceOrderRequest(
    string? DeliveryType,
    string? PaymentMethod,
    string? DeliveryAddress,
    List<PlaceOrderItemRequest> Items
);

public record PlaceOrderItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice
);
