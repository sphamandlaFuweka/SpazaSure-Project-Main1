using System.Net;
using Microsoft.EntityFrameworkCore;
using SpazaSure.AuthService.DTOs;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.AuthService.Services;

public class AuthenticationService(
    SpazaSureDbContext db,
    IConfiguration config,
    IEmailSender? emailSender = null,
    ILogger<AuthenticationService>? logger = null)
{
    private readonly string _jwtSecret = config["Jwt:Secret"]!;
    private readonly int _accessExpiry = int.Parse(config["Jwt:AccessExpiryMinutes"] ?? "60");
    private readonly int _refreshExpiry = int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30");

    /// <summary>Account types anyone may sign up for. Everything else (admin staff) is created by a master admin.</summary>
    private static readonly HashSet<string> PublicRoles = ["supplier", "spaza_owner", "customer"];

    public async Task<(bool Success, string? Error, AuthResponse? Data)> RegisterAsync(RegisterRequest req, string ipAddress)
    {
        var addressError = AddressValidation.ValidateCoordinates(req.Latitude, req.Longitude);
        if (addressError is not null) return (false, addressError, null);
        if (!req.AcceptedPrivacyNotice) return (false, PrivacyNotice.NotAcceptedMessage, null);

        var email = req.Email?.Trim();
        var phone = req.Phone?.Trim();
        if (!string.IsNullOrEmpty(phone) && SaValidation.NormalizePhone(phone) is null)
            return (false, "Enter a valid South African phone number, e.g. 082 123 4567.", null);

        if (await db.Users.AnyAsync(u =>
            (email != null && u.Email != null && u.Email.ToLower() == email.ToLower()) ||
            (phone != null && u.Phone == phone)))
            return (false, "Email or phone already registered.", null);

        // Admin console accounts are only created by a master admin (Team page), never by self sign-up.
        if (!PublicRoles.Contains(req.Role))
            return (false, "That account type can't be registered here.", null);

        // Look up role by name from DB — no hardcoding
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == req.Role && r.IsActive);
        if (role is null)
            return (false, $"Role '{req.Role}' does not exist.", null);

        var user = new User
        {
            Email = email,
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            RoleId = role.Id,
            Status = "pending"
        };
        db.Users.Add(user);
        Consents.Record(db, user.Id, Consents.PrivacyNotice, ipAddress);

        if (role.Name == "supplier")
        {
            // Admin setting: with manual approval on, new suppliers stay hidden
            // from shops ("pending") until an admin verifies them.
            var rules = await PlatformSettingsStore.LoadAsync(db);
            db.Suppliers.Add(new Supplier
            {
                Status = rules.RequireSupplierApproval ? "pending" : "active",
                UserId = user.Id,
                CompanyName = req.CompanyName ?? string.Empty,
                ContactPerson = req.ContactPerson ?? string.Empty,
                Phone = phone ?? string.Empty,
                Email = email ?? string.Empty,
                Address = req.Address,
                City = req.City,
                Province = req.Province,
                PostalCode = req.PostalCode,
                Latitude = req.Latitude,
                Longitude = req.Longitude
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
                Address = req.Address ?? string.Empty,
                City = req.City,
                Province = req.Province,
                PostalCode = req.PostalCode,
                Latitude = req.Latitude,
                Longitude = req.Longitude
            });
        }

        await db.SaveChangesAsync();
        return (true, null, await IssueTokensAsync(user, role, ipAddress));
    }

    public async Task<(bool Success, string? Error, AuthResponse? Data)> LoginAsync(LoginRequest req, string ipAddress)
    {
        var email = req.Email?.Trim();
        var phone = req.Phone?.Trim();
        var user = await db.Users
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u =>
                (email != null && u.Email != null && u.Email.ToLower() == email.ToLower()) ||
                (phone != null && u.Phone == phone));

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

        // The admin console and supplier portal are separate apps: an account only signs in to its own.
        // The same generic error is returned so neither portal reveals what kind of account it is.
        var adminPortal = string.Equals(req.Role, "admin", StringComparison.OrdinalIgnoreCase);
        var isAdminAccount = string.Equals(user.Role.Name, "admin", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(req.Role) && adminPortal != isAdminAccount)
            return (false, "Invalid credentials.", null);

        // Checked only after the password matches, so suspension isn't revealed to guessers
        if (await IsSuspendedAsync(user))
            return (false, SuspendedMessage, null);

        user.FailedAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastSeenAt = user.LastLoginAt;
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
        // The apps refresh every half hour while open, so this is the "still using it" signal
        stored.User.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // A suspended account can't keep its session alive by refreshing
        if (await IsSuspendedAsync(stored.User))
            return (false, SuspendedMessage, null);

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

    public const string SuspendedMessage = "This account has been suspended. Please contact SpazaSure support.";

    /// <summary>
    /// Admins suspend the supplier or spaza shop record (not the login), so check both,
    /// plus the user's own status.
    /// </summary>
    private async Task<bool> IsSuspendedAsync(User user) =>
        user.Status == "suspended" ||
        await db.Suppliers.AnyAsync(s => s.UserId == user.Id && s.Status == "suspended") ||
        await db.SpazaShops.AnyAsync(s => s.UserId == user.Id && s.Status == "suspended");

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
        await SendPasswordResetEmailAsync(user.Email ?? email, rawToken);
        return true;
    }

    /// <summary>
    /// Emails a one-click reset link. Never throws: the caller always gets the same
    /// response whether or not the account exists, so failures are only logged.
    /// The raw token is never logged unless Email:LogResetTokens is enabled (local dev only).
    /// </summary>
    private async Task SendPasswordResetEmailAsync(string email, string rawToken)
    {
        var portalUrl = (config["Email:PortalUrl"] ?? "https://portal.spazasure.co.za").TrimEnd('/');
        var link = $"{portalUrl}/forgot-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(rawToken)}";

        if (bool.TryParse(config["Email:LogResetTokens"], out var logTokens) && logTokens)
            logger?.LogInformation("[DEV] Password reset link for {Email}: {Link}", email, link);

        if (emailSender is null || !emailSender.IsConfigured)
        {
            logger?.LogWarning("Password reset requested for {Email} but email is not configured (set Email__SmtpHost and Email__FromAddress).", LogMask.Email(email));
            return;
        }

        var safeLink = WebUtility.HtmlEncode(link);
        var html = $"""
            <div style="font-family:Nunito,Arial,sans-serif;background:#F5F7FF;padding:32px 16px">
              <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:24px;overflow:hidden;border:1px solid #E8ECF8">
                <div style="background:#25449A;padding:28px 32px">
                  <div style="color:#ffffff;font-size:22px;font-weight:900">SpazaSure</div>
                </div>
                <div style="padding:32px">
                  <h1 style="margin:0 0 12px;font-size:22px;color:#17244F">Reset your password</h1>
                  <p style="margin:0 0 24px;color:#5A5A7A;font-size:15px;line-height:1.6">
                    We received a request to reset the password for your SpazaSure account.
                    This link is valid for 30 minutes and can be used once.
                  </p>
                  <a href="{safeLink}" style="display:inline-block;background:#F8B217;color:#25449A;font-weight:800;font-size:15px;text-decoration:none;padding:14px 28px;border-radius:50px">Choose a new password</a>
                  <p style="margin:24px 0 0;color:#8888AA;font-size:13px;line-height:1.6">
                    If you didn't ask for this, you can ignore this email. Your password won't change.
                  </p>
                </div>
              </div>
            </div>
            """;
        var text = $"""
            Reset your SpazaSure password

            Open this link within 30 minutes to choose a new password:
            {link}

            If you didn't ask for this, you can ignore this email.
            """;

        try
        {
            await emailSender.SendAsync(email, "Reset your SpazaSure password", html, text);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to send password reset email to {Email}", LogMask.Email(email));
        }
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
