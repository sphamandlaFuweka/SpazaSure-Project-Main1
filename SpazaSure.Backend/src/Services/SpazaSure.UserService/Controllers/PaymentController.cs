using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Authorize]
[Route("api/supplier/payment")]
public class PaymentController(
    SpazaSureDbContext db,
    PaystackPaymentService paystack,
    EventPublisher events) : ControllerBase
{
    private const decimal CustomerPlusMonthlyPrice = 19.99m;
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("paystack/checkout-session")]
    public async Task<IActionResult> CreatePaystackCheckout(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await db.SupplierSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Supplier)
            .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId, cancellationToken);
        if (subscription is null)
            return NotFound(ApiResponse.Fail("Subscription not found."));
        if (subscription.Supplier.UserId != UserId)
            return Forbid();
        if (subscription.Status != "pending")
            return BadRequest(ApiResponse.Fail("Only pending subscriptions can be paid."));

        if (!string.IsNullOrWhiteSpace(subscription.PaymentProviderCheckoutUrl))
            return Ok(ApiResponse<object>.Ok(new
            {
                checkoutUrl = subscription.PaymentProviderCheckoutUrl,
                subscriptionId = subscription.Id,
                amount = subscription.BillingCycle == "annual"
                    ? subscription.Plan.AnnualPrice
                    : subscription.Plan.MonthlyPrice
            }));

        var amount = subscription.BillingCycle == "annual"
            ? subscription.Plan.AnnualPrice
            : subscription.Plan.MonthlyPrice;
        var cycleKey = subscription.BillingCycle == "annual" ? "Annual" : "Monthly";
        var planKey = $"Supplier{ToPascalCase(subscription.Plan.Tier)}{cycleKey}";
        var planCode = paystack.GetPlanCode(planKey);
        if (string.IsNullOrWhiteSpace(planCode))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail($"Paystack plan {planKey} is not configured."));

        var email = subscription.Supplier.Email;
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse.Fail("A supplier email address is required for subscription checkout."));

        var reference = $"SUB-{subscription.Id:N}-{Guid.NewGuid():N}";
        var callbackUrl = paystack.GetCallbackUrl("SupplierSubscription");
        var checkout = await paystack.CreateSubscriptionCheckoutAsync(
            amount,
            email,
            reference,
            callbackUrl,
            planCode,
            subscription.BillingCycle == "annual" ? "annually" : "monthly",
            cancellationToken);

        subscription.PaymentMethod = "paystack";
        subscription.PaymentReference = reference;
        subscription.PaymentProviderCheckoutReference = reference;
        subscription.PaymentProviderCheckoutUrl = checkout.AuthorizationUrl;
        subscription.PaymentProviderPlanCode = planCode;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new
        {
            checkoutUrl = checkout.AuthorizationUrl,
            subscriptionId = subscription.Id,
            amount
        }));
    }

    [AllowAnonymous]
    [HttpPost("paystack/webhook")]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> PaystackWebhook(CancellationToken cancellationToken)
    {
        if (!paystack.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        await using var payloadStream = new MemoryStream();
        await Request.Body.CopyToAsync(payloadStream, cancellationToken);
        var payload = payloadStream.ToArray();
        if (!paystack.IsValidWebhookSignature(
                payload,
                Request.Headers["x-paystack-signature"].ToString()))
            return Unauthorized();

        JsonDocument eventDocument;
        try
        {
            eventDocument = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        using (eventDocument)
        {
            var root = eventDocument.RootElement;
            if (!root.TryGetProperty("event", out var eventName) ||
                eventName.ValueKind != JsonValueKind.String)
                return BadRequest();

            var data = root.TryGetProperty("data", out var eventData)
                ? eventData
                : default;
            switch (eventName.GetString())
            {
                case "charge.success":
                    return await HandleSuccessfulCharge(data, cancellationToken);
                case "subscription.create":
                    return await HandleSubscriptionCreated(data, cancellationToken);
                case "subscription.disable":
                    return await HandleSubscriptionDisabled(data, cancellationToken);
                case "invoice.payment_failed":
                    return await HandleFailedInvoice(data, cancellationToken);
                default:
                    return Ok();
            }
        }
    }

    [HttpGet("status/{subscriptionId:guid}")]
    public async Task<IActionResult> GetStatus(Guid subscriptionId)
    {
        var subscription = await db.SupplierSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Supplier)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);
        if (subscription is null)
            return NotFound(ApiResponse.Fail("Subscription not found."));
        if (subscription.Supplier.UserId != UserId)
            return Forbid();

        return Ok(ApiResponse<object>.Ok(new
        {
            subscriptionId = subscription.Id,
            status = subscription.Status,
            tier = subscription.Plan.Tier,
            paymentMethod = subscription.PaymentMethod,
            paymentReference = subscription.PaymentReference,
            amountPaid = subscription.AmountPaid
        }));
    }

    private async Task<IActionResult> HandleSuccessfulCharge(
        JsonElement eventData,
        CancellationToken cancellationToken)
    {
        if (!TryGetString(eventData, "reference", out var reference))
            return BadRequest();

        var onboardingPayment = await db.ShopOnboardingPayments
            .Include(p => p.Shop)
            .FirstOrDefaultAsync(p => p.ProviderPaymentId == reference, cancellationToken);
        var walletTransaction = onboardingPayment is null
            ? await db.ShopWalletTransactions.FirstOrDefaultAsync(
                t => t.Reference == reference && t.Method == "paystack",
                cancellationToken)
            : null;
        var orders = onboardingPayment is null && walletTransaction is null
            ? await db.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Shop)
                .Where(o => o.PaymentProviderReference == reference)
                .ToListAsync(cancellationToken)
            : [];
        var customerSubscription = onboardingPayment is null &&
                                   walletTransaction is null &&
                                   orders.Count == 0
            ? await db.CustomerSubscriptions
                .FirstOrDefaultAsync(
                    s => s.PaymentProviderCheckoutReference == reference,
                    cancellationToken)
            : null;
        var supplierSubscription = onboardingPayment is null &&
                                   walletTransaction is null &&
                                   orders.Count == 0 &&
                                   customerSubscription is null
            ? await db.SupplierSubscriptions
                .Include(s => s.Supplier)
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(
                    s => s.PaymentProviderCheckoutReference == reference,
                    cancellationToken)
            : null;

        var transaction = await paystack.VerifyTransactionAsync(reference, cancellationToken);
        if (!transaction.IsSuccessful ||
            !string.Equals(transaction.Reference, reference, StringComparison.Ordinal) ||
            !string.Equals(transaction.Currency, "ZAR", StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        if (onboardingPayment is not null)
        {
            if (!IsExpectedAmount(transaction, onboardingPayment.Amount))
                return BadRequest();
            if (onboardingPayment.Status == "completed")
                return Ok();
            if (onboardingPayment.Status != "pending")
                return Conflict();

            onboardingPayment.Status = "completed";
            onboardingPayment.CompletedAt = DateTime.UtcNow;
            onboardingPayment.Shop.OnboardingFeePaid = true;
            onboardingPayment.Shop.OnboardingFeeRef = reference;
            await db.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        if (walletTransaction is not null)
        {
            if (!IsExpectedAmount(transaction, walletTransaction.Amount))
                return BadRequest();
            if (walletTransaction.Status == "approved")
                return Ok();
            if (walletTransaction.Status != "pending")
                return Conflict();

            walletTransaction.Status = "approved";
            walletTransaction.ApprovedAt = DateTime.UtcNow;
            walletTransaction.Notes = "Payment confirmed by Paystack webhook.";
            await db.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        if (orders.Count > 0)
        {
            if (orders.Any(o => o.PaymentStatus != "pending" && o.PaymentStatus != "paid"))
                return Conflict();
            if (!IsExpectedAmount(transaction, orders.Sum(o => o.TotalAmount)))
                return BadRequest();

            var unpaidOrders = orders.Where(o => o.PaymentStatus != "paid").ToList();
            foreach (var order in unpaidOrders)
            {
                order.PaymentMethod = "paystack";
                order.PaymentStatus = "paid";
            }
            if (unpaidOrders.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                foreach (var order in unpaidOrders)
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
            return Ok();
        }

        if (customerSubscription is not null)
        {
            if (!IsExpectedAmount(transaction, CustomerPlusMonthlyPrice))
                return BadRequest();
            ActivateCustomerSubscription(customerSubscription, transaction);
            await db.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        if (supplierSubscription is not null)
        {
            var expectedAmount = supplierSubscription.BillingCycle == "annual"
                ? supplierSubscription.Plan.AnnualPrice
                : supplierSubscription.Plan.MonthlyPrice;
            if (!IsExpectedAmount(transaction, expectedAmount))
                return BadRequest();
            ActivateSupplierSubscription(supplierSubscription, transaction);
            await db.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        await ActivateRecurringSubscription(transaction, cancellationToken);
        return Ok();
    }

    private async Task ActivateRecurringSubscription(
        PaystackTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transaction.CustomerCode) ||
            string.IsNullOrWhiteSpace(transaction.PlanCode))
            return;

        var customerSubscription = await db.CustomerSubscriptions
            .FirstOrDefaultAsync(
                s => s.PaymentProviderCustomerCode == transaction.CustomerCode &&
                     s.PaymentProviderPlanCode == transaction.PlanCode &&
                     s.Status == "active",
                cancellationToken);
        if (customerSubscription is not null &&
            IsExpectedAmount(transaction, CustomerPlusMonthlyPrice))
        {
            customerSubscription.PaymentReference = transaction.Reference;
            customerSubscription.CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);
            return;
        }

        var supplierSubscription = await db.SupplierSubscriptions
            .Include(s => s.Supplier)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(
                s => s.PaymentProviderCustomerCode == transaction.CustomerCode &&
                     s.PaymentProviderPlanCode == transaction.PlanCode &&
                     s.Status == "active",
                cancellationToken);
        if (supplierSubscription is null)
            return;

        var amount = supplierSubscription.BillingCycle == "annual"
            ? supplierSubscription.Plan.AnnualPrice
            : supplierSubscription.Plan.MonthlyPrice;
        if (!IsExpectedAmount(transaction, amount))
            return;

        supplierSubscription.PaymentReference = transaction.Reference;
        supplierSubscription.AmountPaid = amount;
        supplierSubscription.NextBillingDate = supplierSubscription.BillingCycle == "annual"
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);
    }

    private async Task<IActionResult> HandleSubscriptionCreated(
        JsonElement data,
        CancellationToken cancellationToken)
    {
        if ((!TryGetString(data, "subscription_code", out var subscriptionCode) &&
             !TryGetString(data, "subscription", out subscriptionCode)) ||
            (!TryGetString(data, "email_token", out var emailToken) &&
             !TryGetNestedString(data, "subscription", "email_token", out emailToken)) ||
            !TryGetString(data, "customer", out var customerCode) ||
            !TryGetString(data, "plan", out var planCode))
            return BadRequest();

        var customerEmail = TryGetNestedString(data, "customer", "email", out var email)
            ? email
            : null;
        var customer = await db.CustomerSubscriptions.FirstOrDefaultAsync(
            s => s.PaymentProviderPlanCode == planCode &&
                 (s.Status == "pending" || s.Status == "active") &&
                 (s.PaymentProviderCustomerCode == customerCode ||
                  (s.Status == "pending" &&
                   customerEmail != null &&
                   db.Users.Any(u => u.Id == s.CustomerUserId &&
                                    u.Email != null &&
                                    u.Email.ToLower() == customerEmail.ToLower()))),
            cancellationToken);
        if (customer is not null)
        {
            customer.PaymentProviderCustomerCode = customerCode;
            customer.PaymentProviderSubscriptionCode = subscriptionCode;
            customer.PaymentProviderEmailToken = emailToken;
            return await SaveWebhookChanges(cancellationToken);
        }

        var supplier = await db.SupplierSubscriptions
            .Include(s => s.Supplier)
            .FirstOrDefaultAsync(
            s => s.PaymentProviderPlanCode == planCode &&
                 (s.Status == "pending" || s.Status == "active") &&
                 (s.PaymentProviderCustomerCode == customerCode ||
                  (s.Status == "pending" &&
                   customerEmail != null &&
                   s.Supplier.Email != null &&
                   s.Supplier.Email.ToLower() == customerEmail.ToLower())),
            cancellationToken);
        if (supplier is not null)
        {
            supplier.PaymentProviderCustomerCode = customerCode;
            supplier.PaymentProviderSubscriptionCode = subscriptionCode;
            supplier.PaymentProviderEmailToken = emailToken;
            await db.SaveChangesAsync(cancellationToken);
        }
        return Ok();
    }

    private async Task<IActionResult> HandleSubscriptionDisabled(
        JsonElement data,
        CancellationToken cancellationToken)
    {
        if (!TryGetString(data, "subscription_code", out var subscriptionCode) &&
            !TryGetString(data, "subscription", out subscriptionCode))
            return BadRequest();

        var customer = await db.CustomerSubscriptions.FirstOrDefaultAsync(
            s => s.PaymentProviderSubscriptionCode == subscriptionCode,
            cancellationToken);
        if (customer is not null)
        {
            customer.Status = "cancelled";
            customer.CancelAtPeriodEnd = false;
            customer.CurrentPeriodEnd = DateTime.UtcNow;
            return await SaveWebhookChanges(cancellationToken);
        }

        var supplier = await db.SupplierSubscriptions
            .Include(s => s.Supplier)
            .FirstOrDefaultAsync(
                s => s.PaymentProviderSubscriptionCode == subscriptionCode,
                cancellationToken);
        if (supplier is not null)
        {
            supplier.Status = "cancelled";
            supplier.CancelAtPeriodEnd = false;
            supplier.EndDate = DateTime.UtcNow;
            supplier.NextBillingDate = null;
            supplier.Supplier.Tier = "basic";
            supplier.Supplier.CommissionRate = await BasicPlan.CommissionRateAsync(db, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        return Ok();
    }

    private async Task<IActionResult> HandleFailedInvoice(
        JsonElement data,
        CancellationToken cancellationToken)
    {
        if (!TryGetString(data, "subscription", out var subscriptionCode) &&
            !TryGetString(data, "subscription_code", out subscriptionCode))
            return Ok();

        var customer = await db.CustomerSubscriptions.FirstOrDefaultAsync(
            s => s.PaymentProviderSubscriptionCode == subscriptionCode,
            cancellationToken);
        if (customer is not null)
        {
            customer.Status = "past_due";
            return await SaveWebhookChanges(cancellationToken);
        }

        var supplier = await db.SupplierSubscriptions.FirstOrDefaultAsync(
            s => s.PaymentProviderSubscriptionCode == subscriptionCode,
            cancellationToken);
        if (supplier is not null)
        {
            supplier.Status = "past_due";
            await db.SaveChangesAsync(cancellationToken);
        }
        return Ok();
    }

    private static void ActivateCustomerSubscription(
        CustomerSubscription subscription,
        PaystackTransaction transaction)
    {
        subscription.Status = "active";
        subscription.PaymentReference = transaction.Reference;
        subscription.PaymentProviderCustomerCode = transaction.CustomerCode;
        subscription.CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        subscription.CancelAtPeriodEnd = false;
    }

    private static void ActivateSupplierSubscription(
        SupplierSubscription subscription,
        PaystackTransaction transaction)
    {
        var amount = subscription.BillingCycle == "annual"
            ? subscription.Plan.AnnualPrice
            : subscription.Plan.MonthlyPrice;
        subscription.Status = "active";
        subscription.PaymentMethod = "paystack";
        subscription.PaymentReference = transaction.Reference;
        subscription.PaymentProviderCustomerCode = transaction.CustomerCode;
        subscription.AmountPaid = amount;
        subscription.StartDate = DateTime.UtcNow;
        subscription.NextBillingDate = subscription.BillingCycle == "annual"
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);
        subscription.Supplier.Tier = subscription.Plan.Tier;
        subscription.Supplier.CommissionRate = subscription.Plan.CommissionRate;
    }

    private async Task<IActionResult> SaveWebhookChanges(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    private static bool IsExpectedAmount(PaystackTransaction transaction, decimal expectedAmount)
    {
        var expectedMinorUnits = decimal.ToInt64(decimal.Round(
            expectedAmount * 100m, 0, MidpointRounding.AwayFromZero));
        return transaction.Amount == expectedMinorUnits;
    }

    private static bool TryGetString(JsonElement source, string propertyName, out string value)
    {
        value = string.Empty;
        if (source.ValueKind != JsonValueKind.Object ||
            !source.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        if (property.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in new[] { "customer_code", "plan_code", "subscription_code", "code" })
            {
                if (TryGetString(property, candidate, out value))
                    return true;
            }
        }
        return false;
    }

    private static bool TryGetNestedString(
        JsonElement source,
        string propertyName,
        string nestedPropertyName,
        out string value)
    {
        value = string.Empty;
        return source.ValueKind == JsonValueKind.Object &&
               source.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Object &&
               property.TryGetProperty(nestedPropertyName, out var nestedValue) &&
               nestedValue.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = nestedValue.GetString() ?? string.Empty);
    }

    private static string ToPascalCase(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}

public record InitiatePaymentRequest(Guid SubscriptionId);
