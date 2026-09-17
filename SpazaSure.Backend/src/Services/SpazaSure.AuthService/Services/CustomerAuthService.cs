using Microsoft.EntityFrameworkCore;
using SpazaSure.AuthService.DTOs;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using System.Text.Json;

namespace SpazaSure.AuthService.Services;

/// <summary>
/// Mirrors ShopAuthService's phone+OTP flow exactly, for the "customer"
/// role instead of "spaza_owner". Reuses the same OtpCodes table and
/// SmsService — there's nothing shop-specific about sending/verifying an
/// OTP, only what happens after it's verified differs.
/// </summary>
public class CustomerAuthService(SpazaSureDbContext db, IConfiguration config, ILogger<CustomerAuthService> logger, SmsService sms)
{
    private readonly string _jwtSecret = config["Jwt:Secret"]!;
    private readonly int _accessExpiry = int.Parse(config["Jwt:AccessExpiryMinutes"] ?? "60");
    private readonly int _refreshExpiry = int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30");
    private bool SkipOtpVerification => config.GetValue<bool>("Auth:SkipOtpVerification");

    public async Task<(bool Success, string? Error, string? DevOtp)> SendOtpAsync(string phone, string purpose)
    {
        var existing = await db.OtpCodes
            .Where(o => o.Phone == phone && o.Purpose == purpose && o.UsedAt == null)
            .ToListAsync();
        db.OtpCodes.RemoveRange(existing);

        var rawOtp = Random.Shared.Next(100_000, 999_999).ToString();
        var hashed = JwtHelper.HashToken(rawOtp);

        var otpCode = new OtpCode
        {
            Phone = phone,
            Code = hashed,
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        db.OtpCodes.Add(otpCode);
        await db.SaveChangesAsync();

        if (SkipOtpVerification)
        {
            logger.LogWarning("OTP SMS bypass enabled for customer phone {Phone}", phone);
            return (true, null, rawOtp);
        }

        logger.LogWarning("[DEV-TEST] Raw OTP for {Phone}: {Otp}", phone, rawOtp);
        Console.WriteLine($"\n========================================");
        Console.WriteLine($"[DEV-TEST] OTP for {phone}: {rawOtp}");
        Console.WriteLine($"========================================\n");

        var sent = await sms.SendOtpAsync(phone, rawOtp);
        if (!sent)
        {
            logger.LogWarning("SMS delivery failed for {Phone} — removing unusable OTP", phone);
            db.OtpCodes.Remove(otpCode);
            await db.SaveChangesAsync();
            return (false, "We could not send the verification SMS. Please check the phone number or try again later.", null);
        }

        var isSandbox = config.GetValue<bool>("AfricasTalking:Sandbox", false);
        var env = config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var returnOtp = isSandbox || env != "Production" ? rawOtp : null;

        return (true, null, returnOtp);
    }

    public async Task<(bool Success, string? Error, CustomerAuthResponse? Data)> RegisterAsync(
        CustomerRegisterRequest req, string ipAddress)
    {
        var otpError = SkipOtpVerification ? null : await VerifyOtpAsync(req.Phone, req.Otp, "registration");
        if (otpError != null) return (false, otpError, null);

        if (await db.Users.AnyAsync(u => u.Phone == req.Phone))
            return (false, "Phone number already registered.", null);

        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return (false, "Email address already registered.", null);

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "customer" && r.IsActive);
        if (role is null) return (false, "Role configuration error.", null);

        var user = new User
        {
            Phone = req.Phone,
            Email = req.Email,
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
            Age = req.Age,
            Allergies = JsonSerializer.Serialize(req.Allergies ?? [])
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
        await db.SaveChangesAsync();

        return (true, null, await IssueTokensAsync(user, role, profile, ipAddress));
    }

    public async Task<(bool Success, string? Error, CustomerAuthResponse? Data)> LoginAsync(
        CustomerLoginRequest req, string ipAddress)
    {
        var otpError = SkipOtpVerification ? null : await VerifyOtpAsync(req.Phone, req.Otp, "login");
        if (otpError != null) return (false, otpError, null);

        var user = await db.Users
            .Include(u => u.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Phone == req.Phone && u.Role.Name == "customer");

        if (user is null) return (false, "No account found for this number.", null);
        if (user.LockedUntil > DateTime.UtcNow) return (false, "Account locked. Please contact support.", null);

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

    private async Task<string?> VerifyOtpAsync(string phone, string rawOtp, string purpose)
    {
        var hashed = JwtHelper.HashToken(rawOtp);
        var otp = await db.OtpCodes.FirstOrDefaultAsync(o =>
            o.Phone == phone && o.Code == hashed && o.Purpose == purpose &&
            o.UsedAt == null && o.ExpiresAt > DateTime.UtcNow);

        if (otp is null) return "Invalid or expired OTP.";
        otp.UsedAt = DateTime.UtcNow;
        return null;
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
            allergies
        );
    }
}
