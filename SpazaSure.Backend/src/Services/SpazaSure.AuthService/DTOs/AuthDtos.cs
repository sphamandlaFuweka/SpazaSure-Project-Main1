using System.ComponentModel.DataAnnotations;
using System.Text.Json;

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
    string? Address,
    string? City = null,
    string? Province = null,
    string? PostalCode = null,
    double? Latitude = null,
    double? Longitude = null,
    bool AcceptedPrivacyNotice = false
);

public record LoginRequest(
    string? Email,
    string? Phone,
    [Required] string Password,
    // Which portal is signing in: "admin" for the admin console, anything else for the supplier portal
    string? Role = null
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

public record ShopLoginRequest(
    [Required, Phone] string Phone,
    [Required, MinLength(8)] string Password
);

public record ShopRegisterRequest(
    [Required, Phone] string Phone,
    [Required, MinLength(8)] string Password,
    [Required] string FullName,
    [Required] string ShopName,
    [Required] string Address,
    string? IdNumber,
    string? City = null,
    string? Province = null,
    string? PostalCode = null,
    double? Latitude = null,
    double? Longitude = null,
    string? Email = null,
    JsonElement? RegistrationDetails = null,
    bool AcceptedPrivacyNotice = false
);

public record ShopAuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string ShopName,
    string Phone
);

// ── Customer Auth DTOs ──────────────────────────────────────────────────────

public record CustomerLoginRequest(
    [Required, Phone] string Phone,
    [Required, MinLength(8)] string Password
);

public record CustomerRegisterRequest(
    [Required, Phone] string Phone,
    [Required] string FirstName,
    [Required] string LastName,
    [Required, MinLength(8)] string Password,
    [Range(18, 130)] int? Age,
    List<string>? Allergies,
    [EmailAddress] string? Email = null,
    string? PreferredName = null,
    string? PreferredLanguage = null,
    string? Province = null,
    string? City = null,
    string? Suburb = null,
    bool NearbyRecommendationsEnabled = false,
    [Range(-90, 90)] double? Latitude = null,
    [Range(-180, 180)] double? Longitude = null,
    List<string>? Interests = null,
    List<string>? NotificationPreferences = null,
    bool MarketingOptIn = false,
    bool AcceptedPrivacyNotice = false
);

public record CustomerAuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string Phone,
    int? Age,
    List<string> Allergies,
    string? PreferredName = null,
    string PreferredLanguage = "English",
    string? Province = null,
    string? City = null,
    string? Suburb = null,
    bool NearbyRecommendationsEnabled = false,
    double? Latitude = null,
    double? Longitude = null,
    List<string>? Interests = null,
    List<string>? NotificationPreferences = null,
    bool MarketingOptIn = false
);
