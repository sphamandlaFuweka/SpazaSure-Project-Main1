using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Didit is the first verification step: an admin can only approve a shop's or supplier's
/// documents, or verify the account, once Didit has passed the owner (shop) or business (supplier).
/// </summary>
public static class IdentityChecks
{
    public const string Approved = "Approved";

    public static bool HasPassed(IHasIdentityCheck subject) => subject.IdentityCheckStatus == Approved;

    /// <summary>Why an admin can't approve yet, or null when Didit has passed.</summary>
    public static string? AdminBlockReason(IHasIdentityCheck subject) => HasPassed(subject)
        ? null
        : subject is SpazaShop
            ? "The owner hasn't passed the Didit identity check yet. Approve after it passes."
            : "The supplier hasn't passed the Didit business check yet. Approve after it passes.";

    /// <summary>Starts the check over, e.g. after the ID number or company details change.</summary>
    public static void Reset(IHasIdentityCheck subject)
    {
        subject.IdentityCheckSessionId = null;
        subject.IdentityCheckStatus = null;
        subject.IdentityCheckNotes = null;
        subject.IdentityCheckUpdatedAt = DateTime.UtcNow;
    }
}
