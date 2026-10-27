using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.NotificationService.Email;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Storage;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class EmailSequencePlannerTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);

    private static SequenceRecipient Person(double ageDays, double? idleDays = null) => new(
        Guid.NewGuid(), "thandi@test.com", "customer", "Thandi",
        Now.AddDays(-ageDays), idleDays is null ? null : Now.AddDays(-idleDays.Value));

    private static SentEmail Sent(string key, double daysAgo) => new(key, Now.AddDays(-daysAgo));

    [Fact]
    public void NewAccount_GetsTheWelcomeEmail()
    {
        EmailSequencePlanner.Next(Person(0.1, 0), [], Now)!.Key.Should().Be("welcome");
    }

    [Fact]
    public void NeverSendsTwoEmailsWithinADay()
    {
        EmailSequencePlanner.Next(Person(2.1, 0), [Sent("welcome", 0.5)], Now).Should().BeNull();
    }

    [Fact]
    public void FeatureTipsFollowOnDaysTwoFiveAndNine()
    {
        EmailSequencePlanner.Next(Person(2.5, 0), [Sent("welcome", 2.4)], Now)!.Key.Should().Be("education_1");
        EmailSequencePlanner.Next(Person(5.5, 0), [Sent("welcome", 5), Sent("education_1", 3)], Now)!.Key.Should().Be("education_2");
        EmailSequencePlanner.Next(Person(9.5, 0), [Sent("welcome", 9), Sent("education_1", 7), Sent("education_2", 4)], Now)!
            .Key.Should().Be("education_3");
    }

    [Fact]
    public void SwitchingSequencesOn_DoesNotFloodOlderActiveAccounts()
    {
        EmailSequencePlanner.Next(Person(40, idleDays: 1), [], Now).Should().BeNull();
    }

    [Fact]
    public void QuietForAWeek_GetsOneReminderPerQuietSpell()
    {
        var p = Person(30, idleDays: 8);
        var first = EmailSequencePlanner.Next(p, [], Now)!;
        first.Step.Should().Be("inactive_7");
        first.Key.Should().Be($"inactive_7:{p.LastSeenAt:yyyy-MM-dd}");

        EmailSequencePlanner.Next(p, [Sent(first.Key, 4)], Now).Should().BeNull();
    }

    [Fact]
    public void QuietForThreeWeeks_GetsTheFeedbackRequest()
    {
        var p = Person(60, idleDays: 22);
        var spell = $"{p.LastSeenAt:yyyy-MM-dd}";
        EmailSequencePlanner.Next(p, [Sent($"inactive_7:{spell}", 14)], Now)!.Key.Should().Be($"inactive_21:{spell}");
    }

    [Fact]
    public void ComingBackAndGoingQuietAgain_StartsANewSpell()
    {
        var p = Person(90, idleDays: 8);
        EmailSequencePlanner.Next(p, [Sent("inactive_7:2026-08-01", 80), Sent("inactive_21:2026-08-01", 66)], Now)!
            .Key.Should().Be($"inactive_7:{p.LastSeenAt:yyyy-MM-dd}");
    }

    [Fact]
    public void StopsAfterSixtyQuietDays_AndLeavesThreeDaysAfterAnyEmail()
    {
        EmailSequencePlanner.Next(Person(200, idleDays: 61), [], Now).Should().BeNull();
        EmailSequencePlanner.Next(Person(30, idleDays: 10), [Sent("education_3", 2)], Now).Should().BeNull();
    }

    [Theory]
    [InlineData("tester@gmail.com", "", true)]
    [InlineData("tester@gmail.com", "tester@gmail.com, @spazasure.co.za", true)]
    [InlineData("sipho@spazasure.co.za", "tester@gmail.com,@spazasure.co.za", true)]
    [InlineData("someone@gmail.com", "tester@gmail.com,@spazasure.co.za", false)]
    public void OnlyTo_LimitsWhoGetsEmailsWhileTesting(string email, string onlyTo, bool allowed)
    {
        EmailSequencePlanner.AllowedWhileTesting(email, onlyTo).Should().Be(allowed);
    }
}

public class EmailLinksAndTemplatesTests
{
    private static readonly EmailLinks Links = new("test-secret-that-is-long-enough-123", "https://api.spazasure.co.za/");

