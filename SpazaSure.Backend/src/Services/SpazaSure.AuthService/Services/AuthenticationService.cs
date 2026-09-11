using Microsoft.EntityFrameworkCore;
using SpazaSure.AuthService.DTOs;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.AuthService.Services;

public class AuthenticationService(SpazaSureDbContext db, IConfiguration config)
{
    private readonly string _jwtSecret = config["Jwt:Secret"]!;
    private readonly int _accessExpiry = int.Parse(config["Jwt:AccessExpiryMinutes"] ?? "60");
    private readonly int _refreshExpiry = int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30");

    public async Task<(bool Success, string? Error, AuthResponse? Data)> RegisterAsync(RegisterRequest req, string ipAddress)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email || u.Phone == req.Phone))
            return (false, "Email or phone already registered.", null);

        // Look up role by name from DB — no hardcoding
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == req.Role && r.IsActive);
        if (role is null)
            return (false, $"Role '{req.Role}' does not exist.", null);

        var user = new User
        {
            Email = req.Email,
            Phone = req.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            RoleId = role.Id,
            Status = "pending"
        };
        db.Users.Add(user);

        if (role.Name == "supplier")
        {
            db.Suppliers.Add(new Supplier
            {
                UserId = user.Id,
                CompanyName = req.CompanyName ?? string.Empty,
                ContactPerson = req.ContactPerson ?? string.Empty,
                Phone = req.Phone ?? string.Empty,
                Email = req.Email ?? string.Empty
            });
        }
        else if (role.Name == "spaza_owner")
        {
            db.SpazaShops.Add(new SpazaShop
            {
                UserId = user.Id,
                ShopName = req.ShopName ?? string.Empty,
                OwnerName = req.OwnerName ?? string.Empty,
                Phone = req.Phone ?? string.Empty,
                Address = req.Address ?? string.Empty
            });
        }

        await db.SaveChangesAsync();
        return (true, null, await IssueTokensAsync(user, role, ipAddress));
    }

    public async Task<(bool Success, string? Error, AuthResponse? Data)> LoginAsync(LoginRequest req, string ipAddress)
    {
        var user = await db.Users
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u =>
                (req.Email != null && u.Email == req.Email) ||
                (req.Phone != null && u.Phone == req.Phone));

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            if (user is not null)
            {
                user.FailedAttempts++;
                if (user.FailedAttempts >= 5)
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                await db.SaveChangesAsync();
            }
            return (false, "Invalid credentials.", null);
        }

        if (user.LockedUntil > DateTime.UtcNow)
            return (false, "Account locked. Try again later.", null);

        user.FailedAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;

        db.AuthAuditLogs.Add(new AuthAuditLog { UserId = user.Id, Event = "login", IpAddress = ipAddress });
        await db.SaveChangesAsync();

        return (true, null, await IssueTokensAsync(user, user.Role, ipAddress));
    }

    public async Task<(bool Success, string? Error, AuthResponse? Data)> RefreshAsync(string rawToken, string ipAddress)
    {
        var hash = JwtHelper.HashToken(rawToken);
        var stored = await db.RefreshTokens
            .Include(r => r.User)
                .ThenInclude(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (stored is null || !stored.IsActive)
            return (false, "Invalid or expired refresh token.", null);

        stored.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (true, null, await IssueTokensAsync(stored.User, stored.User.Role, ipAddress));
    }

    public async Task RevokeAsync(string rawToken)
    {
        var hash = JwtHelper.HashToken(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ForgotPasswordAsync(string email)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return true;

        var existing = await db.OtpCodes
            .Where(o => o.UserId == user.Id && o.Purpose == "password_reset" && o.UsedAt == null)
            .ToListAsync();
        db.OtpCodes.RemoveRange(existing);

        var rawToken = JwtHelper.GenerateRefreshToken();
        db.OtpCodes.Add(new OtpCode
        {
            UserId = user.Id,
            Email = email,
            Code = JwtHelper.HashToken(rawToken),
            Purpose = "password_reset",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });

        await db.SaveChangesAsync();
        Console.WriteLine($"[DEV] Password reset token for {email}: {rawToken}");
        return true;
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(ResetPasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null) return (false, "Invalid or expired reset token.");

        var otp = await db.OtpCodes.FirstOrDefaultAsync(o =>
            o.UserId == user.Id &&
            o.Code == JwtHelper.HashToken(req.Token) &&
            o.Purpose == "password_reset" &&
            o.UsedAt == null &&
            o.ExpiresAt > DateTime.UtcNow);

        if (otp is null) return (false, "Invalid or expired reset token.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.FailedAttempts = 0;
        user.LockedUntil = null;
        otp.UsedAt = DateTime.UtcNow;

        var tokens = await db.RefreshTokens
            .Where(r => r.UserId == user.Id && r.RevokedAt == null)
            .ToListAsync();
        foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;

        db.AuthAuditLogs.Add(new AuthAuditLog { UserId = user.Id, Event = "password_reset" });
        await db.SaveChangesAsync();
        return (true, null);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, Role role, string ipAddress)
    {
        // Load permissions if not already loaded
        var permissions = role.RolePermissions.Any()
            ? role.RolePermissions.Select(rp => rp.Permission.Name).ToList()
            : await db.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.Permission.Name)
                .ToListAsync();

        var accessToken = JwtHelper.GenerateAccessToken(
            user.Id.ToString(), role.Name, permissions, _jwtSecret, _accessExpiry);

        var rawRefresh = JwtHelper.GenerateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = JwtHelper.HashToken(rawRefresh),
            IpAddress = ipAddress,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshExpiry)
        });
        await db.SaveChangesAsync();

        return new AuthResponse(
            accessToken,
            rawRefresh,
            DateTime.UtcNow.AddMinutes(_accessExpiry),
            role.Name,
            user.Id
        );
    }
}
