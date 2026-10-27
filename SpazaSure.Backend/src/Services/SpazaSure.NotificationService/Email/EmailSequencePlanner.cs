namespace SpazaSure.NotificationService.Email;

/// <summary>A person who might get a sequence email.</summary>
public sealed record SequenceRecipient(
    Guid Id, string Email, string Role, string? Name, DateTime CreatedAt, DateTime? LastSeenAt);

/// <summary>An email already sent to them: its key and when.</summary>
public sealed record SentEmail(string Key, DateTime SentAt);

/// <summary>The next email to send: the unique key it is logged under, and which template.</summary>
public sealed record PlannedEmail(string Key, string Step);

/// <summary>
/// Decides which sequence email, if any, someone should get now. Pure, so the rules are easy to
/// test and to read in one place:
/// <list type="bullet">
/// <item>Welcome within 3 days of joining.</item>
/// <item>Three feature tips on days 2, 5 and 9, each only within a few days of its date, so
/// switching sequences on never floods older accounts.</item>
/// <item>"We miss you" after 7 days without using the app, and a feedback request after 21, once
/// per quiet spell. Nothing after 60 quiet days.</item>
/// <item>Never more than one email a day, and no reminder within 3 days of any other email.</item>
/// </list>
/// </summary>
public static class EmailSequencePlanner
{
    public const string Welcome = "welcome";
    public const string Inactive7 = "inactive_7";
    public const string Inactive21 = "inactive_21";

    /// <summary>Feature tips: key, first day, last day (exclusive).</summary>
    public static readonly (string Key, int From, int Until)[] Education =
    [
        ("education_1", 2, 6),
        ("education_2", 5, 9),
        ("education_3", 9, 14),
    ];

    public static readonly TimeSpan MinimumGap = TimeSpan.FromHours(20);
    public static readonly TimeSpan ReminderGap = TimeSpan.FromDays(3);
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromDays(60);

    public static PlannedEmail? Next(SequenceRecipient r, IReadOnlyCollection<SentEmail> sent, DateTime now)
    {
        var lastEmail = sent.Count == 0 ? (DateTime?)null : sent.Max(s => s.SentAt);
        if (lastEmail is { } last && now - last < MinimumGap) return null;
        bool Has(string key) => sent.Any(s => s.Key == key);

        var age = now - r.CreatedAt;
        if (age < TimeSpan.Zero) return null;

        if (!Has(Welcome) && age <= TimeSpan.FromDays(3))
            return new(Welcome, Welcome);

        foreach (var (key, from, until) in Education)
            if (!Has(key) && age >= TimeSpan.FromDays(from) && age < TimeSpan.FromDays(until))
                return new(key, key);

        var lastActive = r.LastSeenAt ?? r.CreatedAt;
        var idle = now - lastActive;
        if (age < TimeSpan.FromDays(7) || idle < TimeSpan.FromDays(7) || idle > GiveUpAfter) return null;
        if (lastEmail is { } recent && now - recent < ReminderGap) return null;

        // Keyed by the day they went quiet: each quiet spell gets its own reminders, once
        var spell = lastActive.ToString("yyyy-MM-dd");
        if (idle >= TimeSpan.FromDays(21) && !Has($"{Inactive21}:{spell}"))
            return new($"{Inactive21}:{spell}", Inactive21);
        if (!Has($"{Inactive7}:{spell}") && !Has($"{Inactive21}:{spell}"))
            return new($"{Inactive7}:{spell}", Inactive7);
        return null;
    }

    /// <summary>
    /// Whether an address may get sequence emails while testing. Empty list: everyone.
    /// Entries are full addresses or "@domain.co.za".
    /// </summary>
    public static bool AllowedWhileTesting(string email, string? onlyTo)
    {
        if (string.IsNullOrWhiteSpace(onlyTo)) return true;
        foreach (var entry in onlyTo.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.StartsWith('@') ? email.EndsWith(entry, StringComparison.OrdinalIgnoreCase)
                                      : email.Equals(entry, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
