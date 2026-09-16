using Stripe;
using Stripe.Checkout;

namespace SpazaSure.UserService.Services;

public sealed class StripePaymentService(IConfiguration config)
{
    private readonly string _secretKey = config["Stripe:SecretKey"] ?? string.Empty;
    private readonly string _webhookSecret = config["Stripe:WebhookSecret"] ?? string.Empty;
    private readonly string _successUrl = config["Stripe:SuccessUrl"] ?? string.Empty;
    private readonly string _cancelUrl = config["Stripe:CancelUrl"] ?? string.Empty;

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

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
            SuccessUrl = $"{_successUrl}?payment=success&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_cancelUrl}?payment=cancelled",
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
