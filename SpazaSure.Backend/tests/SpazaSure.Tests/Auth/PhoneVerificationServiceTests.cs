using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Auth;

public class PhoneVerificationServiceTests
{
    [Fact]
    public async Task AfricaTalking_sender_posts_sms_and_accepts_a_numeric_success_status()
    {
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(
                "https://api.africastalking.com/version1/messaging",
                request.RequestUri?.ToString());
            Assert.Equal("test-api-key", request.Headers.GetValues("apiKey").Single());
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("username=test-user", form);
            Assert.Contains("to=%2B27820000001", form);
            Assert.Contains("message=verification+message", form);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"SMSMessageData":{"Recipients":[{"statusCode":101}]}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var config = ConfigHelper.Build(new Dictionary<string, string?>
        {
            ["AfricaTalking:Username"] = "test-user",
            ["AfricaTalking:ApiKey"] = "test-api-key"
        });
        var sender = new AfricaTalkingSmsSender(new HttpClient(handler), config);

        await sender.SendAsync("+27820000001", "verification message");
    }

    [Fact]
    public async Task AfricaTalking_sender_rejects_unsuccessful_recipient_status()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"SMSMessageData":{"Recipients":[{"statusCode":406}]}}""")
            }));
        var config = ConfigHelper.Build(new Dictionary<string, string?>
        {
            ["AfricaTalking:Username"] = "test-user",
            ["AfricaTalking:ApiKey"] = "test-api-key"
        });
        var sender = new AfricaTalkingSmsSender(new HttpClient(handler), config);

        await Assert.ThrowsAsync<SmsDeliveryException>(
            () => sender.SendAsync("+27820000001", "verification message"));
    }

    [Fact]
    public async Task Disabled_phone_verification_does_not_send_sms()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var sender = new TestSmsSender();
        var service = new PhoneVerificationService(
            db, ConfigHelper.Build(), sender, NullLogger<PhoneVerificationService>.Instance);

        var result = await service.SendCodeAsync("082 000 0001");

        Assert.False(service.IsEnabled);
        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Registration_code_is_hashed_and_can_only_be_used_once()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var sender = new TestSmsSender();
        var service = CreateEnabledService(db, sender);

        var sent = await service.SendCodeAsync("082 000 0002");

        Assert.True(sent.Success);
        Assert.NotEmpty(sender.Messages);
        var code = Regex.Match(sender.Messages.Single(), @"\b\d{6}\b").Value;
        var storedCode = Assert.Single(db.OtpCodes).Code;
        Assert.NotEqual(code, storedCode);

        var verified = await service.ConsumeRegistrationCodeAsync("+27820000002", code);
        Assert.True(verified.Success);
        await db.SaveChangesAsync();

        var replay = await service.ConsumeRegistrationCodeAsync("+27820000002", code);
        Assert.False(replay.Success);
    }

    [Fact]
    public async Task Both_public_registration_types_require_a_code_when_enabled()
    {
        using var shopDb = DbContextHelper.CreateInMemoryDb();
        var shopSender = new TestSmsSender();
        var shopService = CreateEnabledService(shopDb, shopSender);
        var shopAuth = new ShopAuthService(shopDb, EnabledConfig(), shopService);
        var shopRequest = new ShopRegisterRequest(
            "+27820000003",
            "StrongPassword123",
            "Test Owner",
            "Test Shop",
            "1 Main Street",
            "8001015009087",
            AcceptedPrivacyNotice: true);
        var shopResult = await shopAuth.RegisterAsync(shopRequest, "127.0.0.1");

        Assert.False(shopResult.Success);
        Assert.Empty(shopDb.Users);
        await shopService.SendCodeAsync(shopRequest.Phone);
        var shopCode = Regex.Match(shopSender.Messages.Single(), @"\b\d{6}\b").Value;
        var registeredShop = await shopAuth.RegisterAsync(
            shopRequest with { PhoneVerificationCode = shopCode }, "127.0.0.1");
        Assert.True(registeredShop.Success);
        Assert.Equal("+27820000003", Assert.Single(shopDb.Users).Phone);
        Assert.NotNull(Assert.Single(shopDb.OtpCodes).UsedAt);

        using var customerDb = DbContextHelper.CreateInMemoryDb();
        customerDb.Roles.Add(new Role { Name = "customer", IsActive = true });
        await customerDb.SaveChangesAsync();
        var customerSender = new TestSmsSender();
        var customerService = CreateEnabledService(customerDb, customerSender);
        var customerAuth = new CustomerAuthService(customerDb, EnabledConfig(), customerService);
        var customerRequest = new CustomerRegisterRequest(
            "+27820000004",
            "Test",
            "Customer",
            "StrongPassword123",
            null,
            [],
            AcceptedPrivacyNotice: true);
        var customerResult = await customerAuth.RegisterAsync(customerRequest, "127.0.0.1");

        Assert.False(customerResult.Success);
        Assert.Empty(customerDb.Users);
        await customerService.SendCodeAsync(customerRequest.Phone);
        var customerCode = Regex.Match(customerSender.Messages.Single(), @"\b\d{6}\b").Value;
        var registeredCustomer = await customerAuth.RegisterAsync(
            customerRequest with { PhoneVerificationCode = customerCode }, "127.0.0.1");
        Assert.True(registeredCustomer.Success);
        Assert.Equal("+27820000004", Assert.Single(customerDb.Users).Phone);
        Assert.NotNull(Assert.Single(customerDb.OtpCodes).UsedAt);
    }

    private static PhoneVerificationService CreateEnabledService(
        SpazaSure.Infrastructure.Data.SpazaSureDbContext db,
        TestSmsSender sender) =>
        new(db, EnabledConfig(), sender, NullLogger<PhoneVerificationService>.Instance);

    private static IConfiguration EnabledConfig() =>
        ConfigHelper.Build(new Dictionary<string, string?>
        {
            ["PhoneVerification:Enabled"] = "true",
            ["PhoneVerification:HashKey"] = "test-hash-key-with-at-least-32-characters",
            ["AfricaTalking:Username"] = "test",
            ["AfricaTalking:ApiKey"] = "test"
        });

    private sealed class TestSmsSender : ISmsSender
    {
        public List<string> Messages { get; } = [];

        public Task SendAsync(
            string phone, string message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            responseFactory(request, cancellationToken);
    }
}
