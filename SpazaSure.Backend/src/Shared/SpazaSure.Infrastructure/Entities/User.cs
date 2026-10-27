using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class User : BaseEntity
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public Guid RoleId { get; set; }
    public string Status { get; set; } = "pending";
    public bool MfaEnabled { get; set; } = false;
    public string? MfaSecret { get; set; }
    public short FailedAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    /// <summary>
    /// Last time the person used SpazaSure: set on sign-in and on every token refresh, which the
    /// apps do every half hour while open. Unlike LastLoginAt it tracks real use, because the apps
    /// stay signed in for weeks.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }
    /// <summary>Set when they unsubscribe from the onboarding and reminder emails.</summary>
    public DateTime? EmailUnsubscribedAt { get; set; }
    public string? ProfilePhotoUrl { get; set; }

    // Dashboard staff (role "admin") only.
    /// <summary>Name shown in the admin console and team list.</summary>
    public string? DisplayName { get; set; }
    /// <summary>Job title chosen by the master admin, e.g. "Finance officer". Display only.</summary>
    public string? AdminTitle { get; set; }
    /// <summary>
    /// What this staff member can do in the admin console: "master", or a comma-separated list of
    /// area:level grants such as "trust:manage,finance:view". See SpazaSure.Shared.Helpers.AdminAccess.
    /// Null on admin accounts that predate staff roles; those are treated as master admins.
    /// </summary>
    public string? AdminAccess { get; set; }

    public Role Role { get; set; } = null!;
    public Supplier? Supplier { get; set; }
    public SpazaShop? SpazaShop { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<OtpCode> OtpCodes { get; set; } = [];
}
