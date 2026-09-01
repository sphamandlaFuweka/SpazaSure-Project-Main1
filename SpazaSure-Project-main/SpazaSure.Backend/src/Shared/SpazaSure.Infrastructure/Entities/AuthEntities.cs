using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;
}

public class OtpCode : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;   // registration, login, password_reset
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public User? User { get; set; }
}

public class AuthAuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
