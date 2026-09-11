using Microsoft.EntityFrameworkCore;
using SpazaSure.AuthService.DTOs;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.AuthService.Services;

public class ShopAuthService(SpazaSureDbContext db, IConfiguration config, ILogger<ShopAuthService> logger, SmsService sms)
{
    private readonly string _jwtSecret = config["Jwt:Secret"]!;
    private readonly int _accessExpiry = int.Parse(config["Jwt:AccessExpiryMinutes"] ?? "60");
    private readonly int _refreshExpiry = int.Parse(config["Jwt:RefreshExpiryDays"] ?? "30");

    // ── Step 1: Send OTP ──────────────────────────────────────────────────────
    public async Task<(bool Success, string? Error, string? DevOtp)> SendOtpAsync(string phone, string purpose)
    {
        // Invalidate any existing unused OTPs for this phone + purpose
        var existing = await db.OtpCodes
            .Where(o => o.Phone == phone && o.Purpose == purpose && o.UsedAt == null)
            .ToListAsync();
        db.OtpCodes.RemoveRange(existing);

        // Generate 6-digit OTP
        var rawOtp = Random.Shared.Next(100_000, 999_999).ToString();
        var hashed = JwtHelper.HashToken(rawOtp);

        db.OtpCodes.Add(new OtpCode
        {
            Phone = phone,
            Code = hashed,
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });

        await db.SaveChangesAsync();

        // Send OTP via Africa's Talking SMS
        // DEV: Also log raw OTP to console for testing
        logger.LogWarning("[DEV-TEST] Raw OTP for {Phone}: {Otp}", phone, rawOtp);
        Console.WriteLine($"\n========================================");
        Console.WriteLine($"[DEV-TEST] OTP for {phone}: {rawOtp}");
        Console.WriteLine($"========================================\n");

        var sent = await sms.SendOtpAsync(phone, rawOtp);
        if (!sent)
            logger.LogWarning("SMS delivery failed for {Phone} — OTP still stored", phone);

        // Return raw OTP for non-production environments (auto-fill in app)
        var isSandbox = config.GetValue<bool>("AfricasTalking:Sandbox", false);
        var env = config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var returnOtp = isSandbox || env != "Production" ? rawOtp : null;

        return (true, null, returnOtp);
    }

    // ── Step 2a: Register new spaza shop ─────────────────────────────────────
    public async Task<(bool Success, string? Error, ShopAuthResponse? Data)> RegisterAsync(
        ShopRegisterRequest req, string ipAddress)
    {
        // Verify OTP
        var otpError = await VerifyOtpAsync(req.Phone, req.Otp, "registration");
        if (otpError != null) return (false, otpError, null);

        // Check phone not already registered
        if (await db.Users.AnyAsync(u => u.Phone == req.Phone))
            return (false, "Phone number already registered.", null);

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "spaza_owner" && r.IsActive);
        if (role is null) return (false, "Role configuration error.", null);

        var user = new User
        {
            Phone = req.Phone,
            RoleId = role.Id,
            Status = "active"
        };
        db.Users.Add(user);

        var shop = new SpazaShop
        {
            UserId = user.Id,
            ShopName = req.ShopName,
            OwnerName = req.FullName,
            OwnerIdNumber = req.IdNumber,
            Phone = req.Phone,
            Address = req.Address
        };
        db.SpazaShops.Add(shop);

        // Save user and shop first so FK constraint is satisfied
        await db.SaveChangesAsync();

        db.AuthAuditLogs.Add(new AuthAuditLog
        {
            UserId = user.Id,
            Event = "shop_register",
            IpAddress = ipAddress
        });

        await db.SaveChangesAsync();
        return (true, null, await IssueTokensAsync(user, role, shop, ipAddress));
    }

    // ── Step 2b: Login existing spaza shop ────────────────────────────────────
    public async Task<(bool Success, string? Error, ShopAuthResponse? Data)> LoginAsync(
        ShopLoginRequest req, string ipAddress)
    {
        // Verify OTP
        var otpError = await VerifyOtpAsync(req.Phone, req.Otp, "login");
        if (otpError != null) return (false, otpError, null);

        var user = await db.Users
            .Include(u => u.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(u => u.SpazaShop)
            .FirstOrDefaultAsync(u => u.Phone == req.Phone && u.Role.Name == "spaza_owner");

        if (user is null) return (false, "No account found for this number.", null);

        if (user.LockedUntil > DateTime.UtcNow)
            return (false, "Account locked. Please contact support.", null);

        user.FailedAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;

        db.AuthAuditLogs.Add(new AuthAuditLog
        {
            UserId = user.Id,
            Event = "shop_login",
            IpAddress = ipAddress
        });

        await db.SaveChangesAsync();
        return (true, null, await IssueTokensAsync(user, user.Role, user.SpazaShop, ipAddress));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<string?> VerifyOtpAsync(string phone, string rawOtp, string purpose)
    {
        var hashed = JwtHelper.HashToken(rawOtp);
        var otp = await db.OtpCodes.FirstOrDefaultAsync(o =>
            o.Phone == phone &&
            o.Code == hashed &&
            o.Purpose == purpose &&
            o.UsedAt == null &&
            o.ExpiresAt > DateTime.UtcNow);

        if (otp is null) return "Invalid or expired OTP.";

        otp.UsedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<ShopAuthResponse> IssueTokensAsync(
        User user, Role role, SpazaShop? shop, string ipAddress)
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

        return new ShopAuthResponse(
            accessToken,
            rawRefresh,
            DateTime.UtcNow.AddMinutes(_accessExpiry),
            user.Id,
            shop?.ShopName ?? string.Empty,
            user.Phone ?? string.Empty
        );
    }
}
