using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SpazaSure.UserService.Services;
using Xunit;

namespace SpazaSure.Tests.Payments;

public class PaystackPaymentServiceTests
{
    private const string SecretKey = "sk_test_unit_test_key";
    private const string CallbackUrl = "https://app.example.test/?payment=success";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingSecretKey_DisablesCheckoutAndRejectsWebhooks(string? secretKey)
    {
        var requestSent = false;
        using var client = new HttpClient(new TestHttpMessageHandler((_, _) =>
        {
            requestSent = true;
            return Task.FromResult(JsonResponse("{}"));
        }));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Paystack:SecretKey"] = secretKey
            })
            .Build();
        var service = new PaystackPaymentService(client, configuration);
        var payload = Encoding.UTF8.GetBytes("""{"event":"charge.success"}""");
        var signature = Convert.ToHexString(HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(secretKey ?? string.Empty), payload));

        Assert.False(service.IsConfigured);
        Assert.False(service.IsValidWebhookSignature(payload, signature));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOnboardingCheckoutAsync(
                150m, "owner@example.test", "SS-test", CallbackUrl));
        var orderService = new SpazaSure.OrderService.Services.OrderPaystackPaymentService(
            client, configuration);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orderService.CreateCheckoutAsync([], "owner@example.test", "SS-test", "SPL-test"));
        Assert.False(requestSent);
    }

    [Fact]
    public async Task CreateOnboardingCheckoutAsync_InitializesInZarMinorUnits()
    {
        HttpRequestMessage? capturedRequest = null;
        JsonDocument? capturedBody = null;
        using var client = new HttpClient(new TestHttpMessageHandler(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            return JsonResponse("""
                {"status":true,"data":{"authorization_url":"https://checkout.paystack.com/test","reference":"SS-test"}}
                """);
        }));
        var service = CreateService(client);

        var checkout = await service.CreateOnboardingCheckoutAsync(
            150m, "owner@example.test", "SS-test", CallbackUrl);

        Assert.Equal("https://checkout.paystack.com/test", checkout.AuthorizationUrl);
        Assert.Equal("SS-test", checkout.Reference);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal(SecretKey, capturedRequest.Headers.Authorization?.Parameter);
        Assert.NotNull(capturedBody);
        Assert.Equal(15000, capturedBody.RootElement.GetProperty("amount").GetInt32());
        Assert.Equal("ZAR", capturedBody.RootElement.GetProperty("currency").GetString());
        Assert.Equal(CallbackUrl, capturedBody.RootElement.GetProperty("callback_url").GetString());
        capturedBody.Dispose();
    }

    [Fact]
    public async Task CreateSubscriptionCheckoutAsync_UsesValidatedPlanWithoutOverridingPlanPrice()
    {
        var requests = new List<(string Url, string? Body)>();
        using var client = new HttpClient(new TestHttpMessageHandler(async (request, _) =>
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync();
            requests.Add((request.RequestUri!.ToString(), body));
            return request.RequestUri!.AbsolutePath.EndsWith("/plan/PLN-monthly", StringComparison.Ordinal)
                ? JsonResponse("""
                    {"status":true,"data":{"plan_code":"PLN-monthly","currency":"ZAR","interval":"monthly","amount":1999}}
                    """)
                : JsonResponse("""
                    {"status":true,"data":{"authorization_url":"https://checkout.paystack.com/test","reference":"SS-sub"}}
                    """);
        }));
        var service = CreateService(client);

        await service.CreateSubscriptionCheckoutAsync(
            19.99m, "customer@example.test", "SS-sub", CallbackUrl, "PLN-monthly", "monthly");

        Assert.Equal(2, requests.Count);
        using var initializeBody = JsonDocument.Parse(requests[1].Body!);
        Assert.Equal("PLN-monthly", initializeBody.RootElement.GetProperty("plan").GetString());
        Assert.False(initializeBody.RootElement.TryGetProperty("amount", out _));
        Assert.False(initializeBody.RootElement.TryGetProperty("currency", out _));
    }

    [Fact]
    public async Task CreateSubscriptionCheckoutAsync_RejectsMismatchedPlanBeforeInitializing()
    {
        var initializeRequested = false;
        using var client = new HttpClient(new TestHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/plan/PLN-monthly", StringComparison.Ordinal))
                return Task.FromResult(JsonResponse("""
                    {"status":true,"data":{"plan_code":"PLN-monthly","currency":"ZAR","interval":"annually","amount":1999}}
                    """));
            initializeRequested = true;
            return Task.FromResult(JsonResponse("{}"));
        }));
        var service = CreateService(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSubscriptionCheckoutAsync(
                19.99m, "customer@example.test", "SS-sub", CallbackUrl, "PLN-monthly", "monthly"));

        Assert.False(initializeRequested);
    }

    [Fact]
    public void IsValidWebhookSignature_UsesPaystackHmacSha512()
    {
        using var client = new HttpClient(new TestHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse("{}"))));
        var service = CreateService(client);
        var payload = Encoding.UTF8.GetBytes("""{"event":"charge.success"}""");
        var signature = Convert.ToHexString(HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(SecretKey),
            payload));

        Assert.True(service.IsValidWebhookSignature(payload, signature));
        Assert.False(service.IsValidWebhookSignature(payload, "not-a-signature"));
        Assert.False(service.IsValidWebhookSignature(payload, Convert.ToHexString(new byte[64])));
    }

    [Fact]
    public async Task VerifyTransactionAsync_ReturnsVerifiedZarTransaction()
    {
        using var client = new HttpClient(new TestHttpMessageHandler((request, _) =>
        {
            Assert.Equal(
                "https://api.paystack.co/transaction/verify/SS-test",
                request.RequestUri!.ToString());
            return Task.FromResult(JsonResponse("""
                {"status":true,"data":{"status":"success","amount":15000,"currency":"ZAR","reference":"SS-test"}}
                """));
        }));
        var service = CreateService(client);

        var transaction = await service.VerifyTransactionAsync("SS-test");

        Assert.True(transaction.IsSuccessful);
        Assert.Equal(15000, transaction.Amount);
        Assert.Equal("ZAR", transaction.Currency);
        Assert.Equal("SS-test", transaction.Reference);
    }

    private static PaystackPaymentService CreateService(HttpClient client)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Paystack:SecretKey"] = SecretKey,
                ["Paystack:CallbackUrls:ShopCheckout"] = CallbackUrl
            })
            .Build();
        return new PaystackPaymentService(client, configuration);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class TestHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
