using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// An admin can only approve a shop's or supplier's documents, or verify the account, after Didit
/// passes the owner (shop) or business (supplier), or the admin records a manual decision.
/// </summary>
public static class IdentityChecks
{
    public const string Approved = "Approved";

    public static bool HasPassed(IHasIdentityCheck subject) => subject.IdentityCheckStatus == Approved;

    /// <summary>Why an admin can't approve yet, or null when Didit has passed.</summary>
    public static string? AdminBlockReason(IHasIdentityCheck subject) => HasPassed(subject)
        ? null
        : subject is SpazaShop
            ? "The shop owner must pass Didit or be manually verified by an admin before approval."
            : "The supplier must pass Didit or be manually verified by an admin before approval.";

    /// <summary>Starts the check over, e.g. after the ID number or company details change.</summary>
    public static void Reset(IHasIdentityCheck subject)
    {
        subject.IdentityCheckSessionId = null;
        subject.IdentityCheckStatus = null;
        subject.IdentityCheckNotes = null;
        subject.IdentityCheckUpdatedAt = DateTime.UtcNow;
    }
}
