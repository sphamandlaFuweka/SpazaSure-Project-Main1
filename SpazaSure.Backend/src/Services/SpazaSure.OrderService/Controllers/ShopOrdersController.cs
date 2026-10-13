using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using SpazaSure.OrderService.Services;
using System.Security.Claims;

namespace SpazaSure.OrderService.Controllers;

[ApiController]
[Route("api/shop/orders")]
[Authorize(Roles = "spaza_owner")]
public class ShopOrdersController(
    SpazaSureDbContext db,
    OrderStripePaymentService stripePayments) : ControllerBase
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
        var createdOrders = new List<Order>();
        var checkoutId = Guid.NewGuid();
        await using var transaction = await db.Database.BeginTransactionAsync();

        foreach (var group in grouped)
        {
            var supplier = products.First(p => p.SupplierId == group.Key).Supplier;
            var orderItems = new List<OrderItem>();
            decimal subtotal = 0;

            foreach (var item in group)
            {
                var product = products.First(p => p.Id == item.ProductId);
                if (item.Quantity < 1)
                    return BadRequest(ApiResponse.Fail($"Choose at least one unit of '{product.Name}'."));
                if (item.Quantity > product.StockQty)
                    return BadRequest(ApiResponse.Fail($"Only {product.StockQty} units of '{product.Name}' are available."));

                var unitPrice = product.Price;
                var lineTotal = unitPrice * item.Quantity;
                subtotal += lineTotal;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    OriginalUnitPrice = unitPrice,
                    UnitPrice = unitPrice,
                    DiscountPct = 0m,
                    LineTotal = lineTotal,
                    Product = product
                });
            }

            // Calculate fees
            var deliveryFee = req.DeliveryType?.ToLowerInvariant() switch
            {
                "express" => 250m,
                "pickup" => 0m,
                _ => 150m
            };
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
                PaymentMethod = "stripe",
                PaymentStatus = "pending",
                StripeCheckoutId = checkoutId,
                Supplier = supplier,
                Items = orderItems
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync();
            createdOrders.Add(order);
        }

        var checkout = await stripePayments.CreateCheckoutSessionAsync(
            createdOrders,
            checkoutId,
            shop.Email ?? string.Empty,
            HttpContext.RequestAborted);
        foreach (var order in createdOrders)
            order.StripeCheckoutSessionId = checkout.Id;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var firstOrder = createdOrders[0];
        return Ok(ApiResponse<object>.Ok(new
        {
            orders = createdOrders.Select(order => new
            {
                order.Id,
                order.OrderNumber,
                order.Status,
                order.TotalAmount,
                supplierName = order.Supplier.CompanyName
            }),
            orderNumber = firstOrder.OrderNumber,
            checkoutUrl = checkout.Url
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
