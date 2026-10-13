using SpazaSure.Infrastructure.Entities;
using Stripe;
using Stripe.Checkout;

namespace SpazaSure.OrderService.Services;

public sealed class OrderStripePaymentService(IConfiguration config)
{
    private readonly string _secretKey = config["Stripe:SecretKey"] ?? string.Empty;
    private readonly string _successUrl = config["Stripe:OrderSuccessUrl"] ?? string.Empty;
    private readonly string _cancelUrl = config["Stripe:OrderCancelUrl"] ?? string.Empty;

    public async Task<Session> CreateCheckoutSessionAsync(
        IReadOnlyCollection<Order> orders,
        Guid checkoutId,
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_secretKey) ||
            string.IsNullOrWhiteSpace(_successUrl) ||
            string.IsNullOrWhiteSpace(_cancelUrl))
        {
            throw new InvalidOperationException("Stripe order checkout configuration is missing.");
        }

        StripeConfiguration.ApiKey = _secretKey;
        var lineItems = new List<SessionLineItemOptions>();
        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    Quantity = item.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "zar",
                        UnitAmount = ToMinorUnits(item.UnitPrice),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"{item.Product.Name} ({order.Supplier.CompanyName})"
                        }
                    }
                });
            }

            if (order.DeliveryFee > 0)
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "zar",
                        UnitAmount = ToMinorUnits(order.DeliveryFee),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Delivery for {order.OrderNumber}"
                        }
                    }
                });
            }

            if (order.PlatformCommission > 0)
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "zar",
                        UnitAmount = ToMinorUnits(order.PlatformCommission),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"SpazaSure platform fee for {order.OrderNumber}"
                        }
                    }
                });
            }
        }

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
            SuccessUrl = AddPaymentResult(_successUrl, "success"),
            CancelUrl = AddPaymentResult(_cancelUrl, "cancelled"),
            Metadata = new Dictionary<string, string>
            {
                ["payment_type"] = "shop_orders",
                ["checkout_id"] = checkoutId.ToString()
            },
            LineItems = lineItems
        };

        return await new SessionService().CreateAsync(options, cancellationToken: cancellationToken);
    }

    private static long ToMinorUnits(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    private static string AddPaymentResult(string url, string result) =>
        $"{url}{(url.Contains('?') ? '&' : '?')}payment={result}";
}
