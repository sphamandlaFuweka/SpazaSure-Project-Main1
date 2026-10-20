using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>
/// A user asking for their account and personal information to be deleted (POPIA s24). A master
/// admin approves it, which anonymises the account (see PersonalData). Kept as the record of the
/// request and decision, so it holds no personal details itself.
/// </summary>
public class AccountDeletionRequest : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>spaza_owner, supplier or customer.</summary>
    public string Role { get; set; } = string.Empty;
    public string? Reason { get; set; }
    /// <summary>pending, approved, rejected or cancelled.</summary>
    public string Status { get; set; } = "pending";
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    /// <summary>Why it was rejected, shown to the user.</summary>
    public string? DecisionNote { get; set; }
}
