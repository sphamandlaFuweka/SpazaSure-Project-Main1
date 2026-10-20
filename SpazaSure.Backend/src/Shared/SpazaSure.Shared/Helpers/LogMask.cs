namespace SpazaSure.Shared.Helpers;

/// <summary>Keeps personal information out of logs (POPIA s19) while leaving enough to trace a problem.</summary>
public static class LogMask
{
    /// <summary>"thabo@freshfoods.co.za" becomes "t***@freshfoods.co.za".</summary>
    public static string Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(none)";
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
