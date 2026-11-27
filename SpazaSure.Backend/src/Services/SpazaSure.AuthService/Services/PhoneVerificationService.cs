using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.AuthService.Services;

public sealed record PhoneVerificationSendResult(bool Success, string Message, int StatusCode);

public interface ISmsSender
{
    Task SendAsync(string phone, string message, CancellationToken cancellationToken = default);
}

public sealed class SmsDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class PhoneVerificationService(
    SpazaSureDbContext db,
    IConfiguration config,
    ISmsSender smsSender,
    ILogger<PhoneVerificationService> logger)
{
    private const string Purpose = "phone_registration";
    private const string GenericSentMessage = "If this number can be verified, a code has been sent.";
    private const string InvalidCodeMessage = "The verification code is invalid or expired.";
    private readonly string _hashKey = config["PhoneVerification:HashKey"] ?? string.Empty;

    public bool IsEnabled => config.GetValue<bool>("PhoneVerification:Enabled");

    public async Task<PhoneVerificationSendResult> SendCodeAsync(
        string? phone, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return new(false, "Phone verification is not enabled.", StatusCodes.Status503ServiceUnavailable);

        var normalizedPhone = SaValidation.NormalizePhone(phone);
        if (normalizedPhone is null)
            return new(false, "Enter a valid South African mobile number.", StatusCodes.Status400BadRequest);

        if (await db.Users.AnyAsync(user => user.Phone == normalizedPhone, cancellationToken))
            return new(true, GenericSentMessage, StatusCodes.Status200OK);

        var now = DateTime.UtcNow;
        var recentCodes = db.OtpCodes.Where(code =>
            code.Phone == normalizedPhone &&
            code.Purpose == Purpose &&
            code.CreatedAt >= now.AddDays(-1));

        if (await recentCodes.CountAsync(cancellationToken) >= 5)
            return new(false, "Too many verification codes requested. Try again later.", StatusCodes.Status429TooManyRequests);

        if (await recentCodes.AnyAsync(code => code.CreatedAt >= now.AddSeconds(-60), cancellationToken))
            return new(false, "Wait a minute before requesting another verification code.", StatusCodes.Status429TooManyRequests);

        var existingCodes = await db.OtpCodes
            .Where(code => code.Phone == normalizedPhone && code.Purpose == Purpose && code.UsedAt == null)
            .ToListAsync(cancellationToken);
        db.OtpCodes.RemoveRange(existingCodes);

        var rawCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otp = new OtpCode
        {
            Phone = normalizedPhone,
            Code = HashCode(normalizedPhone, rawCode),
            Purpose = Purpose,
            ExpiresAt = now.AddMinutes(5)
        };
        db.OtpCodes.Add(otp);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await smsSender.SendAsync(
                normalizedPhone,
                $"Your SpazaSure sign-up code is {rawCode}. It expires in 5 minutes. Never share it.",
                cancellationToken);
        }
        catch (SmsDeliveryException ex)
        {
            db.OtpCodes.Remove(otp);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "Africa's Talking could not deliver a registration code.");
            return new(false, "Could not send a verification code. Please try again later.", StatusCodes.Status502BadGateway);
        }

        return new(true, GenericSentMessage, StatusCodes.Status200OK);
    }

    public async Task<(bool Success, string? Error)> ConsumeRegistrationCodeAsync(
        string phone, string? rawCode, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return (true, null);

        var normalizedPhone = SaValidation.NormalizePhone(phone);
        if (normalizedPhone is null) return (false, "Enter a valid South African mobile number.");

        var now = DateTime.UtcNow;
        var otp = await db.OtpCodes
            .Where(code =>
                code.Phone == normalizedPhone &&
                code.Purpose == Purpose &&
                code.UsedAt == null &&
                code.ExpiresAt > now)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null || otp.FailedAttempts >= 5)
            return (false, InvalidCodeMessage);

        var candidate = rawCode?.Trim() ?? string.Empty;
        var validFormat = Regex.IsMatch(candidate, @"^\d{6}$");
        var suppliedHash = validFormat ? HashCode(normalizedPhone, candidate) : string.Empty;
        var storedBytes = Encoding.UTF8.GetBytes(otp.Code);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedHash);
        var matches = validFormat &&
                      storedBytes.Length == suppliedBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(storedBytes, suppliedBytes);

        if (!matches)
        {
            otp.FailedAttempts++;
            if (otp.FailedAttempts >= 5) otp.UsedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return (false, InvalidCodeMessage);
        }

        otp.UsedAt = now;
        return (true, null);
    }

    private string HashCode(string phone, string code)
    {
        var key = Encoding.UTF8.GetBytes(_hashKey);
        var data = Encoding.UTF8.GetBytes($"{phone}:{code}");
        return Convert.ToHexString(HMACSHA256.HashData(key, data));
    }
}
