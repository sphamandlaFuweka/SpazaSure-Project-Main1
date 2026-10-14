using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/supplier/subscription")]
[Authorize]
public class SubscriptionController(
    SpazaSureDbContext db,
    PaystackPaymentService paystack) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    //  GET all plans 
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(plans.Select(p => new
        {
            p.Id, p.Tier, p.Name, p.Description,
            p.MonthlyPrice, p.AnnualPrice, p.CommissionRate,
            p.MaxListings, p.MaxOrders,
            p.HasAnalytics, p.HasPrioritySupport, p.HasBulkPricing,
            p.HasApiAccess, p.HasCustomBranding,
            p.SortOrder
        })));
    }

    //  GET current supplier subscription 
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var sub = await db.SupplierSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.SupplierId == supplier.Id && s.Status == "active")
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();

        // Always return plan info even if no subscription (basic = free default)
        var plan = sub?.Plan ?? await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Tier == supplier.Tier);

        return Ok(ApiResponse<object>.Ok(new
        {
            currentTier = supplier.Tier,
            commissionRate = supplier.CommissionRate,
            subscription = sub is null ? null : new
            {
                sub.Id, sub.BillingCycle, sub.Status,
                sub.StartDate, sub.EndDate, sub.NextBillingDate,
                sub.AmountPaid, sub.PaymentMethod, sub.PaymentReference,
                sub.CancelAtPeriodEnd
            },
            plan = plan is null ? null : new
            {
                plan.Id, plan.Tier, plan.Name, plan.Description,
                plan.MonthlyPrice, plan.AnnualPrice, plan.CommissionRate,
                plan.MaxListings, plan.MaxOrders,
                plan.HasAnalytics, plan.HasPrioritySupport, plan.HasBulkPricing,
                plan.HasApiAccess, plan.HasCustomBranding
            }
        }));
    }

    //  POST subscribe to a plan 
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Tier))
            return BadRequest(ApiResponse.Fail("Tier is required."));

        var validTiers = new[] { "basic", "bronze", "silver", "gold" };
        if (!validTiers.Contains(req.Tier.ToLower()))
            return BadRequest(ApiResponse.Fail("Invalid tier. Choose basic, bronze, silver, or gold."));

        var billingCycle = (req.BillingCycle ?? "monthly").ToLower();
        if (billingCycle != "monthly" && billingCycle != "annual")
            return BadRequest(ApiResponse.Fail("Billing cycle must be 'monthly' or 'annual'."));

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var plan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Tier == req.Tier.ToLower() && p.IsActive);
        if (plan is null) return NotFound(ApiResponse.Fail("Plan not found."));

        var pending = await db.SupplierSubscriptions
            .Where(s => s.SupplierId == supplier.Id && s.Status == "pending")
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();
        if (pending is not null)
        {
            if (pending.PlanId == plan.Id && pending.BillingCycle == billingCycle)
            {
                var pendingAmount = billingCycle == "annual" ? plan.AnnualPrice : plan.MonthlyPrice;
                return Ok(ApiResponse<object>.Ok(new
                {
                    subscriptionId = pending.Id,
                    tier = supplier.Tier,
                    commissionRate = supplier.CommissionRate,
                    status = pending.Status,
                    billingCycle,
                    nextBillingDate = pending.NextBillingDate,
                    amountDue = pendingAmount,
                    paymentMethod = "paystack"
                }));
            }

            if (!string.IsNullOrWhiteSpace(pending.PaymentProviderCheckoutReference))
                return Conflict(ApiResponse.Fail(
                    "Finish or wait for your existing Paystack plan checkout before choosing another plan."));
            if (pending.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
            {
                return Conflict(ApiResponse.Fail(
                    "A plan checkout is being prepared. Please retry shortly."));
            }

            pending.Status = "cancelled";
            pending.EndDate = DateTime.UtcNow;
            pending.CancelledReason = "Unpaid checkout expired or was abandoned.";
            await db.SaveChangesAsync();
        }

        var currentProviderSubscription = await db.SupplierSubscriptions
            .Where(s => s.SupplierId == supplier.Id &&
                        s.PaymentProviderSubscriptionCode != null &&
                        (s.Status == "active" || s.Status == "past_due" ||
                         s.Status == "unpaid" || s.Status == "incomplete" ||
                         s.Status == "trialing"))
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();
        if (currentProviderSubscription is not null)
            return Conflict(ApiResponse.Fail(
                "Cancel your current auto-renewing plan before changing tiers. Cancellation takes effect immediately."));

        // Retire legacy non-recurring subscriptions before activating or paying for a new tier.
        var existing = await db.SupplierSubscriptions
            .Where(s => s.SupplierId == supplier.Id && s.Status == "active")
            .ToListAsync();
        foreach (var e in existing)
        {
            e.Status = "cancelled";
            e.EndDate = DateTime.UtcNow;
            e.CancelledReason = $"Upgraded/changed to {req.Tier}";
        }

        var amount = billingCycle == "annual" ? plan.AnnualPrice : plan.MonthlyPrice;
        var now = DateTime.UtcNow;
        var nextBilling = billingCycle == "annual" ? now.AddYears(1) : now.AddMonths(1);

        // For basic (free) tier  activate immediately, no payment needed
        var status = plan.MonthlyPrice == 0m ? "active" : "pending";
        var paymentMethod = plan.MonthlyPrice == 0m ? "free" : "paystack";

        var sub = new SupplierSubscription
        {
            SupplierId = supplier.Id,
            PlanId = plan.Id,
            BillingCycle = billingCycle,
            Status = status,
            StartDate = now,
            NextBillingDate = null,
            AmountPaid = 0m,
            PaymentMethod = paymentMethod,
            PaymentReference = null
        };

        db.SupplierSubscriptions.Add(sub);

        if (plan.MonthlyPrice == 0m)
        {
            supplier.Tier = plan.Tier;
            supplier.CommissionRate = plan.CommissionRate;
            sub.Status = "active";
        }

        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            subscriptionId = sub.Id,
            tier = supplier.Tier,
            commissionRate = supplier.CommissionRate,
            status = sub.Status,
            billingCycle,
            nextBillingDate = sub.NextBillingDate,
            amountDue = amount,
            paymentMethod
        }));
    }

    //  POST cancel subscription 
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelSubscriptionRequest req)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var sub = await db.SupplierSubscriptions
            .Where(s => s.SupplierId == supplier.Id &&
                        (s.Status == "active" || s.Status == "past_due" || s.Status == "unpaid"))
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();

        if (sub is null) return NotFound(ApiResponse.Fail("No active subscription found."));

        if (string.IsNullOrWhiteSpace(sub.PaymentProviderSubscriptionCode))
        {
            if (!string.Equals(sub.PaymentMethod, "free", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status409Conflict,
                    ApiResponse.Fail("This subscription is missing its payment-provider cancellation details. Contact support before cancelling."));
        }
        else
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sub.PaymentProviderEmailToken))
                    return StatusCode(StatusCodes.Status409Conflict,
                        ApiResponse.Fail("Paystack has not yet returned the subscription cancellation token. Please try again shortly."));
                await paystack.DisableSubscriptionAsync(
                    sub.PaymentProviderSubscriptionCode,
                    sub.PaymentProviderEmailToken);
                sub.CancelAtPeriodEnd = false;
                sub.Status = "cancelled";
                sub.EndDate = DateTime.UtcNow;
                sub.NextBillingDate = null;
                sub.CancelledReason = req.Reason ?? "User cancelled";
                supplier.Tier = "basic";
                supplier.CommissionRate = await BasicPlan.CommissionRateAsync(db);
                await db.SaveChangesAsync();
            }
            catch (Exception error) when (error is HttpRequestException or InvalidOperationException)
            {
                return StatusCode(StatusCodes.Status502BadGateway,
                    ApiResponse.Fail("Paystack could not cancel your subscription. Please try again."));
            }

            return Ok(ApiResponse.Ok("Your subscription has been cancelled and the plan has ended."));
        }

        sub.Status = "cancelled";
        sub.EndDate = sub.NextBillingDate ?? DateTime.UtcNow;
        sub.CancelledReason = req.Reason ?? "User cancelled";

        // Downgrade to basic
        supplier.Tier = "basic";
        supplier.CommissionRate = await BasicPlan.CommissionRateAsync(db);

        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Subscription cancelled. You are now on the Basic tier."));
    }

    //  GET billing history 
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var history = await db.SupplierSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.SupplierId == supplier.Id)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new
            {
                s.Id, s.BillingCycle, s.Status,
                s.StartDate, s.EndDate, s.NextBillingDate,
                s.AmountPaid, s.PaymentMethod, s.PaymentReference,
                s.CancelledReason, s.CancelAtPeriodEnd,
                plan = new { s.Plan.Tier, s.Plan.Name, s.Plan.MonthlyPrice }
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(history));
    }
}

//  Request DTOs 
public record SubscribeRequest(
    string Tier,
    string? BillingCycle
);

public record CancelSubscriptionRequest(string? Reason);