using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Stripe;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Authorize]
[Route("api/supplier/payment")]
public class PaymentController(SpazaSureDbContext db, StripePaymentService stripe) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("stripe/checkout-session")]
    public async Task<IActionResult> CreateStripeCheckout([FromBody] InitiatePaymentRequest req)
    {
        var sub = await db.SupplierSubscriptions.Include(s => s.Plan).Include(s => s.Supplier)
            .FirstOrDefaultAsync(s => s.Id == req.SubscriptionId);
        if (sub is null) return NotFound(ApiResponse.Fail("Subscription not found."));
        if (sub.Supplier.UserId != UserId) return Forbid();
        if (sub.Status != "pending") return BadRequest(ApiResponse.Fail("Only pending subscriptions can be paid."));

        var amount = sub.BillingCycle == "annual" ? sub.Plan.AnnualPrice : sub.Plan.MonthlyPrice;
        var url = await stripe.CreateCheckoutSessionAsync(
            amount, "zar", $"SpazaSure {sub.Plan.Name} Plan ({sub.BillingCycle})",
            "supplier_subscription", sub.Id, sub.Supplier.Email ?? string.Empty);
        sub.PaymentMethod = "stripe";
        sub.PaymentReference = $"stripe-pending-{sub.Id:N}";
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { checkoutUrl = url, subscriptionId = sub.Id, amount }));
    }

    [AllowAnonymous]
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        try
        {
            var stripeEvent = stripe.ConstructWebhookEvent(payload, Request.Headers["Stripe-Signature"].ToString());
            if (stripeEvent.Type == Stripe.EventTypes.CheckoutSessionCompleted && stripeEvent.Data.Object is Stripe.Checkout.Session session)
            {
                var metadata = session.Metadata;
                if (metadata.TryGetValue("payment_type", out var type) &&
                    metadata.TryGetValue("payment_id", out var rawId) &&
                    Guid.TryParse(rawId, out var id) && type == "supplier_subscription")
                {
                    var sub = await db.SupplierSubscriptions.Include(s => s.Supplier).Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == id);
                    if (sub is not null && sub.Status != "active")
                    {
                        sub.Status = "active";
                        sub.PaymentMethod = "stripe";
                        sub.PaymentReference = session.PaymentIntentId ?? session.Id;
                        sub.AmountPaid = sub.BillingCycle == "annual" ? sub.Plan.AnnualPrice : sub.Plan.MonthlyPrice;
                        sub.Supplier.Tier = sub.Plan.Tier;
                        sub.Supplier.CommissionRate = sub.Plan.CommissionRate;
                        await db.SaveChangesAsync();
                    }
                }
                else if (metadata.TryGetValue("payment_type", out var onboardingType) &&
                         metadata.TryGetValue("payment_id", out var onboardingRawId) &&
                         onboardingType == "shop_onboarding" &&
                         Guid.TryParse(onboardingRawId, out var paymentId))
                {
                    var payment = await db.ShopOnboardingPayments.Include(p => p.Shop)
                        .FirstOrDefaultAsync(p => p.Id == paymentId);
                    if (payment is not null && payment.Status != "completed")
                    {
                        payment.Status = "completed";
                        payment.ProviderPaymentId = session.PaymentIntentId ?? session.Id;
                        payment.CompletedAt = DateTime.UtcNow;
                        payment.Shop.OnboardingFeePaid = true;
                        payment.Shop.OnboardingFeeRef = payment.ProviderPaymentId;
                        await db.SaveChangesAsync();
                    }
                }
            }
            return Ok();
        }
        catch (StripeException) { return BadRequest(); }
        catch (InvalidOperationException) { return BadRequest(); }
    }
    //  GET /status/:subscriptionId  Check payment status 
    [HttpGet("status/{subscriptionId:guid}")]
    public async Task<IActionResult> GetStatus(Guid subscriptionId)
    {
        var sub = await db.SupplierSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Supplier)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (sub is null) return NotFound(ApiResponse.Fail("Subscription not found."));
        if (sub.Supplier.UserId != UserId) return Forbid();

        return Ok(ApiResponse<object>.Ok(new
        {
            subscriptionId = sub.Id,
            status = sub.Status,
            tier = sub.Plan.Tier,
            paymentMethod = sub.PaymentMethod,
            paymentReference = sub.PaymentReference,
            amountPaid = sub.AmountPaid
        }));
    }

}

public record InitiatePaymentRequest(Guid SubscriptionId);