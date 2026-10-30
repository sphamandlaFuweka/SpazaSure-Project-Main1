using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using SpazaSure.UserService.Services;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>
/// A shop paid its onboarding fee but Paystack's webhook was late or never came: the fee must
/// still count as paid when the app checks, by asking Paystack directly.
/// </summary>
public class OnboardingFeePaymentTests
{
    private const string Reference = "SS-test-reference";

    private static PaystackPaymentService Paystack(string? status, long amountCents = 15000)
    {
        var handler = new FakePaystack(request => status is null || !request.RequestUri!.ToString().Contains(Reference)
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"status\":true,\"data\":{\"status\":\"" + status + "\",\"reference\":\"" + Reference +
                    "\",\"amount\":" + amountCents + ",\"currency\":\"ZAR\"}}",
                    Encoding.UTF8, "application/json"),
            });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "sk_test_x",
        }).Build();
        return new PaystackPaymentService(new HttpClient(handler), config);
    }

    private static (SpazaSureDbContext Db, SpazaShop Shop, ShopOnboardingFeeController Controller) Setup(
        string paymentStatus, PaystackPaymentService paystack)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "spaza_owner");
        var user = new User { Phone = "0820003000", PasswordHash = "h", RoleId = role.Id };
        var shop = new SpazaShop { UserId = user.Id, ShopName = "Gogo's Spaza", OwnerName = "Lindi", Phone = "0820003000", Address = "3 Test St" };
        var payment = new ShopOnboardingPayment
        {
            ShopId = shop.Id, Amount = 150m, Status = paymentStatus, ProviderPaymentId = Reference,
            ExpiresAt = DateTime.UtcNow.AddHours(23),
        };
        db.AddRange(user, shop, payment);
        db.SaveChanges();
        var controller = new ShopOnboardingFeeController(db, paystack)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test")),
                },
            },
        };
        return (db, shop, controller);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("cancelled")] // replaced by a newer checkout after the shop had already paid it
    public async Task CheckingStatus_AfterAPaymentWhoseWebhookIsMissing_MarksTheFeePaid(string paymentStatus)
    {
        var (db, shop, controller) = Setup(paymentStatus, Paystack("success"));

        await controller.GetStatus(CancellationToken.None);

        db.SpazaShops.Single(s => s.Id == shop.Id).Should().Match<SpazaShop>(s => s.OnboardingFeePaid && s.OnboardingFeeRef == Reference);
        db.ShopOnboardingPayments.Single().Status.Should().Be("completed");
    }

    [Fact]
    public async Task OpeningANewCheckout_AfterPayingTheLastOne_DoesNotChargeAgain()
    {
        var (db, shop, controller) = Setup("pending", Paystack("success"));

        await controller.PaystackCheckout(CancellationToken.None);

        db.SpazaShops.Single(s => s.Id == shop.Id).OnboardingFeePaid.Should().BeTrue();
        db.ShopOnboardingPayments.Should().ContainSingle();
    }

    [Theory]
    [InlineData("abandoned", 15000)]
    [InlineData("success", 100)] // wrong amount
    [InlineData(null, 0)]        // Paystack has no such transaction yet
    public async Task CheckingStatus_LeavesTheFeeUnpaid_UnlessPaystackConfirmsTheFullAmount(string? status, long amountCents)
    {
        var (db, shop, controller) = Setup("pending", Paystack(status, amountCents));

        await controller.GetStatus(CancellationToken.None);

        db.SpazaShops.Single(s => s.Id == shop.Id).OnboardingFeePaid.Should().BeFalse();
        db.ShopOnboardingPayments.Single().Status.Should().Be("pending");
    }

    private sealed class FakePaystack(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }
}
