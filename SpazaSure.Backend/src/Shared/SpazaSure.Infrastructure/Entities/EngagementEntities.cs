using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>Feedback sent from the app or a portal: a bug, an idea, a question or praise.</summary>
public class FeedbackItem : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>customer, spaza_owner, supplier or admin, at the time of sending.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>bug, idea, question or praise.</summary>
    public string Type { get; set; } = "idea";
    public string Message { get; set; } = string.Empty;
    /// <summary>Optional 1–5 rating of the app overall.</summary>
    public short? Rating { get; set; }
    /// <summary>Where the person was when they sent it, e.g. "Scan Product".</summary>
    public string? Screen { get; set; }
    public string? ScreenshotUrl { get; set; }
    /// <summary>mobile_app, supplier_portal or admin_console.</summary>
    public string Source { get; set; } = "mobile_app";
    public string? AppVersion { get; set; }
    public string? Platform { get; set; }
    /// <summary>new, planned, in_progress, done or wont_fix.</summary>
    public string Status { get; set; } = "new";
    /// <summary>Internal note from the team; never shown to the sender.</summary>
    public string? AdminNote { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>Someone who joined the waitlist on the website before getting access.</summary>
public class WaitlistSignup : BaseEntity
{
    /// <summary>Lower-cased, unique.</summary>
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    /// <summary>customer, spaza_owner or supplier: who they'd use SpazaSure as.</summary>
    public string Interest { get; set; } = "customer";
    public string? Area { get; set; }
    /// <summary>Where they came from, e.g. a campaign tag in the page link.</summary>
    public string? Source { get; set; }
    /// <summary>They ticked "keep me updated" (POPIA s69 consent for non-customers).</summary>
    public bool MarketingConsent { get; set; }
    public string? IpAddress { get; set; }
    /// <summary>When the confirmation email went out; null until then.</summary>
    public DateTime? ConfirmationSentAt { get; set; }
    /// <summary>When they clicked the link in that email. Only confirmed sign-ups are emailed again.</summary>
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
}

/// <summary>
/// One automated email to one person. The unique (user, key) pair is what stops anyone getting
/// the same sequence email twice, even if two workers run at once.
/// </summary>
public class EmailSend : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>Which email in which sequence, e.g. "welcome", "education_2" or "inactive_7:2026-10-20".</summary>
    public string Key { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    /// <summary>sent, failed or dry_run.</summary>
    public string Status { get; set; } = "sent";
    public string? Error { get; set; }
}
