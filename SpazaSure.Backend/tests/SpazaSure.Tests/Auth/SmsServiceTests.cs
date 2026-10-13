using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.AuthService.Services;
using Xunit;

namespace SpazaSure.Tests.Auth;

public class SmsServiceTests
{
    [Theory]
    [InlineData("082 123 4567", "+27821234567")]
    [InlineData("821234567", "+27821234567")]
    [InlineData("+27 82 123 4567", "+27821234567")]
    [InlineData("0027 82 123 4567", "+27821234567")]
    public async Task SendOtp_normalizes_South_African_numbers_and_omits_unapproved_sender(
        string phone,
        string expectedRecipient)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK,
            """{"SMSMessageData":{"Recipients":[{"status":"Queued"}]}}""");
        var service = CreateService(handler);

        var sent = await service.SendOtpAsync(phone, "123456");

        Assert.True(sent);
        Assert.Equal("https://api.africastalking.com/version1/messaging", handler.RequestUri);
        Assert.Contains($"to={Uri.EscapeDataString(expectedRecipient)}", handler.FormBody);
        Assert.DoesNotContain("from=", handler.FormBody);
    }

    [Fact]
    public async Task SendOtp_includes_sender_only_when_explicitly_configured()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK,
            """{"SMSMessageData":{"Recipients":[{"status":"Sent"}]}}""");
        var service = CreateService(handler, senderId: "ApprovedName");

        var sent = await service.SendOtpAsync("+27821234567", "123456");

        Assert.True(sent);
        Assert.Contains("from=ApprovedName", handler.FormBody);
    }

    [Fact]
    public async Task SendOtp_returns_failure_when_provider_rejects_the_recipient()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK,
            """{"SMSMessageData":{"Recipients":[{"status":"Invalid phone number"}]}}""");
        var service = CreateService(handler);

        var sent = await service.SendOtpAsync("0821234567", "123456");

        Assert.False(sent);
    }

    [Fact]
    public async Task SendOtp_rejects_invalid_phone_without_calling_provider()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK,
            """{"SMSMessageData":{"Recipients":[{"status":"Queued"}]}}""");
        var service = CreateService(handler);

        var sent = await service.SendOtpAsync("12345", "123456");

        Assert.False(sent);
        Assert.Null(handler.RequestUri);
    }

    private static SmsService CreateService(RecordingHandler handler, string? senderId = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AfricasTalking:Username"] = "test-account",
            ["AfricasTalking:ApiKey"] = "test-key",
            ["AfricasTalking:Sandbox"] = "false",
            ["AfricasTalking:SenderId"] = senderId
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new SmsService(config, NullLogger<SmsService>.Instance, new RecordingHttpClientFactory(handler));
    }

    private sealed class RecordingHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class RecordingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string FormBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            FormBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
