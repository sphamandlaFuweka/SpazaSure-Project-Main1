using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SpazaSure.ComplianceService.Services;
using Xunit;

namespace SpazaSure.Tests.Features;

public class DiditServiceTests
{
    private const string ApiKey = "didit_test_key";
    private const string WebhookSecret = "didit_webhook_secret";
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static DiditService Create(HttpMessageHandler? handler = null, string? webhookSecret = WebhookSecret)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Didit:ApiKey"] = ApiKey,
            ["Didit:WebhookSecret"] = webhookSecret,
        }).Build();
        return new DiditService(new HttpClient(handler ?? new TestHttpMessageHandler(_ => throw new InvalidOperationException())), config);
    }

    private static string Sign(byte[] payload, string secret = WebhookSecret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload)).ToLowerInvariant();

    [Fact]
    public void Webhook_SignedWithTheSecretAndRecent_IsAccepted()
    {
        var payload = Encoding.UTF8.GetBytes("""{"session_id":"abc","status":"Approved"}""");

        Create().IsValidWebhook(payload, Sign(payload), Now.AddSeconds(-30).ToUnixTimeSeconds().ToString(), Now)
            .Should().BeTrue();
    }

    [Fact]
    public void Webhook_WithWrongSignatureOrChangedBody_IsRejected()
    {
        var payload = Encoding.UTF8.GetBytes("""{"session_id":"abc","status":"Approved"}""");
        var tampered = Encoding.UTF8.GetBytes("""{"session_id":"abc","status":"Declined"}""");
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var service = Create();

        service.IsValidWebhook(payload, Sign(payload, "another_secret"), timestamp, Now).Should().BeFalse();
        service.IsValidWebhook(tampered, Sign(payload), timestamp, Now).Should().BeFalse();
        service.IsValidWebhook(payload, "not-hex", timestamp, Now).Should().BeFalse();
        service.IsValidWebhook(payload, null, timestamp, Now).Should().BeFalse();
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void Webhook_OlderOrNewerThanFiveMinutes_IsRejected(int offsetSeconds)
    {
        var payload = Encoding.UTF8.GetBytes("{}");

        Create().IsValidWebhook(payload, Sign(payload), Now.AddSeconds(offsetSeconds).ToUnixTimeSeconds().ToString(), Now)
            .Should().BeFalse();
    }

    [Fact]
    public void WithoutAWebhookSecret_NothingIsAccepted()
    {
        var payload = Encoding.UTF8.GetBytes("{}");
        var service = Create(webhookSecret: null);

        service.IsConfigured.Should().BeFalse();
        service.IsValidWebhook(payload, Sign(payload, ""), Now.ToUnixTimeSeconds().ToString(), Now).Should().BeFalse();
    }

    [Fact]
    public void ExtractNotes_CollectsEachCheckWarningOnce()
    {
        using var decision = JsonDocument.Parse("""
        {
          "status": "In Review",
          "id_verifications": [{ "status": "Approved", "warnings": [{ "short_description": "QR not detected" }] }],
          "face_matches": [{ "status": "In Review", "warnings": [{ "short_description": "Low face match similarity" }, { "short_description": "QR not detected" }] }],
          "aml_screenings": null,
          "reviews": []
        }
        """);

        DiditService.ExtractNotes(decision.RootElement).Should().Be("QR not detected\nLow face match similarity");
    }

    [Fact]
    public void ExtractNotes_WithoutWarnings_IsNull()
    {
        using var decision = JsonDocument.Parse("""{ "status": "Approved", "id_verifications": [{ "status": "Approved", "warnings": [] }] }""");

        DiditService.ExtractNotes(decision.RootElement).Should().BeNull();
    }

    [Fact]
    public async Task CreateSession_SendsTheWorkflowAndApiKey_AndReturnsTheHostedLink()
    {
        HttpRequestMessage? sent = null;
        string? sentBody = null;
        var service = Create(new TestHttpMessageHandler(async request =>
        {
            sent = request;
            sentBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"session_id":"s-1","url":"https://verify.didit.me/session/s-1","status":"Not Started"}""",
                    Encoding.UTF8, "application/json"),
            };
        }));

        var session = await service.CreateSessionAsync("wf-1", "shop:42", null, new { id_country = "ZAF" });

        session.Should().Be(new DiditService.Session("s-1", "https://verify.didit.me/session/s-1", "Not Started"));
        sent!.RequestUri!.ToString().Should().Be("https://verification.didit.me/v3/session/");
        sent.Headers.GetValues("x-api-key").Should().Equal(ApiKey);
        sentBody.Should().Contain("\"workflow_id\":\"wf-1\"").And.Contain("\"vendor_data\":\"shop:42\"").And.Contain("\"id_country\":\"ZAF\"");
    }

    private sealed class TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request);
    }
}
