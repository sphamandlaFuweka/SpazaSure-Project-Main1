using System.Text.RegularExpressions;
using FluentAssertions;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Auth;

public class PasswordResetEmailTests
{
    private sealed class FakeEmailSender(bool configured = true, bool fail = false) : IEmailSender
    {
        public List<(string To, string Subject, string Html, string Text)> Sent { get; } = [];
        public bool IsConfigured => configured;

        public Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default)
        {
            if (fail) throw new InvalidOperationException("SMTP down");
            Sent.Add((to, subject, htmlBody, textBody));
            return Task.CompletedTask;
        }
    }

    private static AuthenticationService CreateService(IEmailSender sender, out SpazaSure.Infrastructure.Data.SpazaSureDbContext db)
    {
        db = DbContextHelper.CreateInMemoryDb();
        var config = ConfigHelper.Build(new Dictionary<string, string?> { ["Email:PortalUrl"] = "https://portal.example.com/" });
        return new AuthenticationService(db, config, sender);
    }

    private static Task RegisterAsync(AuthenticationService svc, string email) =>
        svc.RegisterAsync(new RegisterRequest("supplier", email, "0820000099", "Password123!", "Mail Co", "Thandi", null, null, null), "127.0.0.1");

    [Fact]
    public async Task ForgotPassword_SendsEmailWithWorkingResetLink()
    {
        var sender = new FakeEmailSender();
        var svc = CreateService(sender, out _);
        await RegisterAsync(svc, "mail@test.com");

        await svc.ForgotPasswordAsync("mail@test.com");

        sender.Sent.Should().ContainSingle();
        var mail = sender.Sent[0];
        mail.To.Should().Be("mail@test.com");
        mail.Text.Should().Contain("https://portal.example.com/forgot-password?email=mail%40test.com&token=");

        // The emailed token must actually reset the password
        var token = Uri.UnescapeDataString(Regex.Match(mail.Text, @"token=([^\s]+)").Groups[1].Value);
        var (success, error) = await svc.ResetPasswordAsync(new ResetPasswordRequest("mail@test.com", token, "NewPassword456!"));
        success.Should().BeTrue(error);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_SendsNothing()
    {
        var sender = new FakeEmailSender();
        var svc = CreateService(sender, out _);

        var result = await svc.ForgotPasswordAsync("nobody@test.com");

        result.Should().BeTrue();
        sender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPassword_WhenSmtpFails_StillReturnsTrue()
    {
        var svc = CreateService(new FakeEmailSender(fail: true), out _);
        await RegisterAsync(svc, "fail@test.com");

        var result = await svc.ForgotPasswordAsync("fail@test.com");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ForgotPassword_WhenEmailNotConfigured_DoesNotSend()
    {
        var sender = new FakeEmailSender(configured: false);
        var svc = CreateService(sender, out _);
        await RegisterAsync(svc, "nocfg@test.com");

        var result = await svc.ForgotPasswordAsync("nocfg@test.com");

        result.Should().BeTrue();
        sender.Sent.Should().BeEmpty();
    }
}
