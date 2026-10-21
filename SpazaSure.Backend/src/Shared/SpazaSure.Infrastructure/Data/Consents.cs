using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Proof of consent (POPIA s11): who agreed to what, when, from where, and to which version of
/// the privacy notice. Stored in the audit log as "consent_{kind}:{version}".
/// </summary>
public static class Consents
{
    /// <summary>The privacy notice, accepted when registering.</summary>
    public const string PrivacyNotice = "privacy_notice";
    /// <summary>Didit checking the person's ID or business, including outside South Africa (s72).</summary>
    public const string IdentityCheck = "identity_check";
    /// <summary>Direct marketing (s69).</summary>
    public const string Marketing = "marketing";

    /// <summary>Adds the consent record. Does not save.</summary>
    public static void Record(SpazaSureDbContext db, Guid userId, string kind, string? ipAddress) =>
        db.AuthAuditLogs.Add(new AuthAuditLog
        {
            UserId = userId,
            Event = $"consent_{kind}:{SpazaSure.Shared.Helpers.PrivacyNotice.Version}",
            IpAddress = ipAddress,
        });
}
