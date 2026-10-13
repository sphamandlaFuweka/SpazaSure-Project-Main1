using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Stripe;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using SpazaSure.Shared.Helpers;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Authorize]
[Route("api/supplier/payment")]
public class PaymentController(
    SpazaSureDbContext db,
    StripePaymentService stripe,
    EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("stripe/checkout-session")]
    public async Task<IActionResult> CreateStripeCheckout(
        [FromBody] InitiatePaymentRequest req,
        CancellationToken cancellationToken)
    {
        var sub = await db.SupplierSubscriptions.Include(s => s.Plan).Include(s => s.Supplier)
            .FirstOrDefaultAsync(s => s.Id == req.SubscriptionId);
        if (sub is null) return NotFound(ApiResponse.Fail("Subscription not found."));
        if (sub.Supplier.UserId != UserId) return Forbid();
        if (sub.Status != "pending") return BadRequest(ApiResponse.Fail("Only pending subscriptions can be paid."));

        var amount = sub.BillingCycle == "annual" ? sub.Plan.AnnualPrice : sub.Plan.MonthlyPrice;
        if (!string.IsNullOrWhiteSpace(sub.StripeCheckoutSessionId))
        {
            var existingSession = await stripe.GetCheckoutSessionAsync(
                sub.StripeCheckoutSessionId, cancellationToken);
            if (existingSession.Status == "open" && !string.IsNullOrWhiteSpace(existingSession.Url))
                return Ok(ApiResponse<object>.Ok(new
                {
                    checkoutUrl = existingSession.Url,
                    subscriptionId = sub.Id,
                    amount
                }));
            if (existingSession.Status == "complete")
                return Conflict(ApiResponse.Fail("Your payment is processing. Refresh your subscription status shortly."));
        }

        var idempotencyKey = sub.StripeCheckoutSessionId is null
            ? $"supplier-subscription:{sub.Id}:initial"
            : $"supplier-subscription:{sub.Id}:after:{sub.StripeCheckoutSessionId}";
        var session = await stripe.CreateSupplierSubscriptionCheckoutSessionAsync(
            amount,
            sub.BillingCycle,
            $"SpazaSure {sub.Plan.Name} Plan",
            sub.Id,
            sub.Supplier.Email ?? string.Empty,
            idempotencyKey,
            cancellationToken);
        sub.StripeCheckoutSessionId = session.Id;
        sub.PaymentMethod = "stripe";
        sub.PaymentReference = session.Id;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            checkoutUrl = session.Url,
            subscriptionId = sub.Id,
            amount
        }));
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
            if ((stripeEvent.Type == Stripe.EventTypes.CheckoutSessionCompleted ||
                 stripeEvent.Type == "checkout.session.async_payment_succeeded") &&
                stripeEvent.Data.Object is Stripe.Checkout.Session session)
            {
                var metadata = session.Metadata;
                if (metadata.TryGetValue("payment_type", out var type) &&
                    metadata.TryGetValue("payment_id", out var rawId) &&
                    Guid.TryParse(rawId, out var id) && type == "supplier_subscription")
                {
                    var sub = await db.SupplierSubscriptions.Include(s => s.Supplier).Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == id);
                    if (sub is null) return StatusCode(StatusCodes.Status500InternalServerError);
                    var expectedAmount = sub.BillingCycle == "annual"
                        ? sub.Plan.AnnualPrice
                        : sub.Plan.MonthlyPrice;
                    var expectedMinorUnits = decimal.ToInt64(decimal.Round(
                        expectedAmount * 100m, 0, MidpointRounding.AwayFromZero));
                    if (session.Currency != "zar" || session.AmountTotal != expectedMinorUnits ||
                        (session.Mode != "payment" && session.Mode != "subscription"))
                        return BadRequest();
                    if (!string.IsNullOrWhiteSpace(sub.StripeCheckoutSessionId) &&
                        sub.StripeCheckoutSessionId != session.Id)
                        return Conflict();
                    if (session.Mode == "subscription" && string.IsNullOrWhiteSpace(session.SubscriptionId))
                        return BadRequest();

                    if (sub.Status is "cancelled" or "expired")
                        return Ok();

                    sub.StripeCheckoutSessionId = session.Id;
                    if (session.Mode == "subscription")
                    {
                        sub.StripeCustomerId = session.CustomerId;
                        sub.StripeSubscriptionId = session.SubscriptionId;
                    }

                    if (session.PaymentStatus == "paid")
                    {
                        sub.Status = "active";
                        sub.PaymentMethod = "stripe";
                        sub.PaymentReference = session.PaymentIntentId ?? session.Id;
                        sub.AmountPaid = expectedAmount;
                        if (sub.NextBillingDate is null)
                            sub.NextBillingDate = sub.BillingCycle == "annual"
                                ? DateTime.UtcNow.AddYears(1)
                                : DateTime.UtcNow.AddMonths(1);
                        sub.Supplier.Tier = sub.Plan.Tier;
                        sub.Supplier.CommissionRate = sub.Plan.CommissionRate;
                    }
                    await db.SaveChangesAsync();
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
                else if (metadata.TryGetValue("payment_type", out var customerSubscriptionType) &&
                         metadata.TryGetValue("payment_id", out var customerSubscriptionRawId) &&
                         customerSubscriptionType == "customer_subscription" &&
                         Guid.TryParse(customerSubscriptionRawId, out var customerSubscriptionId))
                {
                    var subscription = await db.CustomerSubscriptions
                        .FirstOrDefaultAsync(s => s.Id == customerSubscriptionId);
                    if (subscription is null) return StatusCode(StatusCodes.Status500InternalServerError);

                    if (session.Mode != "subscription" ||
                        session.PaymentStatus != "paid" ||
                        session.Currency != "zar" ||
                        session.AmountTotal != StripePaymentService.CustomerPlusMonthlyPriceInCents ||
                        string.IsNullOrWhiteSpace(session.SubscriptionId))
                        return BadRequest();

                    if (subscription.Status != "active")
                    {
                        subscription.Status = "active";
                        subscription.StripeSubscriptionId = session.SubscriptionId;
                        subscription.StripeCustomerId = session.CustomerId;
                        subscription.PaymentReference = session.Id;
                        await db.SaveChangesAsync();
                    }
                }
                else if (metadata.TryGetValue("payment_type", out var orderType) &&
                         metadata.TryGetValue("checkout_id", out var checkoutRawId) &&
                         orderType == "shop_orders" &&
                         Guid.TryParse(checkoutRawId, out var checkoutId))
                {
                    var orders = await db.Orders.Include(o => o.Supplier).Include(o => o.Shop)
                        .Where(o => o.StripeCheckoutId == checkoutId)
                        .ToListAsync();
                    if (orders.Count == 0) return StatusCode(StatusCodes.Status500InternalServerError);
                    if (orders.Any(o => !string.Equals(
                            o.StripeCheckoutSessionId, session.Id, StringComparison.Ordinal)))
                        return Conflict();

                    var expectedAmount = decimal.ToInt64(decimal.Round(
                        orders.Sum(o => o.TotalAmount) * 100m, 0, MidpointRounding.AwayFromZero));
                    if (session.Currency != "zar" || session.AmountTotal != expectedAmount)
                        return BadRequest();

                    if (session.PaymentStatus == "paid" && orders.Any(o => o.PaymentStatus != "paid"))
                    {
                        if (orders.Any(o => o.PaymentStatus != "pending"))
                            return Conflict();

                        foreach (var order in orders)
                        {
                            order.PaymentMethod = "stripe";
                            order.PaymentStatus = "paid";
                        }
                        await db.SaveChangesAsync();

                        foreach (var order in orders)
                        {
                            events.PublishNotification(
                                supplierId: order.Supplier.UserId.ToString(),
                                type: "order",
                                title: "New Order Received",
                                message: $"{order.Shop.ShopName} placed order {order.OrderNumber} for R{order.TotalAmount:F2}",
                                priority: order.TotalAmount > 1000 ? "high" : "normal",
                                referenceId: order.Id.ToString(),
                                routingKey: "order.placed");
                        }
                    }
                }
            }

            if ((stripeEvent.Type == "customer.subscription.updated" ||
                 stripeEvent.Type == "customer.subscription.deleted") &&
                stripeEvent.Data.Object is Stripe.Subscription stripeSubscription)
            {
                var subscription = await db.CustomerSubscriptions
                    .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);
                if (subscription is not null)
                {
                    subscription.Status = stripeSubscription.Status == "canceled"
                        ? "cancelled"
                        : stripeSubscription.Status;
                    subscription.CancelAtPeriodEnd = stripeSubscription.CancelAtPeriodEnd;
                    subscription.CurrentPeriodEnd = stripeSubscription.Items.Data.FirstOrDefault()?.CurrentPeriodEnd;
                    await db.SaveChangesAsync();
                }

                var supplierSubscription = await db.SupplierSubscriptions
                    .Include(s => s.Supplier)
                    .Include(s => s.Plan)
                    .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);
                if (supplierSubscription is not null)
                {
                    var periodEnd = stripeSubscription.Items.Data.FirstOrDefault()?.CurrentPeriodEnd;
                    supplierSubscription.CancelAtPeriodEnd = stripeSubscription.CancelAtPeriodEnd;
                    supplierSubscription.NextBillingDate = periodEnd;
                    if (stripeSubscription.Status == "canceled")
                    {
                        supplierSubscription.Status = "cancelled";
                        supplierSubscription.EndDate = periodEnd ?? DateTime.UtcNow;
                        supplierSubscription.Supplier.Tier = "basic";
                        supplierSubscription.Supplier.CommissionRate = 5m;
                    }
                    else
                    {
                        supplierSubscription.Status = stripeSubscription.Status;
                        if (stripeSubscription.Status == "active")
                        {
                            supplierSubscription.Supplier.Tier = supplierSubscription.Plan.Tier;
                            supplierSubscription.Supplier.CommissionRate = supplierSubscription.Plan.CommissionRate;
                        }
                        else if (stripeSubscription.Status != "trialing")
                        {
                            supplierSubscription.Supplier.Tier = "basic";
                            supplierSubscription.Supplier.CommissionRate = 5m;
                        }
                    }
                    await db.SaveChangesAsync();
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