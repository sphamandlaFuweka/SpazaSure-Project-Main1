using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.OrderService.Controllers;

/// <summary>
/// Supplier payout rules. SpazaSure always collects the shop's money and pays the supplier:
///  - Paystack orders: the shop pays online before the order is processed.
///  - Cash on delivery: SpazaSure collects the cash from the shop on delivery (the order
///    is marked paid at that point). The supplier never handles cash.
///  - SpazaSure delivers, so the supplier is owed the order's Subtotal (the value of the goods).
///    SpazaSure keeps the delivery fee, and the commission + service fee the shop paid on top.
///    "Pending" until PayoutDelayDays after delivery, then "available". Automatic payouts
///    (Paystack Transfers) move it to "processing" and then "paid_out"; a failed transfer is
///    "failed" until the supplier fixes their bank details or an admin retries.
/// </summary>
public static class PayoutRules
{
    public static IQueryable<Order> Settleable(IQueryable<Order> orders) =>
        orders.Where(o => o.Status != "cancelled" && o.PaymentStatus == "paid"
            && (o.PaymentMethod == "paystack" || o.PaymentMethod == ShopWallet.PaymentMethod
                || o.PaymentMethod == ShopOrdersController.CashOnDelivery));

    public static decimal AmountOwed(Order o) => o.Subtotal;

    public static string Stage(Order o, DateTime availableCutoff) =>
        o.PaidOutAt is not null ? "paid_out"
        : SupplierPayoutResults.IsFailed(o.PayoutReference) ? "failed"
        : o.PayoutInitiatedAt is not null ? "processing"
        : o.Status != "delivered" || o.DeliveredAt is null ? "awaiting_delivery"
        : o.DeliveredAt <= availableCutoff ? "available"
        : "pending";

    /// <summary>
    /// Like <see cref="Stage(Order, DateTime)"/>, but a group buy's order that is ready waits
    /// ("awaiting_group") while <paramref name="heldGroupBuys"/> says the rest of its group buy
    /// isn't, so the supplier is paid for the whole group buy at once rather than shop by shop.
    /// </summary>
    public static string Stage(Order o, DateTime availableCutoff, ISet<Guid> heldGroupBuys)
    {
        var stage = Stage(o, availableCutoff);
        return stage == "available" && o.GroupBuyId is { } groupBuyId && heldGroupBuys.Contains(groupBuyId)
            ? "awaiting_group"
            : stage;
    }

    /// <summary>
    /// Group buys whose money still waits: the deal is still running (more products may reach
    /// their target and add orders), or one of its unpaid orders isn't delivered and past the
    /// payout delay yet.
    /// </summary>
    public static async Task<HashSet<Guid>> GroupBuysStillOpenAsync(
        SpazaSureDbContext db, DateTime availableCutoff, CancellationToken ct = default)
    {
        var running = await db.GroupBuys.AsNoTracking()
            .Where(g => g.Status == "active")
            .Select(g => g.Id)
            .ToListAsync(ct);
        var unfinished = await db.Orders.AsNoTracking()
            .Where(o => o.GroupBuyId != null && o.Status != "cancelled" && o.PaidOutAt == null && o.PayoutInitiatedAt == null &&
                        (o.Status != "delivered" || o.DeliveredAt == null || o.DeliveredAt > availableCutoff))
            .Select(o => o.GroupBuyId!.Value)
            .Distinct()
            .ToListAsync(ct);
        return running.Concat(unfinished).ToHashSet();
    }
}

