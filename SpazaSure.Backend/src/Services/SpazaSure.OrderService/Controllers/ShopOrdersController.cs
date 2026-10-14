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
    OrderPaystackPaymentService paystackPayments,
    EventPublisher events) : ControllerBase
{
    public const string CashOnDelivery = "cash_on_delivery";
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

        // Payment method: Paystack (pay now), the shop's SpazaSure wallet (pay now from its
        // prepaid balance) or cash on delivery (the shop pays SpazaSure on arrival)
        var paymentMethod = req.PaymentMethod?.Trim().ToLowerInvariant() switch
        {
            null or "" or "paystack" => "paystack",
            ShopWallet.PaymentMethod => ShopWallet.PaymentMethod,
            CashOnDelivery or "cod" or "cash" => CashOnDelivery,
            _ => null
        };
        if (paymentMethod is null)
            return BadRequest(ApiResponse.Fail("Choose Paystack, your SpazaSure wallet or cash on delivery."));

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
        var rules = await PlatformSettingsStore.LoadAsync(db);
        var isCashOnDelivery = paymentMethod == CashOnDelivery;
        var isWallet = paymentMethod == ShopWallet.PaymentMethod;
        if (isCashOnDelivery && !rules.CashOnDeliveryEnabled)
            return BadRequest(ApiResponse.Fail("Cash on delivery is not available right now. Please pay with Paystack."));
        var createdOrders = new List<Order>();
        var checkoutId = Guid.NewGuid();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Hold the shop's wallet until this checkout commits, so the balance can't be spent twice
        if (isWallet) await ShopWallet.LockAsync(db, shop.Id);

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

            // Fees come from admin platform settings (see PlatformRules for defaults).
            // The shop pays the supplier's tier commission plus any shop service fee on top
            // of the subtotal; both are SpazaSure revenue, so they're stored together.
            var deliveryFee = req.DeliveryType?.ToLowerInvariant() switch
            {
                "express" => rules.ExpressDeliveryFee,
                "pickup" => 0m,
                _ => rules.StandardDeliveryFee
            };
            var platformCommission = Math.Round(
                subtotal * ((supplier.CommissionRate + rules.ShopServiceFeePercent) / 100m), 2);
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
                PaymentMethod = paymentMethod,
                // Wallet orders are paid immediately; cash orders are marked paid when
                // delivered (SpazaSure collects the cash); Paystack orders by the webhook
                PaymentStatus = isWallet ? "paid" : "pending",
                PaymentCheckoutId = isCashOnDelivery || isWallet ? null : checkoutId,
                Supplier = supplier,
                Items = orderItems
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync();
            createdOrders.Add(order);
        }

        if (isCashOnDelivery || isWallet)
        {
            var checkoutTotal = createdOrders.Sum(o => o.TotalAmount);
            if (isCashOnDelivery && rules.CashOnDeliveryMaxOrderTotal > 0 && checkoutTotal > rules.CashOnDeliveryMaxOrderTotal)
                return BadRequest(ApiResponse.Fail(
                    $"Cash on delivery is available for orders up to R{rules.CashOnDeliveryMaxOrderTotal:N2}. Your total is R{checkoutTotal:N2}, so please pay with Paystack or split the order."));
            if (isWallet)
            {
                // The new orders are already saved in this transaction, so they're included in the balance
                var balanceAfter = await ShopWallet.BalanceAsync(db, shop.Id);
                if (balanceAfter < 0)
                {
                    var available = balanceAfter + checkoutTotal;
                    return BadRequest(ApiResponse.Fail(
                        $"Your wallet balance is R{Math.Max(available, 0):N2} but this order is R{checkoutTotal:N2}. Top up your wallet or choose another payment method."));
                }
            }

            await transaction.CommitAsync();

            // No online payment step, so suppliers hear about the order straight away.
            // SpazaSure holds the payment (wallet) or collects the cash on delivery, and pays the supplier as usual.
            foreach (var order in createdOrders)
            {
                events.PublishNotification(
                    supplierId: order.Supplier.UserId.ToString(),
                    type: "order",
                    title: "New Order Received",
                    message: isWallet
                        ? $"{shop.ShopName} placed order {order.OrderNumber} for R{order.TotalAmount:F2}"
                        : $"{shop.ShopName} placed order {order.OrderNumber} for R{order.TotalAmount:F2}. The shop pays SpazaSure on delivery; you are paid out as usual.",
                    priority: order.TotalAmount > 1000 ? "high" : "normal",
                    referenceId: order.Id.ToString(),
                    routingKey: "order.placed");
            }

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
                orderNumber = createdOrders[0].OrderNumber,
                paymentMethod,
                checkoutUrl = (string?)null,
                totalAmount = checkoutTotal
            }));
        }

        var paymentReference = $"SPZ-{Guid.NewGuid():N}";
        var checkout = await paystackPayments.CreateCheckoutAsync(
            createdOrders,
            shop.Email ?? string.Empty,
            paymentReference,
            HttpContext.RequestAborted);
        foreach (var order in createdOrders)
            order.PaymentProviderReference = checkout.Reference;
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
            paymentMethod,
            checkoutUrl = checkout.AuthorizationUrl
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
    /// Confirm delivery of an order (marks as "delivered"). Cash on delivery orders
    /// also need the delivery code the supplier hands over with the goods.
    /// </summary>
    [HttpPatch("{id:guid}/confirm-delivery")]
    public async Task<IActionResult> ConfirmDelivery(Guid id, [FromBody] ConfirmDeliveryRequest? req = null)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.ShopId == shop.Id);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        if (order.Status != "dispatched")
            return BadRequest(ApiResponse.Fail("Only dispatched orders can be confirmed as delivered."));

        if (order.PaymentMethod == CashOnDelivery)
        {
            var code = req?.Code?.Trim();
            if (string.IsNullOrEmpty(code))
                return BadRequest(ApiResponse.Fail("Enter the delivery code the driver gave you to confirm this cash order."));
            if (!DeliveryCodes.Matches(order.DeliveryCode, code))
                return BadRequest(ApiResponse.Fail("That delivery code is not correct. Check it with the driver and try again."));
        }

        order.Status = "delivered";
        order.DeliveredAt ??= DateTime.UtcNow;
        if (order.PaymentMethod == CashOnDelivery) order.PaymentStatus = "paid";
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status }));
    }
}

public record ConfirmDeliveryRequest(string? Code);

/// <summary>Codes that prove a cash on delivery order reached the shop.</summary>
public static class DeliveryCodes
{
    public static string Generate() =>
        System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static bool Matches(string? expected, string? supplied) =>
        !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(supplied) &&
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(supplied));
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
