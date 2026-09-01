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

    public Role Role { get; set; } = null!;
    public Supplier? Supplier { get; set; }
    public SpazaShop? SpazaShop { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<OtpCode> OtpCodes { get; set; } = [];
}
