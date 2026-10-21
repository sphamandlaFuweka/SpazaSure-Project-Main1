using System.Net.Mail;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// Paystack needs an email address for every payment, but shops register with a phone number and
/// often have no email. One-off payments (orders, wallet top-ups, the onboarding fee) then use a
/// SpazaSure address made from the phone number, so nobody is blocked from paying.
/// </summary>
public static class PaymentEmail
{
    public const string Domain = "pay.spazasure.co.za";

    /// <summary>The person's own email when it's valid, otherwise e.g. 27821234567@pay.spazasure.co.za.</summary>
    public static string For(string? email, string? phone, Guid userId)
    {
        if (IsValid(email)) return email!.Trim();
        var local = SaValidation.NormalizePhone(phone) is { } normalized
            ? normalized.TrimStart('+')
            : userId.ToString("N");
        return $"{local}@{Domain}";
    }

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try { return new MailAddress(email.Trim()).Address == email.Trim(); }
        catch (FormatException) { return false; }
    }
}