    [Fact]
    public void Links_OnlyWorkForThePersonAndPurposeTheyWereMadeFor()
    {
        var id = Guid.NewGuid();
        var url = Links.Url(EmailLinks.Unsubscribe, id);
        var token = url.Split("t=")[1];

        url.Should().StartWith($"https://api.spazasure.co.za/api/email/unsubscribe?id={id}&t=");
        Links.IsValid(EmailLinks.Unsubscribe, id, token).Should().BeTrue();
        Links.IsValid(EmailLinks.Unsubscribe, Guid.NewGuid(), token).Should().BeFalse();
        Links.IsValid(EmailLinks.WaitlistConfirm, id, token).Should().BeFalse();
        Links.IsValid(EmailLinks.Unsubscribe, id, token[..^1] + (token[^1] == 'A' ? 'B' : 'A')).Should().BeFalse();
        Links.IsValid(EmailLinks.Unsubscribe, id, null).Should().BeFalse();
    }

    [Fact]
    public void EveryEmail_RendersForEveryRole_WithAnUnsubscribeLink()
    {
        foreach (var step in EmailTemplates.Steps)
        foreach (var role in EmailTemplates.Roles)
        {
            var email = EmailTemplates.Render(step, new EmailContext(role, "Thandi",
                "https://app.spazasure.co.za", "https://portal.spazasure.co.za", "https://api.spazasure.co.za/api/email/unsubscribe?id=x&t=y"));
            email.Subject.Should().NotBeNullOrWhiteSpace($"{step}/{role}");
            email.Html.Should().Contain("Hi Thandi,").And.Contain("Unsubscribe");
            email.Text.Should().Contain("Unsubscribe: https://api.spazasure.co.za");
            email.Html.Should().Contain(role == "supplier" ? "portal.spazasure.co.za" : "app.spazasure.co.za");
        }
    }

    [Fact]
    public void Names_AreEscaped_SoTheyCantInjectHtml()
    {
        var email = EmailTemplates.Render("welcome", new EmailContext("customer", "<script>x</script>",
            "https://app", "https://portal", null));

        email.Html.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
    }
}

public class EmailSequenceWorkerTests
{
    private static readonly DateTime Morning = new(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc); // 10:00 in South Africa

    private sealed class FakeSender : IEmailSender
    {
        public bool IsConfigured => true;
        public List<(string To, string Subject, IReadOnlyDictionary<string, string>? Headers)> Sent { get; } = [];

        public Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            Sent.Add((to, subject, headers));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedOptions(EmailSequenceOptions value) : IOptionsMonitor<EmailSequenceOptions>
    {
        public EmailSequenceOptions CurrentValue => value;
        public EmailSequenceOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<EmailSequenceOptions, string?> listener) => null;
    }

    private static (EmailSequenceWorker Worker, FakeSender Sender, Func<SpazaSureDbContext> Db) Create(EmailSequenceOptions options)
    {
        var name = Guid.NewGuid().ToString();
        var seed = DbContextHelper.CreateInMemoryDb(name);
        seed.Roles.Add(new Role { Name = "customer", IsActive = true });
        seed.SaveChanges();

        var services = new ServiceCollection()
            .AddDbContext<SpazaSureDbContext>(o => o.UseInMemoryDatabase(name))
            .BuildServiceProvider();
        var sender = new FakeSender();
        var worker = new EmailSequenceWorker(services.GetRequiredService<IServiceScopeFactory>(), sender,
            new FixedOptions(options), new EmailLinks("secret-secret-secret-secret-1234", "https://api.spazasure.co.za"),
            NullLogger<EmailSequenceWorker>.Instance);
        return (worker, sender, () => DbContextHelper.CreateInMemoryDb(name));
    }

