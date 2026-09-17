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
public class CustomerProfileController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record UpdateCustomerProfileRequest(
        string? FirstName,
        string? LastName,
        int? Age,
        List<string>? Allergies
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
            user.CustomerProfile.Age,
            Allergies = allergies,
            JoinedAt = user.CreatedAt,
        }));
    }

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateCustomerProfileRequest req)
    {
        var profile = await db.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == UserId);
        if (profile is null) return NotFound(ApiResponse.Fail("Customer profile not found."));

        if (req.Age is < 0 or > 130) return BadRequest(ApiResponse.Fail("Age must be between 0 and 130."));

        if (!string.IsNullOrWhiteSpace(req.FirstName)) profile.FirstName = req.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(req.LastName)) profile.LastName = req.LastName.Trim();
        if (req.Age.HasValue) profile.Age = req.Age;
        if (req.Allergies is not null) profile.Allergies = JsonSerializer.Serialize(req.Allergies);

        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Profile updated."));
    }
}
