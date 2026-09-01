using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/supplier/subscription")]
[Authorize]
public class SubscriptionController(SpazaSureDbContext db) : ControllerBase
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
                sub.AmountPaid, sub.PaymentMethod, sub.PaymentReference
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

        // Cancel existing active subscriptions
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
        var paymentMethod = plan.MonthlyPrice == 0m ? "free" : (req.PaymentMethod ?? "eft");

        var sub = new SupplierSubscription
        {
            SupplierId = supplier.Id,
            PlanId = plan.Id,
            BillingCycle = billingCycle,
            Status = status,
            StartDate = now,
            NextBillingDate = plan.MonthlyPrice == 0m ? null : nextBilling,
            AmountPaid = plan.MonthlyPrice == 0m ? 0m : amount,
            PaymentMethod = paymentMethod,
            PaymentReference = req.PaymentReference
        };

        db.SupplierSubscriptions.Add(sub);

        // Update supplier tier and commission immediately (for free tier or on payment confirmation)
        if (plan.MonthlyPrice == 0m || req.ConfirmPayment == true)
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

    //  POST confirm payment (after EFT / PayFast callback) 
    [HttpPost("{subscriptionId:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid subscriptionId, [FromBody] ConfirmPaymentRequest req)
    {
        var sub = await db.SupplierSubscriptions
            .Include(s => s.Supplier)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (sub is null) return NotFound(ApiResponse.Fail("Subscription not found."));
        if (sub.Supplier.UserId != UserId) return Forbid();

        sub.Status = "active";
        sub.PaymentReference = req.PaymentReference ?? sub.PaymentReference;
        sub.Supplier.Tier = sub.Plan.Tier;
        sub.Supplier.CommissionRate = sub.Plan.CommissionRate;

        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            subscriptionId = sub.Id,
            tier = sub.Supplier.Tier,
            commissionRate = sub.Supplier.CommissionRate,
            status = sub.Status,
            paymentReference = sub.PaymentReference
        }));
    }

    //  POST cancel subscription 
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelSubscriptionRequest req)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var sub = await db.SupplierSubscriptions
            .Where(s => s.SupplierId == supplier.Id && s.Status == "active")
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();

        if (sub is null) return NotFound(ApiResponse.Fail("No active subscription found."));

        sub.Status = "cancelled";
        sub.EndDate = sub.NextBillingDate ?? DateTime.UtcNow;
        sub.CancelledReason = req.Reason ?? "User cancelled";

        // Downgrade to basic
        supplier.Tier = "basic";
        supplier.CommissionRate = 5m;

        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Subscription cancelled. You will be on Basic tier from the end of your billing period."));
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
                s.CancelledReason,
                plan = new { s.Plan.Tier, s.Plan.Name, s.Plan.MonthlyPrice }
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(history));
    }
}

//  Request DTOs 
public record SubscribeRequest(
    string Tier,
    string? BillingCycle,
    string? PaymentMethod,
    string? PaymentReference,
    bool? ConfirmPayment
);

public record ConfirmPaymentRequest(string? PaymentReference);
public record CancelSubscriptionRequest(string? Reason);