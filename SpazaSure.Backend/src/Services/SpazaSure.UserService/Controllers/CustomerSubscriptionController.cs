using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;
using Stripe;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/customer/subscription")]
[Authorize(Roles = "customer")]
public sealed class CustomerSubscriptionController(
    SpazaSureDbContext db,
    StripePaymentService stripe) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var subscription = await db.CustomerSubscriptions
            .Where(s => s.CustomerUserId == UserId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
        var isPlusActive = subscription?.PlanKey == "plus" &&
                           subscription.Status == "active" &&
                           (!subscription.CurrentPeriodEnd.HasValue ||
                            subscription.CurrentPeriodEnd.Value > DateTime.UtcNow);
        var currentPlan = subscription is null ||
                          (subscription.Status is "cancelled" or "canceled" or "incomplete_expired") &&
                          (!subscription.CurrentPeriodEnd.HasValue ||
                           subscription.CurrentPeriodEnd.Value <= DateTime.UtcNow)
            ? "free"
            : subscription.PlanKey;

        return Ok(ApiResponse<object>.Ok(new
        {
            currentPlan,
            status = subscription?.Status ?? "active",
            isPlusActive,
            cancelAtPeriodEnd = subscription?.CancelAtPeriodEnd ?? false,
            currentPeriodEnd = subscription?.CurrentPeriodEnd,
            plans = new[]
            {
                new
                {
                    key = "free",
                    name = "SpazaSure Free",
                    monthlyPrice = 0m,
                    isAvailable = true,
                    description = "Everyday product safety essentials.",
                    features = new[]
                    {
                        "Product scanning with basic results",
                        "Basic product information and verification status",
                        "Warnings for products already flagged",
                        "Report a suspicious product",
                        "View nearby participating shops",
                        "Essential safety guidance"
                    }
                },
                new
                {
                    key = "plus",
                    name = "SpazaSure Plus",
                    monthlyPrice = StripePaymentService.CustomerPlusMonthlyPrice,
                    isAvailable = true,
                    description = "Richer product information and personalised shopping tools.",
                    features = new[]
                    {
                        "Everything in Free",
                        "Detailed product information where available",
                        "Personal scan history and saved products",
                        "Personalised product safety alerts",
                        "Product comparison and shopping guidance",
                        "Cashback and participating-shop rewards, where available",
                        "Priority customer support"
                    }
                },
                new
                {
                    key = "family",
                    name = "SpazaSure Family",
                    monthlyPrice = 34.99m,
                    isAvailable = false,
                    description = "A future plan for households.",
                    features = new[]
                    {
                        "Multiple household profiles",
                        "Shared saved products and scan history",
                        "Shared safety alerts",
                        "Family shopping lists and comparisons",
                        "Rewards at participating shops"
                    }
                }
            }
        }));
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CustomerSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.PlanKey, "plus", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("Only the SpazaSure Plus plan is currently available for checkout."));

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == UserId, cancellationToken);
        if (user is null) return NotFound(ApiResponse.Fail("Customer account not found."));

        var subscription = await db.CustomerSubscriptions
            .Where(s => s.CustomerUserId == UserId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription?.PlanKey == "plus" &&
            subscription.Status is "active" or "past_due" or "unpaid" or "incomplete" or "trialing" or "paused")
            return Conflict(ApiResponse.Fail("Your SpazaSure Plus subscription is already active."));

        if (subscription?.Status == "pending" && !string.IsNullOrWhiteSpace(subscription.StripeCheckoutSessionId))
        {
            var existingSession = await stripe.GetCheckoutSessionAsync(
                subscription.StripeCheckoutSessionId, cancellationToken);
            if (existingSession.Status == "open" && !string.IsNullOrWhiteSpace(existingSession.Url))
                return Ok(ApiResponse<object>.Ok(new
                {
                    subscriptionId = subscription.Id,
                    checkoutUrl = existingSession.Url
                }));
            if (existingSession.Status == "complete")
                return Conflict(ApiResponse.Fail("Your payment is processing. Refresh your subscription status shortly."));
        }

        if (subscription is null || subscription.Status != "pending")
        {
            subscription = new CustomerSubscription
            {
                CustomerUserId = UserId,
                PlanKey = "plus",
                Status = "pending"
            };
            db.CustomerSubscriptions.Add(subscription);
            await db.SaveChangesAsync(cancellationToken);
        }

        var idempotencyKey = subscription.StripeCheckoutSessionId is null
            ? $"customer-plus:{subscription.Id}:initial"
            : $"customer-plus:{subscription.Id}:after:{subscription.StripeCheckoutSessionId}";
        var session = await stripe.CreateCustomerSubscriptionCheckoutSessionAsync(
            UserId,
            user.Email ?? string.Empty,
            subscription.Id,
            idempotencyKey,
            cancellationToken);
        subscription.StripeCheckoutSessionId = session.Id;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            subscriptionId = subscription.Id,
            checkoutUrl = session.Url
        }));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        var subscription = await db.CustomerSubscriptions
            .Where(s => s.CustomerUserId == UserId &&
                        s.PlanKey == "plus" &&
                        (s.Status == "active" || s.Status == "past_due" || s.Status == "unpaid") &&
                        !s.CancelAtPeriodEnd)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (subscription is null || string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
            return NotFound(ApiResponse.Fail("There is no active SpazaSure Plus subscription to cancel."));

        try
        {
            var updated = await stripe.CancelSubscriptionAtPeriodEndAsync(
                subscription.StripeSubscriptionId,
                cancellationToken);
            subscription.CancelAtPeriodEnd = updated.CancelAtPeriodEnd;
            subscription.CurrentPeriodEnd = updated.Items.Data.FirstOrDefault()?.CurrentPeriodEnd;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (StripeException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse.Fail("Stripe could not update your subscription. Please try again."));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            cancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            currentPeriodEnd = subscription.CurrentPeriodEnd
        }, "Your subscription will remain active until the current billing period ends."));
    }
}

public sealed record CustomerSubscriptionCheckoutRequest(string PlanKey);
