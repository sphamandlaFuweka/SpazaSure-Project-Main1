using Microsoft.EntityFrameworkCore;
using SpazaSure.AuthService.DTOs;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using System.Text.Json;

namespace SpazaSure.AuthService.Services;

/// <summary>
/// Handles phone/password authentication for customer app accounts.
/// </summary>
public class CustomerAuthService(SpazaSureDbContext db, IConfiguration config)
{
    private readonly string _jwtSecret = config["Jwt:Secret"]!;
    private readonly int _accessExpiry = int.Parse(config["Jwt:AccessExpiryMinutes"] ?? "60");
    private readonly int _refreshExpiry = int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30");

    public async Task<(bool Success, string? Error, CustomerAuthResponse? Data)> RegisterAsync(
        CustomerRegisterRequest req, string ipAddress)
    {
        if (req.Latitude.HasValue != req.Longitude.HasValue)
            return (false, "Location coordinates must be provided together.", null);
        if (!req.AcceptedPrivacyNotice) return (false, PrivacyNotice.NotAcceptedMessage, null);

        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();

        if (await db.Users.AnyAsync(u =>
                u.Phone == req.Phone ||
                (email != null && u.Email != null && u.Email.ToLower() == email.ToLower())))
            return (false, "Phone number already registered.", null);

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "customer" && r.IsActive);
        if (role is null) return (false, "Role configuration error.", null);

        var user = new User
        {
            Phone = req.Phone,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            RoleId = role.Id,
            Status = "active"
        };
        db.Users.Add(user);

        var profile = new CustomerProfile
        {
            UserId = user.Id,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            PreferredName = string.IsNullOrWhiteSpace(req.PreferredName)
                ? null
                : req.PreferredName.Trim(),
            Age = req.Age,
            PreferredLanguage = string.IsNullOrWhiteSpace(req.PreferredLanguage)
                ? "English"
                : req.PreferredLanguage.Trim(),
            Province = NormalizeOptional(req.Province),
            City = NormalizeOptional(req.City),
            Suburb = NormalizeOptional(req.Suburb),
            NearbyRecommendationsEnabled = req.NearbyRecommendationsEnabled,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            Allergies = JsonSerializer.Serialize(req.Allergies ?? []),
            Interests = JsonSerializer.Serialize(req.Interests ?? []),
            NotificationPreferences = JsonSerializer.Serialize(
                req.NotificationPreferences ?? []),
            MarketingOptIn = req.MarketingOptIn
        };
        db.CustomerProfiles.Add(profile);

        // Save user + profile first so the FK constraint is satisfied
        await db.SaveChangesAsync();

        db.AuthAuditLogs.Add(new AuthAuditLog
        {
            UserId = user.Id,
            Event = "customer_register",
            IpAddress = ipAddress
        });
        Consents.Record(db, user.Id, Consents.PrivacyNotice, ipAddress);
        if (req.MarketingOptIn) Consents.Record(db, user.Id, Consents.Marketing, ipAddress);
        await db.SaveChangesAsync();

        return (true, null, await IssueTokensAsync(user, role, profile, ipAddress));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<(bool Success, string? Error, CustomerAuthResponse? Data)> LoginAsync(
        CustomerLoginRequest req, string ipAddress)
    {
        var user = await db.Users
            .Include(u => u.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Phone == req.Phone && u.Role.Name == "customer");

        if (user is null) return (false, "Invalid phone number or password.", null);
        if (user.LockedUntil > DateTime.UtcNow) return (false, "Account locked. Please contact support.", null);

        if (user.PasswordHash is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= 5)
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            await db.SaveChangesAsync();
            return (false, "Invalid phone number or password.", null);
        }

        user.FailedAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;

        db.AuthAuditLogs.Add(new AuthAuditLog
        {
            UserId = user.Id,
            Event = "customer_login",
            IpAddress = ipAddress
        });
        await db.SaveChangesAsync();

        return (true, null, await IssueTokensAsync(user, user.Role, user.CustomerProfile, ipAddress));
    }

    private async Task<CustomerAuthResponse> IssueTokensAsync(
        User user, Role role, CustomerProfile? profile, string ipAddress)
    {
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

        List<string> allergies;
        try { allergies = JsonSerializer.Deserialize<List<string>>(profile?.Allergies ?? "[]") ?? []; }
        catch (JsonException) { allergies = []; }
        List<string> interests;
        try { interests = JsonSerializer.Deserialize<List<string>>(profile?.Interests ?? "[]") ?? []; }
        catch (JsonException) { interests = []; }
        List<string> notificationPreferences;
        try
        {
            notificationPreferences = JsonSerializer.Deserialize<List<string>>(
                profile?.NotificationPreferences ?? "[]") ?? [];
        }
        catch (JsonException) { notificationPreferences = []; }

        var firstName = profile?.FirstName ?? string.Empty;
        var lastName = profile?.LastName ?? string.Empty;

        return new CustomerAuthResponse(
            accessToken,
            rawRefresh,
            DateTime.UtcNow.AddMinutes(_accessExpiry),
            user.Id,
            firstName,
            lastName,
            $"{firstName} {lastName}".Trim(),
            user.Email ?? string.Empty,
            user.Phone ?? string.Empty,
            profile?.Age,
            allergies,
            profile?.PreferredName,
            profile?.PreferredLanguage ?? "English",
            profile?.Province,
            profile?.City,
            profile?.Suburb,
            profile?.NearbyRecommendationsEnabled ?? false,
            profile?.Latitude,
            profile?.Longitude,
            interests,
            notificationPreferences,
            profile?.MarketingOptIn ?? false
        );
    }
}
