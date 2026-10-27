using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.NotificationService.Email;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.NotificationService.Controllers;

/// <summary>
/// The pages behind the links in automated emails. Opening a link only shows a page with a button;
/// the button (a POST) does the work. Mail scanners open links in emails automatically, so acting
/// on the GET would unsubscribe or confirm people without them knowing. Mail apps' one-click
/// unsubscribe (RFC 8058) POSTs straight to the same address.
/// </summary>
[ApiController]
[Route("api/email")]
[AllowAnonymous]
public class EmailController(SpazaSureDbContext db, EmailLinks links) : ControllerBase
{
    [HttpGet(EmailLinks.Unsubscribe)]
    public IActionResult UnsubscribePage(Guid id, string? t) =>
        !links.IsValid(EmailLinks.Unsubscribe, id, t)
            ? Page("This link doesn't work", "It may have been copied incompletely. Use the unsubscribe link in the email again.", status: 400)
            : Page("Unsubscribe from SpazaSure emails?",
                "You'll stop getting tips and reminders. You'll still get emails you need for your account, such as password resets.",
                button: "Unsubscribe");

    [HttpPost(EmailLinks.Unsubscribe)]
    public async Task<IActionResult> Unsubscribe(Guid id, string? t, CancellationToken ct)
    {
        if (!links.IsValid(EmailLinks.Unsubscribe, id, t))
            return Page("This link doesn't work", "Use the unsubscribe link in the email again.", status: 400);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is not null && user.EmailUnsubscribedAt is null)
        {
            user.EmailUnsubscribedAt = DateTime.UtcNow;
            db.AuthAuditLogs.Add(new AuthAuditLog { UserId = id, Event = "email_unsubscribed" });
            await db.SaveChangesAsync(ct);
        }
        return Page("You're unsubscribed", "You won't get any more tips or reminders from SpazaSure.");
    }

    [HttpGet(EmailLinks.WaitlistConfirm)]
    public IActionResult WaitlistConfirmPage(Guid id, string? t) =>
        !links.IsValid(EmailLinks.WaitlistConfirm, id, t)
            ? Page("This link doesn't work", "Use the confirmation link in the email again.", status: 400)
            : Page("Confirm your spot", "Tap the button to confirm your email address for the SpazaSure waitlist.",
                button: "Confirm my email");

    [HttpPost(EmailLinks.WaitlistConfirm)]
    public async Task<IActionResult> WaitlistConfirm(Guid id, string? t, CancellationToken ct)
    {
        if (!links.IsValid(EmailLinks.WaitlistConfirm, id, t))
            return Page("This link doesn't work", "Use the confirmation link in the email again.", status: 400);
        var signup = await db.WaitlistSignups.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (signup is null)
            return Page("We couldn't find your sign-up", "You can join the waitlist again on our website.", status: 404);
        signup.ConfirmedAt ??= DateTime.UtcNow;
        signup.UnsubscribedAt = null;
        await db.SaveChangesAsync(ct);
        return Page("You're on the list!", "Thanks for confirming. We'll email you as soon as it's your turn to join SpazaSure.");
    }

    [HttpGet(EmailLinks.WaitlistUnsubscribe)]
    public IActionResult WaitlistUnsubscribePage(Guid id, string? t) =>
        !links.IsValid(EmailLinks.WaitlistUnsubscribe, id, t)
            ? Page("This link doesn't work", "Use the link in the email again.", status: 400)
            : Page("Leave the SpazaSure waitlist?", "We won't email you about SpazaSure again.", button: "Leave the waitlist");

    [HttpPost(EmailLinks.WaitlistUnsubscribe)]
    public async Task<IActionResult> WaitlistUnsubscribe(Guid id, string? t, CancellationToken ct)
    {
        if (!links.IsValid(EmailLinks.WaitlistUnsubscribe, id, t))
            return Page("This link doesn't work", "Use the link in the email again.", status: 400);
        var signup = await db.WaitlistSignups.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (signup is not null)
        {
            signup.UnsubscribedAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Page("You've left the waitlist", "We won't email you again. You're welcome back any time.");
    }

    private ContentResult Page(string title, string message, string? button = null, int status = 200)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        var form = button is null
            ? ""
            : $"<form method=\"post\"><button type=\"submit\">{E(button)}</button></form>";
        var html = $$$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1"><title>{{{E(title)}}} · SpazaSure</title>
            <style>
              body{margin:0;background:#F4F5FA;font-family:Arial,Helvetica,sans-serif;color:#1B1B2F}
              main{max-width:440px;margin:48px auto;background:#fff;border-radius:16px;padding:28px 24px;box-sizing:border-box}
              .brand{color:#25449A;font-weight:bold;font-size:18px;margin-bottom:18px}
              h1{font-size:22px;margin:0 0 10px} p{line-height:1.6;margin:0 0 20px;color:#55556B}
              button{background:#078C50;color:#fff;border:0;border-radius:10px;padding:12px 22px;font-size:15px;font-weight:bold;cursor:pointer}
              @media (max-width:480px){main{margin:16px}}
            </style></head>
            <body><main><div class="brand">SpazaSure</div><h1>{{{E(title)}}}</h1><p>{{{E(message)}}}</p>{{{form}}}</main></body></html>
            """;
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}

/// <summary>Admin console: is email on, what went out, and previews of every email.</summary>
[ApiController]
[Route("api/admin/email-sequences")]
[Authorize(Roles = "admin")]
public class AdminEmailSequencesController(
    SpazaSureDbContext db, IEmailSender sender, IOptionsMonitor<EmailSequenceOptions> options, EmailLinks links) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var o = options.CurrentValue;
        var since = DateTime.UtcNow.AddDays(-30);
        var dayStartUtc = EmailSequenceWorker.SouthAfricanTime(DateTime.UtcNow).Date.AddHours(-2);

        var recent = await db.EmailSends.AsNoTracking().Where(s => s.CreatedAt >= since)
            .Select(s => new { s.Key, s.Status }).ToListAsync(ct);
        var byEmail = recent
            .GroupBy(s => s.Key.Split(':')[0])
            .Select(g => new
            {
                step = g.Key,
                sent = g.Count(s => s.Status == "sent"),
                failed = g.Count(s => s.Status == "failed"),
            })
            .OrderBy(s => Array.IndexOf(EmailTemplates.Steps, s.step));

        var latest = await db.EmailSends.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).Take(30)
            .Select(s => new { s.Key, s.Subject, email = s.Email, s.Status, s.Error, sentAt = s.CreatedAt })
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            emailConfigured = sender.IsConfigured,
            enabled = o.Enabled,
            dryRun = o.DryRun,
            onlyTo = string.IsNullOrWhiteSpace(o.OnlyTo) ? null : o.OnlyTo,
            dailyLimit = o.DailyLimit,
            sendingHours = $"{o.SendFromHour:00}:00–{o.SendUntilHour:00}:00",
            sentToday = await db.EmailSends.CountAsync(s => s.CreatedAt >= dayStartUtc && s.Status == "sent", ct),
            unsubscribed = await db.Users.CountAsync(u => u.EmailUnsubscribedAt != null, ct),
            byEmail,
            latest,
        }));
    }

    [HttpGet("preview")]
    public IActionResult Preview([FromQuery] string step = "welcome", [FromQuery] string role = "customer")
    {
        if (!EmailTemplates.Steps.Contains(step) || !EmailTemplates.Roles.Contains(role))
            return BadRequest(ApiResponse.Fail("Unknown email or role."));
        var email = Render(step, role, "Thandi", Guid.Empty);
        return Ok(ApiResponse<object>.Ok(new { email.Subject, email.Html, email.Text }));
    }

    public sealed record TestEmailRequest(string? Step, string? Role);

    /// <summary>Sends one email to the signed-in admin, to see it in a real inbox.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest(TestEmailRequest req, CancellationToken ct)
    {
        var step = req.Step ?? "welcome";
        var role = req.Role ?? "customer";
        if (!EmailTemplates.Steps.Contains(step) || !EmailTemplates.Roles.Contains(role))
            return BadRequest(ApiResponse.Fail("Unknown email or role."));
        if (!sender.IsConfigured)
            return BadRequest(ApiResponse.Fail("Email isn't set up on the server yet (SMTP settings are missing)."));

        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var admin = Guid.TryParse(idValue, out var adminId)
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == adminId, ct)
            : null;
        if (admin?.Email is null) return BadRequest(ApiResponse.Fail("Your admin account has no email address."));

        var email = Render(step, role, admin.DisplayName, admin.Id);
        await sender.SendAsync(admin.Email, "[Test] " + email.Subject, email.Html, email.Text, ct);
        return Ok(ApiResponse.Ok($"Test email sent to {admin.Email}."));
    }

    private RenderedEmail Render(string step, string role, string? name, Guid userId)
    {
        var o = options.CurrentValue;
        return EmailTemplates.Render(step, new EmailContext(
            role, name?.Split(' ')[0], o.AppUrl, o.PortalUrl, links.Url(EmailLinks.Unsubscribe, userId)));
    }
}
