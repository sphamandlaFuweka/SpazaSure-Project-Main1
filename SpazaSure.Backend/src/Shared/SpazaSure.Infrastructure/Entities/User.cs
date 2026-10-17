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
