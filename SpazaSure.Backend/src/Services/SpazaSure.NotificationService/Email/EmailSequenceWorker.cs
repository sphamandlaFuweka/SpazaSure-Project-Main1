using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.NotificationService.Email;

/// <summary>Settings under "EmailSequences". Everything is off until Enabled is set.</summary>
public sealed class EmailSequenceOptions
{
    /// <summary>Send the welcome, feature tip and reminder emails. Waitlist confirmations always go out.</summary>
    public bool Enabled { get; set; }
    /// <summary>Log who would get which email, without sending or recording anything.</summary>
    public bool DryRun { get; set; }
    /// <summary>While testing: only these addresses or "@domains" (comma separated) get sequence emails.</summary>
    public string? OnlyTo { get; set; }
    /// <summary>Emails per South African day, all kinds together (Brevo's free plan allows 300).</summary>
    public int DailyLimit { get; set; } = 250;
    public int SendFromHour { get; set; } = 8;
    public int SendUntilHour { get; set; } = 19;
    public string AppUrl { get; set; } = "https://app.spazasure.co.za";
    public string PortalUrl { get; set; } = "https://portal.spazasure.co.za";
}

/// <summary>
/// Every 15 minutes: sends waitlist confirmation emails, then the next sequence email for each
/// person who is due one (see EmailSequencePlanner). Each email is logged in email_sends before it
/// goes out, under a key that is unique per person, so nobody gets the same email twice.
/// </summary>
public sealed class EmailSequenceWorker(
    IServiceScopeFactory scopes,
    IEmailSender sender,
    IOptionsMonitor<EmailSequenceOptions> options,
    EmailLinks links,
    ILogger<EmailSequenceWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly string[] SequenceRoles = ["customer", "spaza_owner", "supplier"];
    private static readonly string[] ClosedStatuses = ["deleted", "suspended", "rejected"];

    /// <summary>South Africa has no daylight saving, so a fixed offset is exact.</summary>
    public static DateTime SouthAfricanTime(DateTime utc) => utc.AddHours(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the service finish starting (and the database come up) first
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Email sequence run failed");
            }
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }

    public async Task RunOnceAsync(DateTime now, CancellationToken ct)
    {
        if (!sender.IsConfigured) return;
        var o = options.CurrentValue;
        var local = SouthAfricanTime(now);
        if (local.Hour < o.SendFromHour || local.Hour >= o.SendUntilHour) return;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpazaSureDbContext>();

        var dayStartUtc = local.Date.AddHours(-2);
        var budget = o.DailyLimit - await db.EmailSends.CountAsync(s => s.CreatedAt >= dayStartUtc && s.Status == "sent", ct)
                     - await db.WaitlistSignups.CountAsync(w => w.ConfirmationSentAt >= dayStartUtc, ct);
        if (budget <= 0) return;

        budget -= await SendWaitlistConfirmationsAsync(db, now, budget, ct);
        if (budget > 0 && o.Enabled)
            await SendSequenceEmailsAsync(db, o, now, budget, ct);
    }

    private async Task<int> SendWaitlistConfirmationsAsync(SpazaSureDbContext db, DateTime now, int budget, CancellationToken ct)
    {
        var pending = await db.WaitlistSignups
            .Where(w => w.ConfirmedAt == null && w.ConfirmationSentAt == null && w.UnsubscribedAt == null)
            .OrderBy(w => w.CreatedAt).Take(Math.Min(budget, 50))
            .ToListAsync(ct);
        foreach (var w in pending)
        {
            var email = EmailTemplates.WaitlistConfirmation(w.Name,
                links.Url(EmailLinks.WaitlistConfirm, w.Id), links.Url(EmailLinks.WaitlistUnsubscribe, w.Id));
            try
            {
                await sender.SendAsync(w.Email, email.Subject, email.Html, email.Text, ct);
            }
            catch (Exception ex)
            {
                // Marked as sent anyway: a bad address isn't retried forever. Signing up again resends it.
                logger.LogWarning(ex, "Waitlist confirmation to {Email} failed", LogMask.Email(w.Email));
            }
            w.ConfirmationSentAt = now;
            await db.SaveChangesAsync(ct);
        }
        return pending.Count;
    }

    private async Task SendSequenceEmailsAsync(SpazaSureDbContext db, EmailSequenceOptions o, DateTime now, int budget, CancellationToken ct)
    {
        var newSince = now.AddDays(-14);
        var quietSince = now.AddDays(-7);
        var giveUp = now - EmailSequencePlanner.GiveUpAfter;

        var candidates = await db.Users.AsNoTracking()
            .Where(u => u.Email != null && u.EmailUnsubscribedAt == null
                        && SequenceRoles.Contains(u.Role.Name) && !ClosedStatuses.Contains(u.Status)
                        && (u.CreatedAt >= newSince
                            || ((u.LastSeenAt ?? u.CreatedAt) <= quietSince && (u.LastSeenAt ?? u.CreatedAt) >= giveUp)))
            .OrderBy(u => u.CreatedAt)
            .Take(1000)
            .Select(u => new
            {
                u.Id, Email = u.Email!, Role = u.Role.Name, u.CreatedAt, u.LastSeenAt,
                Name = u.Role.Name == "customer" && u.CustomerProfile != null
                    ? (u.CustomerProfile.PreferredName ?? u.CustomerProfile.FirstName)
                    : u.Role.Name == "spaza_owner" && u.SpazaShop != null ? u.SpazaShop.OwnerName
                    : u.Role.Name == "supplier" && u.Supplier != null ? u.Supplier.ContactPerson
                    : null,
            })
            .ToListAsync(ct);
        candidates = candidates.Where(c => EmailSequencePlanner.AllowedWhileTesting(c.Email, o.OnlyTo)).ToList();
        if (candidates.Count == 0) return;

        var ids = candidates.Select(c => c.Id).ToList();
        var history = (await db.EmailSends.AsNoTracking()
                .Where(s => ids.Contains(s.UserId) && s.CreatedAt >= now.AddDays(-120))
                .Select(s => new { s.UserId, s.Key, s.CreatedAt })
                .ToListAsync(ct))
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<SentEmail>)g.Select(s => new SentEmail(s.Key, s.CreatedAt)).ToList());

        var sentNow = 0;
        foreach (var c in candidates)
        {
            if (sentNow >= budget) break;
            var person = new SequenceRecipient(c.Id, c.Email, c.Role, FirstWord(c.Name), c.CreatedAt, c.LastSeenAt);
            var plan = EmailSequencePlanner.Next(person, history.GetValueOrDefault(c.Id, []), now);
            if (plan is null) continue;

            var email = EmailTemplates.Render(plan.Step, new EmailContext(
                c.Role, person.Name, o.AppUrl, o.PortalUrl, links.Url(EmailLinks.Unsubscribe, c.Id)));
            if (o.DryRun)
            {
                logger.LogInformation("Dry run: would send {Key} to {Email}", plan.Key, LogMask.Email(c.Email));
                continue;
            }

            if (await SendLoggedAsync(db, c.Id, c.Email, plan.Key, email, ct)) sentNow++;
        }
        if (sentNow > 0) logger.LogInformation("Sent {Count} sequence emails", sentNow);
    }

    /// <summary>Logs the email first (the unique key blocks a second copy), then sends it.</summary>
    private async Task<bool> SendLoggedAsync(SpazaSureDbContext db, Guid userId, string to, string key,
        RenderedEmail email, CancellationToken ct)
    {
        var log = new EmailSend { UserId = userId, Key = key, Email = to, Subject = email.Subject, Status = "sending" };
        db.EmailSends.Add(log);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(log).State = EntityState.Detached; // already sent by an earlier run
            return false;
        }

        try
        {
            var unsubscribe = links.Url(EmailLinks.Unsubscribe, userId);
            await sender.SendAsync(to, email.Subject, email.Html, email.Text, ct, new Dictionary<string, string>
            {
                // One-click unsubscribe (RFC 8058), which Gmail and Yahoo expect from bulk senders
                ["List-Unsubscribe"] = $"<{unsubscribe}>",
                ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
            });
            log.Status = "sent";
        }
        catch (Exception ex)
        {
            log.Status = "failed";
            log.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            logger.LogWarning(ex, "Sequence email {Key} to {Email} failed", key, LogMask.Email(to));
        }
        await db.SaveChangesAsync(ct);
        return log.Status == "sent";
    }

    private static string? FirstWord(string? name) =>
        name?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
}
