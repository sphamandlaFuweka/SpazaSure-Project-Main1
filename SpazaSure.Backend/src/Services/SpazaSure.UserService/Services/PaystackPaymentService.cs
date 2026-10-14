using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;

namespace SpazaSure.UserService.Services;

public sealed class PaystackPaymentService(HttpClient httpClient, IConfiguration config)
{
    private const string Currency = "ZAR";
    private const string ApiBaseUrl = "https://api.paystack.co/";
    private readonly string _secretKey = config["Paystack:SecretKey"] ?? string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_secretKey);

    public string GetPlanCode(string planKey) =>
        config[$"Paystack:PlanCodes:{planKey}"] ?? string.Empty;

    public string GetCallbackUrl(string callbackKey) =>
        config[$"Paystack:CallbackUrls:{callbackKey}"] ?? string.Empty;

    public async Task<PaystackCheckout> CreateOnboardingCheckoutAsync(
        decimal amount,
        string email,
        string reference,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        return await InitializeTransactionAsync(
            amount, email, reference, callbackUrl, null, cancellationToken);
    }

    public async Task<PaystackCheckout> CreateSubscriptionCheckoutAsync(
        decimal amount,
        string email,
        string reference,
        string callbackUrl,
        string planCode,
        string expectedInterval,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planCode))
            throw new InvalidOperationException("The Paystack plan code is not configured.");

        var plan = await GetPlanAsync(planCode, cancellationToken);
        var expectedAmount = decimal.ToInt64(decimal.Round(
            amount * 100m, 0, MidpointRounding.AwayFromZero));
        if (plan.Amount != expectedAmount ||
            !string.Equals(plan.Currency, Currency, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(plan.Interval, expectedInterval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Configured Paystack plan {planCode} must be {Currency} {amount:F2} with a {expectedInterval} billing interval.");
        }

        return await InitializeTransactionAsync(
            amount, email, reference, callbackUrl, planCode, cancellationToken);
    }

    public async Task<PaystackPlan> GetPlanAsync(
        string planCode,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var document = await SendForJsonAsync(
            HttpMethod.Get,
            $"plan/{Uri.EscapeDataString(planCode)}",
            null,
            cancellationToken);
        var data = document.RootElement.GetProperty("data");
        return new PaystackPlan(
            data.GetProperty("plan_code").GetString() ?? string.Empty,
            data.GetProperty("currency").GetString() ?? string.Empty,
            data.GetProperty("interval").GetString() ?? string.Empty,
            data.GetProperty("amount").GetInt64());
    }

    public async Task DisableSubscriptionAsync(
        string subscriptionCode,
        string emailToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionCode) ||
            string.IsNullOrWhiteSpace(emailToken))
            throw new ArgumentException("Paystack subscription code and email token are required.");

        using var document = await SendForJsonAsync(
            HttpMethod.Post,
            "subscription/disable",
            new { code = subscriptionCode, token = emailToken },
            cancellationToken);
        if (!document.RootElement.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.True)
            throw new InvalidOperationException("Paystack could not disable the subscription.");
    }

    public async Task<PaystackTransaction> VerifyTransactionAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        using var document = await SendForJsonAsync(
            HttpMethod.Get,
            $"transaction/verify/{Uri.EscapeDataString(reference)}",
            null,
            cancellationToken);
        var data = document.RootElement.GetProperty("data");
        var customer = data.TryGetProperty("customer", out var customerElement)
            ? customerElement
            : default;
        var authorization = data.TryGetProperty("authorization", out var authorizationElement)
            ? authorizationElement
            : default;
        var plan = data.TryGetProperty("plan_object", out var planElement)
            ? planElement
            : default;
        return new PaystackTransaction(
            string.Equals(
                data.GetProperty("status").GetString(),
                "success",
                StringComparison.OrdinalIgnoreCase),
            data.GetProperty("amount").GetInt64(),
            data.GetProperty("currency").GetString() ?? string.Empty,
            data.GetProperty("reference").GetString() ?? string.Empty,
            GetOptionalString(plan, "plan_code"),
            GetOptionalString(customer, "customer_code"),
            GetOptionalString(customer, "email"),
            GetOptionalString(authorization, "authorization_code"));
    }

    public bool IsValidWebhookSignature(ReadOnlySpan<byte> payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(_secretKey) || string.IsNullOrWhiteSpace(signature))
            return false;

        byte[] suppliedSignature;
        try
        {
            suppliedSignature = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var key = Encoding.UTF8.GetBytes(_secretKey);
        var expectedSignature = HMACSHA512.HashData(key, payload);
        return CryptographicOperations.FixedTimeEquals(expectedSignature, suppliedSignature);
    }

    private async Task<PaystackCheckout> InitializeTransactionAsync(
        decimal amount,
        string email,
        string reference,
        string callbackUrl,
        string? planCode,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ValidateCallbackUrl(callbackUrl);
        if (amount <= 0m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(
                nameof(amount), "Amount must be positive and have at most two decimal places.");
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "A customer email address is required by Paystack.", nameof(email));
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("A payment reference is required.", nameof(reference));

        var amountInMinorUnits = decimal.ToInt64(
            decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        var payload = new Dictionary<string, object>
        {
            ["email"] = email,
            ["reference"] = reference,
            ["callback_url"] = callbackUrl
        };
        if (!string.IsNullOrWhiteSpace(planCode))
        {
            payload["plan"] = planCode;
        }
        else
        {
            payload["amount"] = amountInMinorUnits;
            payload["currency"] = Currency;
        }

        using var document = await SendForJsonAsync(
            HttpMethod.Post, "transaction/initialize", payload, cancellationToken);
        var data = document.RootElement.GetProperty("data");
        var authorizationUrl = data.GetProperty("authorization_url").GetString();
        var returnedReference = data.GetProperty("reference").GetString();
        if (string.IsNullOrWhiteSpace(authorizationUrl) ||
            string.IsNullOrWhiteSpace(returnedReference) ||
            !string.Equals(returnedReference, reference, StringComparison.Ordinal))
            throw new InvalidOperationException("Paystack returned an invalid checkout response.");

        return new PaystackCheckout(authorizationUrl, returnedReference);
    }

    private async Task<JsonDocument> SendForJsonAsync(
        HttpMethod method,
        string relativeUrl,
        object? content,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(method, $"{ApiBaseUrl}{relativeUrl}");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _secretKey);
        if (content is not null)
            request.Content = JsonContent.Create(content);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.True)
        {
            var message = document.RootElement.TryGetProperty("message", out var apiMessage)
                ? apiMessage.GetString()
                : null;
            document.Dispose();
            throw new InvalidOperationException(
                $"Paystack request failed: {message ?? "unknown API response"}");
        }
        return document;
    }

    private static void ValidateCallbackUrl(string callbackUrl)
    {
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback))
            throw new InvalidOperationException(
                "Paystack callback URL must use HTTPS, except for local development.");
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Paystack is not configured.");
    }
}

public sealed record PaystackCheckout(string AuthorizationUrl, string Reference);

public sealed record PaystackPlan(string PlanCode, string Currency, string Interval, long Amount);

public sealed record PaystackTransaction(
    bool IsSuccessful,
    long Amount,
    string Currency,
    string Reference,
    string? PlanCode,
    string? CustomerCode,
    string? CustomerEmail,
    string? AuthorizationCode);
