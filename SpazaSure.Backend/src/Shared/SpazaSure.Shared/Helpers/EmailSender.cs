using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace SpazaSure.Shared.Helpers;

public interface IEmailSender
{
    /// <summary>True when SMTP settings are present, so emails can actually be delivered.</summary>
    bool IsConfigured { get; }

    /// <param name="headers">Extra headers, e.g. List-Unsubscribe on automated emails.</param>
    Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default,
        IReadOnlyDictionary<string, string>? headers = null);
}

/// <summary>
/// Sends email over SMTP. Works with any provider that offers SMTP
/// (SendGrid, Brevo, Mailgun, Amazon SES, Google Workspace, cPanel mail, ...).
///
/// Configuration (appsettings or environment variables, e.g. Email__SmtpHost):
///   Email:SmtpHost, Email:SmtpPort (default 587), Email:Username, Email:Password,
///   Email:FromAddress, Email:FromName (default "SpazaSure"), Email:UseSsl (default true)
/// </summary>
public class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    private string? Host => config["Email:SmtpHost"];
    private string? FromAddress => config["Email:FromAddress"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);

    public async Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Email is not configured (Email:SmtpHost and Email:FromAddress are required).");

        using var message = new MailMessage
        {
            From = new MailAddress(FromAddress!, config["Email:FromName"] ?? "SpazaSure"),
            Subject = subject,
            Body = textBody,
            IsBodyHtml = false,
        };
        message.To.Add(to);
        if (!string.IsNullOrWhiteSpace(config["Email:ReplyTo"]))
            message.ReplyToList.Add(config["Email:ReplyTo"]!);
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            message.Headers.Add(name, value);
        // Plain-text body plus an HTML alternative, so every mail client can show it
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html"));

        using var client = new SmtpClient(Host!, int.TryParse(config["Email:SmtpPort"], out var port) ? port : 587)
        {
            EnableSsl = !bool.TryParse(config["Email:UseSsl"], out var ssl) || ssl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15_000,
        };
        var user = config["Email:Username"];
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, config["Email:Password"]);

        await client.SendMailAsync(message, ct);
    }
}
