using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Shared.Models;
using SpazaSure.Shared.Storage;

namespace SpazaSure.UserService.Controllers;

/// <summary>Uploads the poster image used on a group buy banner. The returned URL is sent when creating the group buy.</summary>
[ApiController]
[Route("api")]
public class GroupBuyBannerController(IFileStorageService storage) : ControllerBase
{
    private static readonly string[] Allowed = [".jpg", ".jpeg", ".png", ".webp"];

    [HttpPost("supplier/profile/group-buy-banner")]
    [Authorize(Roles = "supplier")]
    public Task<IActionResult> SupplierUpload(IFormFile banner) => Upload(banner);

    [HttpPost("admin/group-buy-banner")]
    [Authorize(Roles = "admin")]
    public Task<IActionResult> AdminUpload(IFormFile banner) => Upload(banner);

    private async Task<IActionResult> Upload(IFormFile? banner)
    {
        if (banner is null || banner.Length == 0) return BadRequest(ApiResponse.Fail("No file provided."));
        var ext = Path.GetExtension(banner.FileName).ToLowerInvariant();
        if (!Allowed.Contains(ext)) return BadRequest(ApiResponse.Fail("Only JPG, PNG and WEBP are allowed."));
        if (banner.Length > 5 * 1024 * 1024) return BadRequest(ApiResponse.Fail("Image must be under 5MB."));

        var key = $"group-buy-banners/{Guid.NewGuid():N}{ext}";
        await using var stream = banner.OpenReadStream();
        var url = await storage.SaveAsync(stream, key, banner.ContentType, HttpContext.RequestAborted);
        return Ok(ApiResponse<object>.Ok(new { bannerImageUrl = url }));
    }
}
