using System.Net;
using System.Net.Mail;

namespace SpazaSure.AuthService.Services;

public interface IEmailSender
{
    /// <summary>True when SMTP settings are present, so emails can actually be delivered.</summary>
    bool IsConfigured { get; }

    Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default);
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

    public async Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default)
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
