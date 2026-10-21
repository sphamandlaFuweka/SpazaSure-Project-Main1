using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.OrderService.Services;

public sealed class OrderPaystackPaymentService(HttpClient httpClient, IConfiguration config)
{
    private readonly string _secretKey = config["Paystack:SecretKey"] ?? string.Empty;
    private readonly string _callbackUrl =
        config["Paystack:CallbackUrls:ShopCheckout"] ?? string.Empty;

    /// <summary>
    /// Asks Paystack whether a checkout was paid, for when the webhook was missed or is late.
    /// Returns the amount paid in cents, or null when it wasn't paid (or Paystack can't say).
    /// </summary>
    public async Task<long?> PaidAmountAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_secretKey)) return null;
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"https://api.paystack.co/transaction/verify/{Uri.EscapeDataString(reference)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return null;
        var paid = data.TryGetProperty("status", out var status) && status.GetString() == "success";
        var sameReference = data.TryGetProperty("reference", out var r) && r.GetString() == reference;
        var rand = data.TryGetProperty("currency", out var c) && string.Equals(c.GetString(), "ZAR", StringComparison.OrdinalIgnoreCase);
        return paid && sameReference && rand && data.TryGetProperty("amount", out var amount) && amount.TryGetInt64(out var cents)
            ? cents
            : null;
    }

    /// <summary>
    /// If Paystack confirms the checkout behind this reference was paid in full, marks its orders
    /// paid (and notifies, as the webhook would). Returns true when they are paid.
    /// </summary>
    public async Task<bool> ConfirmOrdersAsync(
        SpazaSure.Infrastructure.Data.SpazaSureDbContext db, SpazaSure.Shared.Helpers.EventPublisher events,
        string reference, CancellationToken cancellationToken = default)
    {
        var orders = await SpazaSure.Infrastructure.Data.OrderPayments.ForReferenceAsync(db, reference, cancellationToken);
        if (orders.Count == 0) return false;
        if (orders.All(o => o.PaymentStatus == "paid")) return true;
        if (orders.Any(o => o.PaymentStatus != "pending" && o.PaymentStatus != "paid")) return false;
        long? paid;
        try { paid = await PaidAmountAsync(reference, cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException) { return false; }
        if (paid is not { } cents || !SpazaSure.Infrastructure.Data.OrderPayments.CoversOrders(cents, orders)) return false;
        await SpazaSure.Infrastructure.Data.OrderPayments.MarkPaidAsync(db, events, orders, cancellationToken);
        return true;
    }

    public async Task<OrderPaystackCheckout> CreateCheckoutAsync(
        IReadOnlyCollection<Order> orders,
        string customerEmail,
        string reference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
            throw new InvalidOperationException("Paystack is not configured for order checkout.");
        if (string.IsNullOrWhiteSpace(customerEmail))
            throw new InvalidOperationException("An email address is required to pay for this order.");
        if (!Uri.TryCreate(_callbackUrl, UriKind.Absolute, out var callbackUri) ||
            (callbackUri.Scheme != Uri.UriSchemeHttps && !callbackUri.IsLoopback))
            throw new InvalidOperationException("Paystack order callback URL must use HTTPS.");

        var amount = orders.Sum(order => order.TotalAmount);
        if (amount <= 0m)
            throw new InvalidOperationException("Order total must be greater than zero.");
        var amountInMinorUnits = decimal.ToInt64(
            decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://api.paystack.co/transaction/initialize")
        {
            Content = JsonContent.Create(new
            {
                email = customerEmail,
                amount = amountInMinorUnits,
                currency = "ZAR",
                reference,
                callback_url = _callbackUrl
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.True)
        {
            var message = root.TryGetProperty("message", out var apiMessage)
                ? apiMessage.GetString()
                : null;
            throw new InvalidOperationException(
                $"Paystack could not initialize order checkout: {message ?? "unknown API response"}");
        }

        var data = root.GetProperty("data");
        var authorizationUrl = data.GetProperty("authorization_url").GetString();
        var returnedReference = data.GetProperty("reference").GetString();
        if (string.IsNullOrWhiteSpace(authorizationUrl) ||
            string.IsNullOrWhiteSpace(returnedReference) ||
            !string.Equals(returnedReference, reference, StringComparison.Ordinal))
            throw new InvalidOperationException("Paystack returned an invalid order checkout response.");

        return new OrderPaystackCheckout(authorizationUrl, returnedReference);
    }
}

public sealed record OrderPaystackCheckout(string AuthorizationUrl, string Reference);