[ApiController]
[Route("api/supplier/payouts")]
[Authorize(Roles = "supplier")]
public class SupplierPayoutsController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Summary()
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var rules = await PlatformSettingsStore.LoadAsync(db);
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-rules.PayoutDelayDays);
        var heldGroupBuys = await PayoutRules.GroupBuysStillOpenAsync(db, cutoff);

        var orders = await PayoutRules.Settleable(db.Orders.AsNoTracking())
            .Where(o => o.SupplierId == supplier.Id)
            .OrderByDescending(o => o.DeliveredAt ?? o.CreatedAt)
            .ToListAsync();

        decimal Sum(string stage) => orders.Where(o => PayoutRules.Stage(o, cutoff, heldGroupBuys) == stage).Sum(PayoutRules.AmountOwed);
        var nextPending = orders
            .Where(o => PayoutRules.Stage(o, cutoff, heldGroupBuys) == "pending")
            .Select(o => o.DeliveredAt!.Value.AddDays(rules.PayoutDelayDays))
            .OrderBy(d => d)
            .Cast<DateTime?>()
            .FirstOrDefault();

        return Ok(ApiResponse<object>.Ok(new
        {
            payoutDelayDays = rules.PayoutDelayDays,
            awaitingDelivery = Sum("awaiting_delivery"),
            // Group buy orders that are ready but wait for the rest of their group buy
            awaitingGroup = Sum("awaiting_group"),
            pending = Sum("pending"),
            available = Sum("available"),
            processing = Sum("processing"),
            failed = Sum("failed"),
            paidOut = Sum("paid_out"),
            nextAvailableAt = nextPending,
            hasBankDetails = AutomaticPayouts.HasBankDetails(supplier),
            recent = orders.Take(20).Select(o => new
            {
                o.Id,
                o.OrderNumber,
                amount = PayoutRules.AmountOwed(o),
                stage = PayoutRules.Stage(o, cutoff, heldGroupBuys),
                o.DeliveredAt,
                availableAt = o.DeliveredAt?.AddDays(rules.PayoutDelayDays),
                o.PaidOutAt,
                PayoutReference = SupplierPayoutResults.IsFailed(o.PayoutReference) ? null : o.PayoutReference,
                failureReason = SupplierPayoutResults.FailureReason(o.PayoutReference),
            }),
        }));
    }

    /// <summary>South African banks Paystack can pay into, for choosing the payout bank.</summary>
    [HttpGet("banks")]
    public async Task<IActionResult> Banks([FromServices] PaystackTransferService transfers, CancellationToken ct)
    {
        try
        {
            var banks = await transfers.ListBanksAsync(ct);
            return Ok(ApiResponse<object>.Ok(banks.Select(b => new { b.Name, b.Code })));
        }
        catch (PaystackTransferException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Fail("The bank list isn't available right now."));
        }
    }
}

[ApiController]
[Route("api/admin/payouts")]
[Authorize(Roles = "admin")]
public class AdminPayoutsController(SpazaSureDbContext db, EventPublisher events, PaystackTransferService transfers) : ControllerBase
{
    /// <summary>Per supplier: what is available to pay now and what is still pending.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var rules = await PlatformSettingsStore.LoadAsync(db);
        var cutoff = DateTime.UtcNow.AddDays(-rules.PayoutDelayDays);
        var heldGroupBuys = await PayoutRules.GroupBuysStillOpenAsync(db, cutoff);

        var orders = await PayoutRules.Settleable(db.Orders.AsNoTracking())
            .Where(o => o.PaidOutAt == null && o.Status == "delivered" && o.DeliveredAt != null)
            .Include(o => o.Supplier)
            .ToListAsync();