    private static User AddCustomer(SpazaSureDbContext db, string email, DateTime created, string first = "Thandi")
    {
        var role = db.Roles.First(r => r.Name == "customer");
        var user = new User { Email = email, Phone = "08" + Random.Shared.Next(10000000, 99999999), RoleId = role.Id, Status = "active", CreatedAt = created };
        db.Users.Add(user);
        db.CustomerProfiles.Add(new CustomerProfile { UserId = user.Id, FirstName = first, LastName = "Zulu" });
        db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task SendsTheWelcomeOnce_WithOneClickUnsubscribe()
    {
        var (worker, sender, db) = Create(new EmailSequenceOptions { Enabled = true });
        AddCustomer(db(), "thandi@test.com", Morning.AddHours(-3));

        await worker.RunOnceAsync(Morning, CancellationToken.None);
        await worker.RunOnceAsync(Morning.AddMinutes(15), CancellationToken.None);

        sender.Sent.Should().ContainSingle();
        sender.Sent[0].To.Should().Be("thandi@test.com");
        sender.Sent[0].Subject.Should().Be("Welcome to SpazaSure");
        sender.Sent[0].Headers!["List-Unsubscribe-Post"].Should().Be("List-Unsubscribe=One-Click");
        db().EmailSends.Single().Should().Match<EmailSend>(s => s.Key == "welcome" && s.Status == "sent");
    }

    [Fact]
    public async Task SendsNothing_WhenSwitchedOff_AtNight_OrInDryRun()
    {
        var (off, offSender, offDb) = Create(new EmailSequenceOptions { Enabled = false });
        AddCustomer(offDb(), "a@test.com", Morning.AddHours(-1));
        await off.RunOnceAsync(Morning, CancellationToken.None);

        var (night, nightSender, nightDb) = Create(new EmailSequenceOptions { Enabled = true });
        AddCustomer(nightDb(), "b@test.com", Morning.AddHours(-1));
        await night.RunOnceAsync(Morning.AddHours(12), CancellationToken.None); // 22:00 in South Africa

        var (dry, drySender, dryDb) = Create(new EmailSequenceOptions { Enabled = true, DryRun = true });
        AddCustomer(dryDb(), "c@test.com", Morning.AddHours(-1));
        await dry.RunOnceAsync(Morning, CancellationToken.None);

        offSender.Sent.Should().BeEmpty();
        nightSender.Sent.Should().BeEmpty();
        drySender.Sent.Should().BeEmpty();
        dryDb().EmailSends.Should().BeEmpty("a dry run must not block the real email later");
    }

    [Fact]
    public async Task SkipsUnsubscribedPeople_AndThoseOutsideTheTestingList()
    {
        var (worker, sender, db) = Create(new EmailSequenceOptions { Enabled = true, OnlyTo = "@spazasure.co.za" });
        var gone = AddCustomer(db(), "gone@spazasure.co.za", Morning.AddHours(-2));
        var ctx = db();
        ctx.Users.First(u => u.Id == gone.Id).EmailUnsubscribedAt = Morning.AddHours(-1);
        ctx.SaveChanges();
        AddCustomer(db(), "outsider@gmail.com", Morning.AddHours(-2));
        AddCustomer(db(), "team@spazasure.co.za", Morning.AddHours(-2));

        await worker.RunOnceAsync(Morning, CancellationToken.None);

        sender.Sent.Select(s => s.To).Should().Equal("team@spazasure.co.za");
    }

    [Fact]
    public async Task ConfirmsWaitlistSignups_EvenWithSequencesOff()
    {
        var (worker, sender, db) = Create(new EmailSequenceOptions { Enabled = false });
        var ctx = db();
        ctx.WaitlistSignups.Add(new WaitlistSignup { Email = "new@test.com", Interest = "customer" });
        ctx.SaveChanges();

        await worker.RunOnceAsync(Morning, CancellationToken.None);
        await worker.RunOnceAsync(Morning.AddMinutes(15), CancellationToken.None);

        sender.Sent.Should().ContainSingle(s => s.Subject == "Confirm your spot on the SpazaSure waitlist");
        db().WaitlistSignups.Single().ConfirmationSentAt.Should().Be(Morning);
    }

    [Fact]
    public async Task RespectsTheDailyLimit()
    {
        var (worker, sender, db) = Create(new EmailSequenceOptions { Enabled = true, DailyLimit = 2 });
        for (var i = 0; i < 5; i++) AddCustomer(db(), $"p{i}@test.com", Morning.AddHours(-1));

        await worker.RunOnceAsync(Morning, CancellationToken.None);

        sender.Sent.Should().HaveCount(2);
    }
}

public class FeedbackAndWaitlistTests
{
    private sealed class MemoryStorage : IFileStorageService
    {
        public List<string> Keys { get; } = [];
        public Task<string> SaveAsync(Stream content, string key, string contentType, CancellationToken ct = default)
        {
            Keys.Add(key);
            return Task.FromResult("/uploads/" + key);
        }
        public Task DeleteAsync(string url, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ControllerContext As(Guid userId, string role) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "Test")),
        },
    };

    private static (FeedbackController Ctrl, SpazaSureDbContext Db, User User, MemoryStorage Storage) CreateFeedback()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var user = new User { Email = "shop@test.com", Phone = "0820001234", RoleId = db.Roles.First(r => r.Name == "spaza_owner").Id };
        db.Users.Add(user);
        db.SaveChanges();
        var storage = new MemoryStorage();
        var ctrl = new FeedbackController(db, storage, new EventPublisher("__nonexistent__", "x", "x"))
        {
            ControllerContext = As(user.Id, "spaza_owner"),
        };
        return (ctrl, db, user, storage);
    }

    [Fact]
    public async Task Feedback_IsStoredWithItsContext_AndScreenshotsGoToAPrivateFolder()
    {
        var (ctrl, db, user, storage) = CreateFeedback();
        var shot = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("png")), 0, 3, "screenshot", "s.png")
        {
            Headers = new HeaderDictionary(), ContentType = "image/png",
        };

        var result = await ctrl.Send(new FeedbackController.FeedbackForm
        {
            Type = "bug", Message = "The scanner froze on a can of beans", Rating = 3, Screen = "Scan Product",
            AppVersion = "1.0.11+13", Platform = "android 14", Screenshot = shot,
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var item = db.FeedbackItems.Single();
        item.Should().Match<FeedbackItem>(f => f.UserId == user.Id && f.Role == "spaza_owner" && f.Type == "bug"
            && f.Status == "new" && f.Screen == "Scan Product" && f.Rating == 3);
        storage.Keys.Single().Should().StartWith("feedback-screenshots/");
        PrivateFiles.IsPrivate(item.ScreenshotUrl!).Should().BeTrue();
    }

    [Fact]
    public async Task Feedback_RejectsBadInput_AndIgnoresADoubleTap()
    {
        var (ctrl, db, _, _) = CreateFeedback();

        (await ctrl.Send(new() { Message = "hi" }, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await ctrl.Send(new() { Message = "Looks great", Type = "rant" }, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await ctrl.Send(new() { Message = "Looks great", Rating = 9 }, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();

        await ctrl.Send(new() { Message = "Please add Afrikaans" }, CancellationToken.None);
        await ctrl.Send(new() { Message = "Please add Afrikaans" }, CancellationToken.None);

        db.FeedbackItems.Should().ContainSingle();
    }

    [Fact]
    public async Task AdminCanTriageFeedback()
    {
        var (ctrl, db, _, _) = CreateFeedback();
        await ctrl.Send(new() { Message = "Dark mode please", Type = "idea" }, CancellationToken.None);
        var admin = new AdminFeedbackController(db);
        var id = db.FeedbackItems.Single().Id;

        (await admin.Update(id, new("planned", "For the next release"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await admin.Update(id, new("someday", null), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();

        db.FeedbackItems.Single().Should().Match<FeedbackItem>(f => f.Status == "planned" && f.AdminNote == "For the next release");
    }

    [Fact]
    public async Task Waitlist_StoresANormalisedSignup_Once()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var ctrl = new WaitlistController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };

        var first = await ctrl.Join(new("  Sipho@Example.COM ", "Sipho", "082 123 4567", "spaza_owner", "Soweto", "facebook", true, null), default);
        var again = await ctrl.Join(new("sipho@example.com", null, null, "spaza_owner", null, null, false, null), default);

        first.Should().BeOfType<OkObjectResult>();
        again.Should().BeOfType<OkObjectResult>();
        var signup = db.WaitlistSignups.Single();
        signup.Should().Match<WaitlistSignup>(w => w.Email == "sipho@example.com" && w.Phone == "+27821234567"
            && w.Interest == "spaza_owner" && w.MarketingConsent && w.Source == "facebook");
    }

    [Fact]
    public async Task Waitlist_RejectsBadInput_AndQuietlyDropsBots()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var ctrl = new WaitlistController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };

        (await ctrl.Join(new("not-an-email", null, null, "customer", null, null, false, null), default)).Should().BeOfType<BadRequestObjectResult>();
        (await ctrl.Join(new("a@b.co.za", null, null, "reseller", null, null, false, null), default)).Should().BeOfType<BadRequestObjectResult>();
        (await ctrl.Join(new("a@b.co.za", null, "12345", "customer", null, null, false, null), default)).Should().BeOfType<BadRequestObjectResult>();
        (await ctrl.Join(new("bot@spam.com", null, null, "customer", null, null, false, "http://spam"), default)).Should().BeOfType<OkObjectResult>();

        db.WaitlistSignups.Should().BeEmpty();
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("Soweto, Gauteng", "\"Soweto, Gauteng\"")]
    [InlineData("plain", "plain")]
    public void WaitlistExport_DefusesSpreadsheetFormulas(string value, string cell)
    {
        AdminWaitlistController.Cell(value).Should().Be(cell);
    }
}

public class LastSeenTests
{
    [Fact]
    public async Task RefreshingTheSession_CountsAsUsingTheApp()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var svc = new AuthenticationService(db, ConfigHelper.Build());
        await svc.RegisterAsync(new RegisterRequest("supplier", "seen@test.com", "0820000009", "Password123!",
            "Seen Co", "Lindiwe", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");
        var (_, _, login) = await svc.LoginAsync(new LoginRequest("seen@test.com", null, "Password123!"), "127.0.0.1");
        var user = db.Users.Single(u => u.Email == "seen@test.com");
        user.LastSeenAt.Should().NotBeNull();

        user.LastSeenAt = DateTime.UtcNow.AddDays(-10);
        db.SaveChanges();
        var (ok, _, _) = await svc.RefreshAsync(login!.RefreshToken, "127.0.0.1");

        ok.Should().BeTrue();
        db.Users.Single(u => u.Id == user.Id).LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
