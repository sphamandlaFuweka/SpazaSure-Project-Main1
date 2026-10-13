using Stripe;
using Stripe.Checkout;
using Microsoft.AspNetCore.WebUtilities;

namespace SpazaSure.UserService.Services;

public sealed class StripePaymentService(IConfiguration config)
{
    public const decimal CustomerPlusMonthlyPrice = 19.99m;
    public static long CustomerPlusMonthlyPriceInCents =>
        decimal.ToInt64(CustomerPlusMonthlyPrice * 100m);

    private readonly string _secretKey = config["Stripe:SecretKey"] ?? string.Empty;
    private readonly string _webhookSecret = config["Stripe:WebhookSecret"] ?? string.Empty;
    private readonly string _successUrl = config["Stripe:SuccessUrl"] ?? string.Empty;
    private readonly string _cancelUrl = config["Stripe:CancelUrl"] ?? string.Empty;
    private readonly string _onboardingSuccessUrl = config["Stripe:OnboardingSuccessUrl"] ?? string.Empty;
    private readonly string _onboardingCancelUrl = config["Stripe:OnboardingCancelUrl"] ?? string.Empty;
    private readonly string _customerSuccessUrl = config["Stripe:CustomerSuccessUrl"] ??
                                                  config["Stripe:OnboardingSuccessUrl"] ?? string.Empty;
    private readonly string _customerCancelUrl = config["Stripe:CustomerCancelUrl"] ??
                                                config["Stripe:OnboardingCancelUrl"] ?? string.Empty;
    private readonly string _walletSuccessUrl = config["Stripe:WalletSuccessUrl"] ??
                                                "https://api.spazasure.co.za/";
    private readonly string _walletCancelUrl = config["Stripe:WalletCancelUrl"] ??
                                               "https://api.spazasure.co.za/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_secretKey) &&
                                !string.IsNullOrWhiteSpace(_successUrl) &&
                                !string.IsNullOrWhiteSpace(_cancelUrl);

    public async Task<string> CreateCheckoutSessionAsync(
        decimal amount,
        string currency,
        string description,
        string paymentType,
        Guid paymentId,
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        StripeConfiguration.ApiKey = _secretKey;
        var (successUrl, cancelUrl) = paymentType == "shop_onboarding"
            ? (_onboardingSuccessUrl, _onboardingCancelUrl)
            : (_successUrl, _cancelUrl);
        if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(cancelUrl))
            throw new InvalidOperationException($"Stripe return URLs are not configured for {paymentType}.");

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
            SuccessUrl = $"{successUrl}{(successUrl.Contains('?') ? '&' : '?')}payment=success&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = QueryHelpers.AddQueryString(cancelUrl, "payment", "cancelled"),
            Metadata = new Dictionary<string, string>
            {
                ["payment_type"] = paymentType,
                ["payment_id"] = paymentId.ToString()
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmount = decimal.ToInt64(decimal.Round(amount * 100m, 0)),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = description
                        }
                    }
                }
            ]
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        return session.Url;
    }

    public Task<Session> CreateSupplierSubscriptionCheckoutSessionAsync(
        decimal amount,
        string billingCycle,
        string description,
        Guid subscriptionId,
        string customerEmail,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (billingCycle is not ("monthly" or "annual"))
            throw new ArgumentException("Billing cycle must be monthly or annual.", nameof(billingCycle));

        StripeConfiguration.ApiKey = _secretKey;
        var metadata = new Dictionary<string, string>
        {
            ["payment_type"] = "supplier_subscription",
            ["payment_id"] = subscriptionId.ToString()
        };
        var successUrl =
            $"{_successUrl}{(_successUrl.Contains('?') ? '&' : '?')}payment=success&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = QueryHelpers.AddQueryString(_cancelUrl, "payment", "cancelled");

        return new SessionService().CreateAsync(
            new SessionCreateOptions
            {
                Mode = "subscription",
                CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = metadata,
                SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "zar",
                            UnitAmount = decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)),
                            Recurring = new SessionLineItemPriceDataRecurringOptions
                            {
                                Interval = billingCycle == "annual" ? "year" : "month"
                            },
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = description
                            }
                        }
                    }
                ]
            },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);
    }

    public async Task<Session> CreateCustomerSubscriptionCheckoutSessionAsync(
        Guid customerUserId,
        string customerEmail,
        Guid subscriptionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(_customerSuccessUrl) ||
            string.IsNullOrWhiteSpace(_customerCancelUrl))
            throw new InvalidOperationException("Stripe customer subscription return URLs are not configured.");

        StripeConfiguration.ApiKey = _secretKey;
        var successUrl =
            $"{_customerSuccessUrl}{(_customerSuccessUrl.Contains('?') ? '&' : '?')}payment=success&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            _customerCancelUrl, "payment", "cancelled");
        var metadata = new Dictionary<string, string>
        {
            ["payment_type"] = "customer_subscription",
            ["payment_id"] = subscriptionId.ToString(),
            ["customer_user_id"] = customerUserId.ToString(),
            ["plan_key"] = "plus"
        };

        return await new SessionService().CreateAsync(
            new SessionCreateOptions
            {
                Mode = "subscription",
                CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = metadata,
                SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
                LineItems =
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "zar",
                            UnitAmount = CustomerPlusMonthlyPriceInCents,
                            Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = "month" },
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "SpazaSure Plus",
                                Description = "Monthly customer subscription"
                            }
                        }
                    }
                }
            },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);
    }

    public Task<Session> CreateWalletTopUpCheckoutSessionAsync(
        decimal amount,
        Guid transactionId,
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
            throw new InvalidOperationException("Stripe payment configuration is missing.");
        if (string.IsNullOrWhiteSpace(_walletSuccessUrl) ||
            string.IsNullOrWhiteSpace(_walletCancelUrl))
            throw new InvalidOperationException("Stripe wallet return URLs are not configured.");

        StripeConfiguration.ApiKey = _secretKey;
        var metadata = new Dictionary<string, string>
        {
            ["payment_type"] = "wallet_top_up",
            ["payment_id"] = transactionId.ToString()
        };
        var successUrl =
            $"{_walletSuccessUrl}{(_walletSuccessUrl.Contains('?') ? '&' : '?')}payment=success&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = QueryHelpers.AddQueryString(_walletCancelUrl, "payment", "cancelled");

        return new SessionService().CreateAsync(
            new SessionCreateOptions
            {
                Mode = "payment",
                CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = metadata,
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "zar",
                            UnitAmount = decimal.ToInt64(decimal.Round(
                                amount * 100m, 0, MidpointRounding.AwayFromZero)),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "SpazaSure wallet top-up"
                            }
                        }
                    }
                ]
            },
            new RequestOptions { IdempotencyKey = $"wallet-top-up:{transactionId}" },
            cancellationToken);
    }

    public Task<Session> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        StripeConfiguration.ApiKey = _secretKey;
        return new SessionService().GetAsync(sessionId, cancellationToken: cancellationToken);
    }

    public Task<Subscription> CancelSubscriptionAtPeriodEndAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        StripeConfiguration.ApiKey = _secretKey;
        return new SubscriptionService().UpdateAsync(
            subscriptionId,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = true },
            cancellationToken: cancellationToken);
    }

    public Event ConstructWebhookEvent(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret))
            throw new InvalidOperationException("Stripe webhook secret is not configured.");
        return EventUtility.ConstructEvent(payload, signature, _webhookSecret);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Stripe payment configuration is missing.");
    }
}