        var suppliers = orders
            .GroupBy(o => o.SupplierId)
            .Select(g =>
            {
                var supplier = g.First().Supplier;
                var available = g.Where(o => PayoutRules.Stage(o, cutoff, heldGroupBuys) == "available").ToList();
                var pending = g.Where(o => PayoutRules.Stage(o, cutoff, heldGroupBuys) == "pending").ToList();
                var processing = g.Where(o => PayoutRules.Stage(o, cutoff, heldGroupBuys) == "processing").ToList();
                var failed = g.Where(o => PayoutRules.Stage(o, cutoff, heldGroupBuys) == "failed").ToList();
                return new
                {
                    supplierId = g.Key,
                    supplier.CompanyName,
                    supplier.Email,
                    supplier.BankName,
                    supplier.BankAccount,
                    supplier.BankBranchCode,
                    hasBankDetails = AutomaticPayouts.HasBankDetails(supplier),
                    availableAmount = available.Sum(PayoutRules.AmountOwed),
                    availableOrders = available.Count,
                    pendingAmount = pending.Sum(PayoutRules.AmountOwed),
                    pendingOrders = pending.Count,
                    processingAmount = processing.Sum(PayoutRules.AmountOwed),
                    failedAmount = failed.Sum(PayoutRules.AmountOwed),
                    failureReason = failed.Select(o => SupplierPayoutResults.FailureReason(o.PayoutReference)).FirstOrDefault(),
                };
            })
            .Where(s => s.availableOrders > 0 || s.pendingOrders > 0 || s.processingAmount > 0 || s.failedAmount > 0)
            .OrderByDescending(s => s.availableAmount)
            .ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            payoutDelayDays = rules.PayoutDelayDays,
            automaticPayouts = transfers.AutomaticPayoutsEnabled,
            totalAvailable = suppliers.Sum(s => s.availableAmount),
            totalPending = suppliers.Sum(s => s.pendingAmount),
            suppliers,
        }));
    }

    /// <summary>Lets a supplier's failed automatic payout be tried again on the next run.</summary>
    [HttpPost("{supplierId:guid}/retry")]
    public async Task<IActionResult> Retry(Guid supplierId)
    {
        var cleared = await SupplierPayoutResults.ClearFailuresAsync(db, supplierId, HttpContext.RequestAborted);
        await db.SaveChangesAsync();
        return cleared == 0
            ? BadRequest(ApiResponse.Fail("This supplier has no failed payouts."))
            : Ok(ApiResponse.Ok("The payout will be tried again within 30 minutes."));
    }

    /// <summary>
    /// Records that SpazaSure has paid a supplier everything currently available
    /// (e.g. after an EFT). Orders that become available later are not included.
    /// </summary>
    [HttpPost("{supplierId:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaid(Guid supplierId, MarkPayoutPaidRequest req)
    {
        var rules = await PlatformSettingsStore.LoadAsync(db);
        var cutoff = DateTime.UtcNow.AddDays(-rules.PayoutDelayDays);
        var heldGroupBuys = await PayoutRules.GroupBuysStillOpenAsync(db, cutoff);

        var orders = (await PayoutRules.Settleable(db.Orders)
            .Where(o => o.SupplierId == supplierId && o.PaidOutAt == null
                && o.Status == "delivered" && o.DeliveredAt != null)
            .ToListAsync())
            // A failed automatic payout can also be settled by EFT; one in progress can't (it may still go through)
            .Where(o => PayoutRules.Stage(o, cutoff, heldGroupBuys) is "available" or "failed")
            .ToList();
        if (orders.Count == 0)
            return BadRequest(ApiResponse.Fail("This supplier has nothing available to pay out."));

        var now = DateTime.UtcNow;
        var reference = req.Reference.Trim();
        foreach (var o in orders)
        {
            o.PaidOutAt = now;
            o.PayoutReference = reference;
            o.UpdatedAt = now;
        }
        await db.SaveChangesAsync();

        var total = orders.Sum(PayoutRules.AmountOwed);
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is not null)
        {
            events.PublishNotification(
                supplierId: supplier.UserId.ToString(),
                type: "order",
                title: "Payout sent",
                message: $"SpazaSure paid you R{total:N2} for {orders.Count} delivered order{(orders.Count == 1 ? "" : "s")}. Reference: {reference}.",
                referenceId: reference,
                routingKey: "payout.paid");
        }

        return Ok(ApiResponse<object>.Ok(new { total, orders = orders.Count, reference }, "Payout recorded."));
    }
}

public record MarkPayoutPaidRequest([Required, StringLength(100, MinimumLength = 3)] string Reference);
