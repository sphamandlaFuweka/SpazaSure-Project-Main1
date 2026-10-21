namespace SpazaSure.Shared.Helpers;

/// <summary>
/// The SpazaSure privacy notice (POPIA s18) people accept when they register. Change the version
/// whenever the notice changes, so each recorded consent shows which wording the person agreed to.
/// </summary>
public static class PrivacyNotice
{
    public const string Version = "2026-10-20-draft";
    public const string Url = "https://portal.spazasure.co.za/privacy";
    public const string NotAcceptedMessage = "Please read and accept the SpazaSure privacy notice to register.";
}
