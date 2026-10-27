using System.Security.Cryptography;
using System.Text;

namespace SpazaSure.NotificationService.Email;

/// <summary>
/// Signed links in emails (unsubscribe, confirm the waitlist). The signature means a link only
/// works for the person it was sent to, so nobody can unsubscribe someone else by guessing ids.
/// </summary>
public sealed class EmailLinks
{
    public const string Unsubscribe = "unsubscribe";
    public const string WaitlistConfirm = "waitlist-confirm";
    public const string WaitlistUnsubscribe = "waitlist-unsubscribe";

    private readonly byte[] _key;
    private readonly string _apiBase;

    public EmailLinks(string secret, string publicApiUrl)
    {
        _key = SHA256.HashData(Encoding.UTF8.GetBytes("spazasure-email-links:" + secret));
        _apiBase = publicApiUrl.TrimEnd('/');
    }

    public string Url(string purpose, Guid id) =>
        $"{_apiBase}/api/email/{purpose}?id={id}&t={Token(purpose, id)}";

    public string Token(string purpose, Guid id)
    {
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{purpose}:{id:N}"));
        return Convert.ToBase64String(mac, 0, 18).Replace('+', '-').Replace('/', '_');
    }

    public bool IsValid(string purpose, Guid id, string? token) =>
        token is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Token(purpose, id)), Encoding.ASCII.GetBytes(token));
}
