using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/customer/subscription")]
[Authorize(Roles = "customer")]
public sealed class CustomerSubscriptionController(
    SpazaSureDbContext db,
    PaystackPaymentService paystack) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var subscription = await CustomerPlus.LatestAsync(db, UserId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(Overview(subscription)));
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

        // Checked before a pending row exists, so a failed first attempt can't leave a stuck checkout behind
        var email = user.Email;
        if (string.IsNullOrWhiteSpace(email))
            // A real address is needed here: Paystack emails the receipt and the link to manage SpazaSure Plus.
            return BadRequest(ApiResponse.Fail("Add your email address in your profile first. Paystack emails your receipt there."));
        if (!paystack.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail("Card payments are not switched on yet. Please try again later."));

        var subscription = await CustomerPlus.LatestAsync(db, UserId, cancellationToken);
        if (CustomerPlus.IsActive(subscription))
            return Conflict(ApiResponse.Fail("Your SpazaSure Plus subscription is already active."));
        if (subscription?.PlanKey == "plus" &&
            (subscription.Status is "past_due" or "unpaid" or "incomplete" or "trialing" or "paused" ||
             subscription.Status == "active" && CustomerPlus.AutoRenews(subscription)))
            return Conflict(ApiResponse.Fail("Your SpazaSure Plus renewal is being processed by Paystack. Check the email Paystack sent you."));

        // Someone who paid and closed the page before coming back is switched on, not charged twice
        if (subscription?.Status == "pending" && await TryActivatePendingAsync(subscription, cancellationToken))
            return Ok(ApiResponse<object>.Ok(new { subscriptionId = subscription.Id, alreadyActive = true }));

        if (subscription is null || subscription.Status != "pending")
        {
            subscription = new CustomerSubscription
            {
                CustomerUserId = UserId,
                PlanKey = "plus",
                Status = "pending"
            };
            db.CustomerSubscriptions.Add(subscription);
        }

        // A fresh Paystack page each time: an old link may have expired or been abandoned
        var planCode = paystack.GetPlanCode("CustomerPlusMonthly");
        var reference = $"CUS-{subscription.Id:N}-{Guid.NewGuid():N}";
        var callbackUrl = paystack.GetCallbackUrl("CustomerSubscription");
        PaystackCheckout checkout;
        try
        {
            checkout = string.IsNullOrWhiteSpace(planCode)
                // No renewing plan set up in Paystack: pay for one month at a time
                ? await paystack.CreateOnceOffCheckoutAsync(
                    CustomerPlus.MonthlyPrice, email, reference, callbackUrl, cancellationToken)
                : await paystack.CreateSubscriptionCheckoutAsync(
                    CustomerPlus.MonthlyPrice, email, reference, callbackUrl, planCode, "monthly", cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse.Fail("Paystack could not open checkout right now. Please try again."));
        }

        subscription.PaymentProviderCheckoutReference = reference;
        subscription.PaymentProviderCheckoutUrl = checkout.AuthorizationUrl;
        subscription.PaymentProviderPlanCode = string.IsNullOrWhiteSpace(planCode) ? null : planCode;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            subscriptionId = subscription.Id,
            checkoutUrl = checkout.AuthorizationUrl,
            alreadyActive = false
        }));
    }

    /// <summary>
    /// Asks Paystack about a pending payment straight away, so Plus switches on as soon as
    /// the customer is back in the app instead of waiting for the webhook.
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        var subscription = await CustomerPlus.LatestAsync(db, UserId, cancellationToken);
        if (subscription?.Status == "pending")
            await TryActivatePendingAsync(subscription, cancellationToken);
        return Ok(ApiResponse<object>.Ok(Overview(subscription)));
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
        if (subscription is null)
            return NotFound(ApiResponse.Fail("There is no active SpazaSure Plus subscription to cancel."));
        if (!CustomerPlus.AutoRenews(subscription))
            return Conflict(ApiResponse.Fail("This month was paid once-off and won't renew, so there is nothing to cancel."));
        if (string.IsNullOrWhiteSpace(subscription.PaymentProviderSubscriptionCode) ||
            string.IsNullOrWhiteSpace(subscription.PaymentProviderEmailToken))
            return Conflict(ApiResponse.Fail(
                "Paystack has not returned the cancellation details for this subscription yet. Please try again shortly."));

        try
        {
            await paystack.DisableSubscriptionAsync(
                subscription.PaymentProviderSubscriptionCode,
                subscription.PaymentProviderEmailToken,
                cancellationToken);
            subscription.Status = "cancelled";
            subscription.CancelAtPeriodEnd = false;
            subscription.CurrentPeriodEnd = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse.Fail("Paystack could not cancel your subscription. Please try again."));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            cancelAtPeriodEnd = false,
            currentPeriodEnd = subscription.CurrentPeriodEnd
        }, "Your subscription has been cancelled."));
    }

    private async Task<bool> TryActivatePendingAsync(
        CustomerSubscription subscription,
        CancellationToken cancellationToken)
    {
        var reference = subscription.PaymentProviderCheckoutReference;
        if (string.IsNullOrWhiteSpace(reference) || !paystack.IsConfigured) return false;

        PaystackTransaction transaction;
        try
        {
            transaction = await paystack.VerifyTransactionAsync(reference, cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException
                                          or KeyNotFoundException)
        {
            // Not paid yet, or Paystack is unreachable: the webhook still switches Plus on later
            return false;
        }
        if (!CustomerPlus.IsExpectedPayment(transaction, reference)) return false;

        CustomerPlus.Activate(subscription, transaction);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static object Overview(CustomerSubscription? subscription)
    {
        var isPlusActive = CustomerPlus.IsActive(subscription);
        var currentPlan = isPlusActive ||
                          subscription?.PlanKey == "plus" &&
                          subscription.Status is "pending" or "past_due" or "unpaid" ||
                          subscription?.Status == "active" && !CustomerPlus.IsLapsed(subscription)
            ? subscription!.PlanKey
            : "free";
        var status = CustomerPlus.IsLapsed(subscription) ? "expired" : subscription?.Status ?? "active";

        return new
        {
            currentPlan,
            status,
            isPlusActive,
            autoRenews = CustomerPlus.AutoRenews(subscription),
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
                    monthlyPrice = CustomerPlus.MonthlyPrice,
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
        };
    }
}

public sealed record CustomerSubscriptionCheckoutRequest(string PlanKey);
