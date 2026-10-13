using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using System.Security.Claims;
using System.Text.Json;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// The customer mobile app's own Profile tab — deliberately separate from
/// ShopProfileController, since a customer isn't a shop: first/last name,
/// email, phone, age, and declared allergies, not shop/compliance fields.
/// </summary>
[ApiController]
[Route("api/customer/profile")]
[Authorize(Roles = "customer")]
public class CustomerProfileController(SpazaSureDbContext db, SpazaSure.Shared.Storage.IFileStorageService storage) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record UpdateCustomerProfileRequest(
        string? FirstName,
        string? LastName,
        int? Age,
        List<string>? Allergies,
        string? Email = null,
        string? PreferredName = null,
        string? PreferredLanguage = null,
        string? Province = null,
        string? City = null,
        string? Suburb = null,
        bool? NearbyRecommendationsEnabled = null,
        double? Latitude = null,
        double? Longitude = null,
        List<string>? Interests = null,
        List<string>? NotificationPreferences = null,
        bool? MarketingOptIn = null
    );

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await db.Users
            .Include(u => u.CustomerProfile)
            .FirstOrDefaultAsync(u => u.Id == UserId);

        if (user?.CustomerProfile is null) return NotFound(ApiResponse.Fail("Customer profile not found."));

        List<string> allergies;
        try { allergies = JsonSerializer.Deserialize<List<string>>(user.CustomerProfile.Allergies) ?? []; }
        catch (JsonException) { allergies = []; }

        return Ok(ApiResponse<object>.Ok(new
        {
            user.Id,
            user.CustomerProfile.FirstName,
            user.CustomerProfile.LastName,
            FullName = $"{user.CustomerProfile.FirstName} {user.CustomerProfile.LastName}".Trim(),
            user.Email,
            user.Phone,
            ProfilePhotoUrl = user.ProfilePhotoUrl,
            user.CustomerProfile.Age,
            Allergies = allergies,
            user.CustomerProfile.PreferredName,
            user.CustomerProfile.PreferredLanguage,
            user.CustomerProfile.Province,
            user.CustomerProfile.City,
            user.CustomerProfile.Suburb,
            user.CustomerProfile.NearbyRecommendationsEnabled,
            user.CustomerProfile.Latitude,
            user.CustomerProfile.Longitude,
            Interests = DeserializeList(user.CustomerProfile.Interests),
            NotificationPreferences = DeserializeList(
                user.CustomerProfile.NotificationPreferences),
            user.CustomerProfile.MarketingOptIn,
            JoinedAt = user.CreatedAt,
        }));
    }

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateCustomerProfileRequest req)
    {
        var profile = await db.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == UserId);
        if (profile is null) return NotFound(ApiResponse.Fail("Customer profile not found."));

        if (req.Age is < 0 or > 130) return BadRequest(ApiResponse.Fail("Age must be between 0 and 130."));
        if (req.Latitude is < -90 or > 90 || req.Longitude is < -180 or > 180)
            return BadRequest(ApiResponse.Fail("Enter a valid location."));
        if (req.Latitude.HasValue != req.Longitude.HasValue)
            return BadRequest(ApiResponse.Fail("Latitude and longitude must be provided together."));

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var email = req.Email.Trim();
            if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
                return BadRequest(ApiResponse.Fail("Enter a valid email address."));
            if (await db.Users.AnyAsync(u => u.Id != UserId && u.Email != null && u.Email.ToLower() == email.ToLower()))
                return BadRequest(ApiResponse.Fail("That email address is already in use."));
            var owner = await db.Users.FirstAsync(u => u.Id == UserId);
            owner.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(req.FirstName)) profile.FirstName = req.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(req.LastName)) profile.LastName = req.LastName.Trim();
        if (req.PreferredName is not null)
            profile.PreferredName = string.IsNullOrWhiteSpace(req.PreferredName)
                ? null
                : req.PreferredName.Trim();
        if (!string.IsNullOrWhiteSpace(req.PreferredLanguage))
            profile.PreferredLanguage = req.PreferredLanguage.Trim();
        if (req.Province is not null) profile.Province = NormalizeOptional(req.Province);
        if (req.City is not null) profile.City = NormalizeOptional(req.City);
        if (req.Suburb is not null) profile.Suburb = NormalizeOptional(req.Suburb);
        if (req.NearbyRecommendationsEnabled.HasValue)
            profile.NearbyRecommendationsEnabled = req.NearbyRecommendationsEnabled.Value;
        if (req.Latitude.HasValue && req.Longitude.HasValue)
        {
            profile.Latitude = req.Latitude;
            profile.Longitude = req.Longitude;
        }
        else if (req.NearbyRecommendationsEnabled == false)
        {
            profile.Latitude = null;
            profile.Longitude = null;
        }
        if (req.Age.HasValue) profile.Age = req.Age;
        if (req.Allergies is not null) profile.Allergies = JsonSerializer.Serialize(req.Allergies);
        if (req.Interests is not null) profile.Interests = JsonSerializer.Serialize(req.Interests);
        if (req.NotificationPreferences is not null)
            profile.NotificationPreferences = JsonSerializer.Serialize(
                req.NotificationPreferences);
        if (req.MarketingOptIn.HasValue)
            profile.MarketingOptIn = req.MarketingOptIn.Value;

        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Profile updated."));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> DeserializeList(string? value)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(value ?? "[]") ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    [HttpPost("photo")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> UploadPhoto(IFormFile file)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == UserId);
        if (user is null) return NotFound(ApiResponse.Fail("User not found."));

        var (url, error) = await SpazaSure.UserService.Services.ProfilePhotoUploader
            .SaveAsync(storage, user, file, HttpContext.RequestAborted);
        if (url is null) return BadRequest(ApiResponse.Fail(error!));

        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { profilePhotoUrl = url }, "Profile picture updated."));
    }
}
