using System.ComponentModel.DataAnnotations;

namespace SpazaSure.AuthService.DTOs;

public record RegisterRequest(
    [Required] string Role,
    string? Email,
    string? Phone,
    [Required, MinLength(8)] string Password,
    string? CompanyName,
    string? ContactPerson,
    string? ShopName,
    string? OwnerName,
    string? Address
);

public record LoginRequest(
    string? Email,
    string? Phone,
    [Required] string Password
);

public record RefreshRequest([Required] string RefreshToken);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required] string Email,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string Role,
    Guid UserId
);

// ── Shop Auth DTOs ──────────────────────────────────────────────────────────

public record SendOtpRequest([Required, Phone] string Phone);

public record ShopLoginRequest(
    [Required, Phone] string Phone,
    [Required, Length(6, 6)] string Otp
);

public record ShopRegisterRequest(
    [Required, Phone] string Phone,
    [Required, Length(6, 6)] string Otp,
    [Required] string FullName,
    [Required] string ShopName,
    [Required] string Address,
    string? IdNumber
);

public record ShopAuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string ShopName,
    string Phone
);
