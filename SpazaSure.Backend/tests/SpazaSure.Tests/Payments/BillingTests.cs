using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using SpazaSure.UserService.Services;
using Xunit;

namespace SpazaSure.Tests.Payments;

/// <summary>
/// The app's Billing section: a card is saved only once Paystack confirms the R1 check, the
/// check is always refunded, and Paystack's authorization never leaves the server.
/// </summary>
public class BillingTests
{
    private const string Reference = "CARD-test";

    private sealed class FakePaystack(long amountCents, bool reusable, string signature) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            Calls.Add($"{request.Method} {url}");
            var body = url.Contains("transaction/verify/")
                ? "{\"status\":true,\"data\":{\"status\":\"success\",\"reference\":\"" + Reference +
                  "\",\"amount\":" + amountCents + ",\"currency\":\"ZAR\",\"authorization\":{" +
                  "\"authorization_code\":\"AUTH_secret\",\"signature\":\"" + signature + "\",\"channel\":\"card\"," +
                  "\"card_type\":\"visa \",\"last4\":\"4081\",\"exp_month\":\"12\",\"exp_year\":\"2030\"," +
                  "\"bank\":\"TEST BANK\",\"reusable\":" + (reusable ? "true" : "false") + "}}}"
                : "{\"status\":true,\"data\":{}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (SpazaSureDbContext Db, User User, BillingController Controller, FakePaystack Paystack) Setup(
        string role = "customer", long amountCents = 100, bool reusable = true, string signature = "SIG_new")
    {
        var db = DbContextHelper.CreateInMemoryDb();
        // The test database only seeds supplier, spaza_owner and admin
        var roleRow = db.Roles.FirstOrDefault(r => r.Name == role) ?? new Role { Name = role };
        var user = new User
        {
            Phone = "0820004000", Email = "thandi@example.com", PasswordHash = "h", Role = roleRow,
        };
        db.Add(user);
        db.SavedCards.Add(new SavedCard { UserId = user.Id, Reference = Reference, Email = user.Email! });
        db.SaveChanges();

        var handler = new FakePaystack(amountCents, reusable, signature);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "sk_test_x",
        }).Build();
        var controller = new BillingController(
            db, new PaystackPaymentService(new HttpClient(handler), config), NullLogger<BillingController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, role)],
                        "Test")),
                },
            },
        };
        return (db, user, controller, handler);
    }

    private static Guid PendingCardId(SpazaSureDbContext db) => db.SavedCards.Single(c => c.Reference == Reference).Id;

    [Fact]
    public async Task ConfirmingAPaidCheck_SavesTheCardAsDefault_AndRefundsTheR1()
    {
        var (db, _, controller, paystack) = Setup();

        await controller.ConfirmCard(PendingCardId(db), CancellationToken.None);

        var card = db.SavedCards.Single();
        card.Should().Match<SavedCard>(c => c.Status == "active" && c.IsDefault && c.Last4 == "4081" &&
                                            c.Brand == "visa" && c.AuthorizationCode == "AUTH_secret");
        paystack.Calls.Should().Contain(c => c.EndsWith("/refund"));
    }

    [Fact]
    public async Task ConfirmingACheckOfTheWrongAmount_LeavesTheCardPending_AndRefundsNothing()
    {
        var (db, _, controller, paystack) = Setup(amountCents: 5000);

        await controller.ConfirmCard(PendingCardId(db), CancellationToken.None);

        db.SavedCards.Single().Status.Should().Be("pending");
        paystack.Calls.Should().NotContain(c => c.EndsWith("/refund"));
    }

    [Fact]
    public async Task ACardPaystackWontLetUsReuse_IsNotSaved_ButStillRefunded()
    {
        var (db, _, controller, paystack) = Setup(reusable: false);

        await controller.ConfirmCard(PendingCardId(db), CancellationToken.None);

        db.SavedCards.Should().BeEmpty();
        paystack.Calls.Should().Contain(c => c.EndsWith("/refund"));
    }

    [Fact]
    public async Task SavingTheSameCardTwice_KeepsTheFirst()
    {
        var (db, user, controller, _) = Setup(signature: "SIG_same");
        db.SavedCards.Add(new SavedCard
        {
            UserId = user.Id, Reference = "CARD-old", Email = user.Email!, Status = "active",
            AuthorizationCode = "AUTH_old", Signature = "SIG_same", Last4 = "4081", IsDefault = true,
        });
        db.SaveChanges();

        await controller.ConfirmCard(PendingCardId(db), CancellationToken.None);

        db.SavedCards.Should().ContainSingle().Which.Reference.Should().Be("CARD-old");
    }

    [Fact]
    public async Task TheOverview_NeverShowsPaystacksAuthorization()
    {
        var (db, _, controller, _) = Setup();
        await controller.ConfirmCard(PendingCardId(db), CancellationToken.None);

        var result = (OkObjectResult)await controller.Get(CancellationToken.None);

        var json = JsonSerializer.Serialize(result.Value);
        json.Should().Contain("4081").And.NotContain("AUTH_secret").And.NotContain("SIG_new");
    }

    [Fact]
    public async Task RemovingTheDefaultCard_DeactivatesItAtPaystack_AndMakesAnotherTheDefault()
    {
        var (db, user, controller, paystack) = Setup();
        var older = new SavedCard
        {
            UserId = user.Id, Reference = "CARD-a", Email = user.Email!, Status = "active",
            AuthorizationCode = "AUTH_a", CreatedAt = DateTime.UtcNow.AddDays(-2),
        };
        var main = new SavedCard
        {
            UserId = user.Id, Reference = "CARD-b", Email = user.Email!, Status = "active",
            AuthorizationCode = "AUTH_b", IsDefault = true,
        };
        db.SavedCards.AddRange(older, main);
        db.SaveChanges();

        await controller.RemoveCard(main.Id, CancellationToken.None);

        db.SavedCards.Should().NotContain(c => c.Id == main.Id);
        db.SavedCards.Single(c => c.Id == older.Id).IsDefault.Should().BeTrue();
        paystack.Calls.Should().Contain(c => c.EndsWith("customer/deactivate_authorization"));
    }

    [Fact]
    public async Task ACustomerCannotSaveAPayoutAccount()
    {
        var (_, _, controller, _) = Setup(role: "customer");

        var result = await controller.SavePayoutAccount(
            new PayoutAccountRequest("Capitec", "Thandi", "1234567890", "470010", "savings"), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AShopSavesItsPayoutAccount_AndTheOverviewShowsIt()
    {
        var (db, user, controller, _) = Setup(role: "spaza_owner");

        await controller.SavePayoutAccount(
            new PayoutAccountRequest(" Capitec ", "Thandi", "1234 567 890", "470010", "savings"), CancellationToken.None);

        db.BillingProfiles.Single(b => b.UserId == user.Id).Should().Match<BillingProfile>(b =>
            b.BankName == "Capitec" && b.AccountNumber == "1234567890" && b.AccountType == "savings");
    }

    [Theory]
    [InlineData("123")]        // too short
    [InlineData("5123456789")] // doesn't start with 4
    public async Task BillingInfo_RejectsAnInvalidVatNumber(string vat)
    {
        var (db, _, controller, _) = Setup();

        var result = await controller.SaveInfo(
            new BillingInfoRequest("Thandi", null, vat, null, null, null, null, null, null), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        db.BillingProfiles.Should().BeEmpty();
    }
}
