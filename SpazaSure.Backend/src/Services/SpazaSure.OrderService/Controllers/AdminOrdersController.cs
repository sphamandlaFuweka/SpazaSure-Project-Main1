using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
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
            disputedCount = allOrders.Count(o => o.Status == "disputed"),
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

    [HttpGet("{id:guid}/dispute")]
    public async Task<IActionResult> GetDispute(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Orders.AnyAsync(o => o.Id == id, cancellationToken))
            return NotFound(ApiResponse.Fail("Order not found."));

        var dispute = await LoadDisputeAsync(id, cancellationToken);
        return dispute is null
            ? NotFound(ApiResponse.Fail("No dispute was found for this order."))
            : Ok(ApiResponse<object>.Ok(dispute));
    }

    [HttpPost("{id:guid}/resolve-dispute")]
    public async Task<IActionResult> ResolveDispute(
        Guid id,
        [FromBody] ResolveDisputeRequest req,
        [FromServices] SpazaSure.OrderService.Services.OrderPaystackPaymentService? paystack = null,
        CancellationToken cancellationToken = default)
    {
        if (req.Note?.Length > 1000)
            return BadRequest(ApiResponse.Fail("Resolution notes must be 1,000 characters or fewer."));

        var order = await db.Orders
            .Include(o => o.Shop)
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        var dispute = await LoadDisputeAsync(id, cancellationToken);
        if (dispute is null) return NotFound(ApiResponse.Fail("No dispute was found for this order."));

        if (req.Resolution == "confirm_refund")
        {
            if (dispute.Status is not ("refund_pending_manual" or "refund_pending" or "refund_requesting"))
                return BadRequest(ApiResponse.Fail("There is no pending refund to confirm."));
            if (string.IsNullOrWhiteSpace(req.Note))
                return BadRequest(ApiResponse.Fail("Add a refund receipt or provider confirmation reference."));

            var heldStock = OrderStock.Holds(order);
            order.Status = "cancelled";
            order.PaymentStatus = "refunded";
            await ShopWallet.SyncOrderAsync(db, order, cancellationToken);
            await OrderStock.SyncAsync(db, order, heldStock, cancellationToken);
            var completedRefundStatus = dispute.Status == "refund_pending_manual" ? "confirmed_manual" : "success";
            await SaveDisputeResolutionAsync(order, dispute.Id, "resolved", "refund_shop",
                req.Note, dispute.RefundReference,
                completedRefundStatus, DateTime.UtcNow, cancellationToken);
            await NotifyDisputeResolvedAsync(order, "The shop refund has been confirmed by SpazaSure.");
            return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status, disputeStatus = "resolved" }));
        }

        if (dispute.Status != "open" || order.Status != "disputed")
            return Conflict(ApiResponse.Fail("This dispute is already being handled or has been resolved."));
        if (order.PaymentStatus != "paid")
            return BadRequest(ApiResponse.Fail("The order must be paid before its dispute can be resolved."));
        if (order.PayoutInitiatedAt is not null)
            return Conflict(ApiResponse.Fail("A supplier payout is already in progress. Resolve it in the Payouts page first."));
        if (req.Resolution == "refund_shop" && order.PaidOutAt is not null &&
            !(order.PaymentMethod == "paystack" && !string.IsNullOrWhiteSpace(order.PaystackSplitCode)))
            return Conflict(ApiResponse.Fail("The supplier payout has already completed. Reconcile it in the Payouts page before refunding the shop."));

        if (req.Resolution == "release_supplier")
        {
            order.Status = "delivered";
            order.DeliveredAt ??= DateTime.UtcNow;
            await SaveDisputeResolutionAsync(order, dispute.Id, "resolved", req.Resolution,
                req.Note, dispute.RefundReference, dispute.RefundStatus, DateTime.UtcNow, cancellationToken);
            await NotifyDisputeResolvedAsync(order, "SpazaSure reviewed the delivery report and made the supplier payout eligible to resume.");
            return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status, disputeStatus = "resolved" }));
        }

        if (req.Resolution != "refund_shop")
            return BadRequest(ApiResponse.Fail("Choose whether to release the supplier payout or refund the shop."));

        if (order.PaymentMethod == ShopWallet.PaymentMethod)
        {
            var heldStock = OrderStock.Holds(order);
            order.Status = "cancelled";
            order.PaymentStatus = "refunded";
            await ShopWallet.SyncOrderAsync(db, order, cancellationToken);
            await OrderStock.SyncAsync(db, order, heldStock, cancellationToken);
            await SaveDisputeResolutionAsync(order, dispute.Id, "resolved", "refund_shop",
                req.Note, dispute.RefundReference, "success", DateTime.UtcNow, cancellationToken);
            await NotifyDisputeResolvedAsync(order, "The order was cancelled and the shop's wallet payment was refunded.");
            return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status, disputeStatus = "resolved" }));
        }

        if (order.PaymentMethod == ShopOrdersController.CashOnDelivery)
        {
            await UpdateDisputeAsync(dispute.Id, "refund_pending_manual", "refund_shop",
                req.Note ?? "Manual refund required.", null, "manual", null, cancellationToken);
            await NotifyDisputeResolvedAsync(order, "SpazaSure requires a manual cash refund before this delivery dispute can be closed.");
            return Ok(ApiResponse<object>.Ok(new
            {
                order.Id,
                order.Status,
                disputeStatus = "refund_pending_manual",
                message = "Send the refund to the shop outside SpazaSure, then confirm it here."
            }));
        }

        if (order.PaymentMethod != "paystack")
            return BadRequest(ApiResponse.Fail("This payment method does not support an automatic refund."));
        if (paystack is null || !paystack.IsConfigured || string.IsNullOrWhiteSpace(order.PaymentProviderReference))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail("Paystack refunds are unavailable for this order."));

        await UpdateDisputeAsync(dispute.Id, "refund_requesting", "refund_shop",
            req.Note ?? "Refund requested after delivery dispute.", null, "requesting", null, cancellationToken);

        SpazaSure.OrderService.Services.OrderPaystackRefund refund;
        try
        {
            refund = await paystack.RefundAsync(order.PaymentProviderReference, order.TotalAmount,
                $"Refund for disputed order {order.OrderNumber}", cancellationToken);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse.Fail("Paystack's response could not be confirmed. Check the refund in Paystack before taking further action."));
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse.Fail("Paystack's response could not be confirmed. Check the refund in Paystack before taking further action."));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse.Fail($"Paystack could not confirm the refund request: {ex.Message}"));
        }

        if (string.Equals(refund.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(refund.Status, "reversed", StringComparison.OrdinalIgnoreCase))
        {
            await UpdateDisputeAsync(dispute.Id, "open", "refund_shop",
                $"Paystack refund {refund.Status}. Review the payment before retrying.",
                refund.Id, refund.Status, null, cancellationToken);
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse.Fail($"Paystack marked the refund as {refund.Status}. Review the transaction before trying again."));
        }

        var isComplete = string.Equals(refund.Status, "success", StringComparison.OrdinalIgnoreCase);
        var wasHoldingStock = OrderStock.Holds(order);
        order.Status = isComplete ? "cancelled" : "disputed";
        if (isComplete) order.PaymentStatus = "refunded";
        if (isComplete)
        {
            await ShopWallet.SyncOrderAsync(db, order, cancellationToken);
            await OrderStock.SyncAsync(db, order, wasHoldingStock, cancellationToken);
        }
        await SaveDisputeResolutionAsync(order, dispute.Id, isComplete ? "resolved" : "refund_pending",
            "refund_shop", req.Note, refund.Id, refund.Status, isComplete ? DateTime.UtcNow : null, cancellationToken);
        await NotifyDisputeResolvedAsync(order, isComplete
            ? "The order was cancelled and the shop's payment was refunded."
            : "The order was cancelled and the shop's Paystack refund is processing.");

        return Ok(ApiResponse<object>.Ok(new
        {
            order.Id,
            order.Status,
            disputeStatus = isComplete ? "resolved" : "refund_pending",
            refundStatus = refund.Status,
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
        if (order.Status == "disputed")
            return BadRequest(ApiResponse.Fail("Resolve the delivery dispute before changing this order's status."));

        // Direct Paystack splits happen before delivery, so they must not block normal order
        // progress. Once delivered, the state is locked except for cancellation/refund handling.
        var wasDirectSplitPaid = order.PaidOutAt is not null && !string.IsNullOrWhiteSpace(order.PaystackSplitCode);
        if (order.PaidOutAt is not null && req.Status != order.Status && !wasDirectSplitPaid)
            return BadRequest(ApiResponse.Fail("This order has already been paid out to the supplier, so its status can't be changed."));
        if (wasDirectSplitPaid && (order.Status is "delivered" or "cancelled") &&
            req.Status != order.Status && req.Status != "cancelled")
            return BadRequest(ApiResponse.Fail("A paid or cancelled Paystack split order can't be reopened or changed."));

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

    private Task<AdminDisputeResult?> LoadDisputeAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Database.SqlQueryRaw<AdminDisputeResult>(@"
            SELECT dd.id, dd.order_id, dd.issue_type, dd.description, dd.status,
                   dd.created_at, dd.resolution, dd.resolution_note, dd.resolved_at,
                   dd.refund_reference, dd.refund_status, o.order_number, s.shop_name
            FROM delivery_disputes dd
            JOIN orders o ON o.id = dd.order_id
            JOIN spaza_shops s ON s.id = dd.shop_id
            WHERE dd.order_id = {0}
            ORDER BY dd.created_at DESC
            LIMIT 1", orderId).FirstOrDefaultAsync(cancellationToken);

    private async Task UpdateDisputeAsync(
        Guid disputeId, string status, string resolution, string? note,
        string? refundReference, string? refundStatus, DateTime? resolvedAt,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE delivery_disputes
            SET status = {status}, resolution = {resolution}, resolution_note = {note},
                refund_reference = {refundReference}, refund_status = {refundStatus}, resolved_at = {resolvedAt}
            WHERE id = {disputeId}", cancellationToken);
    }

    private async Task SaveDisputeResolutionAsync(
        Order order, Guid disputeId, string status, string resolution, string? note,
        string? refundReference, string? refundStatus, DateTime? resolvedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await db.SaveChangesAsync(cancellationToken);
        await UpdateDisputeAsync(disputeId, status, resolution, note, refundReference, refundStatus, resolvedAt,
            cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private async Task NotifyDisputeResolvedAsync(Order order, string message)
    {
        var parties = await db.Orders.Where(o => o.Id == order.Id)
            .Select(o => new { ShopUserId = o.Shop.UserId, SupplierUserId = o.Supplier.UserId })
            .FirstAsync();
        events.PublishNotification(parties.ShopUserId.ToString(), "order", "Delivery dispute update",
            $"{message} Order {order.OrderNumber}.", "high", order.Id.ToString(), "order.dispute_resolved");
        events.PublishNotification(parties.SupplierUserId.ToString(), "order", "Delivery dispute update",
            $"{message} Order {order.OrderNumber}.", "high", order.Id.ToString(), "order.dispute_resolved");
    }
}

public sealed record ResolveDisputeRequest(string Resolution, string? Note);

public sealed class AdminDisputeResult
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string IssueType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? Resolution { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? RefundReference { get; set; }
    public string? RefundStatus { get; set; }
    public string OrderNumber { get; set; } = "";
    public string ShopName { get; set; } = "";
}
