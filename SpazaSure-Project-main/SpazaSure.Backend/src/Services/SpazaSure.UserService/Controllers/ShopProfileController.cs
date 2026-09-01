using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;
using System.Globalization;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/shop/profile")]
[Authorize(Roles = "spaza_owner")]
public class ShopProfileController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get the logged-in spaza owner's profile
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var shop = await db.SpazaShops
            .Include(s => s.Documents)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == UserId);

        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));

        var configuredFee = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == AdminSettingsController.OnboardingFeeKey)
            .Select(s => s.Value)
            .SingleOrDefaultAsync();
        var onboardingFeeAmount = decimal.TryParse(configuredFee, NumberStyles.Number,
            CultureInfo.InvariantCulture, out var parsedFee)
            ? parsedFee
            : AdminSettingsController.DefaultOnboardingFee;

        return Ok(ApiResponse<object>.Ok(new
        {
            shop.Id,
            shop.ShopName,
            shop.OwnerName,
            shop.OwnerIdNumber,
            shop.Phone,
            Email = shop.Email ?? shop.User?.Email ?? "",
            shop.Address,
            shop.City,
            shop.Province,
            shop.PostalCode,
            shop.Latitude,
            shop.Longitude,
            shop.Status,
            shop.ComplianceStatus,
            shop.OnboardingFeePaid,
            OnboardingFeeAmount = onboardingFeeAmount,
            shop.RatingAvg,
            shop.RatingCount,
            JoinedAt = shop.CreatedAt,
            Documents = shop.Documents.Select(d => new
            {
                d.Id,
                d.DocType,
                d.DocUrl,
                d.Status,
                d.ExpiryDate,
                d.RejectionNote
            })
        }));
    }

    /// <summary>
    /// Update shop profile details
    /// </summary>
    [HttpPatch]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateShopProfileRequest req)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));

        var hasLatitude = req.Latitude.HasValue;
        var hasLongitude = req.Longitude.HasValue;
        if (hasLatitude && (!double.IsFinite(req.Latitude!.Value) || req.Latitude < -90 || req.Latitude > 90))
            return BadRequest(ApiResponse.Fail("Latitude must be between -90 and 90."));
        if (hasLongitude && (!double.IsFinite(req.Longitude!.Value) || req.Longitude < -180 || req.Longitude > 180))
            return BadRequest(ApiResponse.Fail("Longitude must be between -180 and 180."));

        if (req.ShopName != null) shop.ShopName = req.ShopName;
        if (req.OwnerName != null) shop.OwnerName = req.OwnerName;
        if (req.Phone != null) shop.Phone = req.Phone;
        if (req.Email != null) shop.Email = req.Email;
        if (req.Address != null) shop.Address = req.Address;
        if (req.City != null) shop.City = req.City;
        if (req.Province != null) shop.Province = req.Province;
        if (req.PostalCode != null) shop.PostalCode = req.PostalCode;
        if (hasLatitude) shop.Latitude = req.Latitude;
        if (hasLongitude) shop.Longitude = req.Longitude;

        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            shop.Latitude,
            shop.Longitude,
            Message = "Profile updated successfully."
        }));
    }

    /// <summary>
    /// Upload a compliance document (PDF, DOC, JPG, PNG)
    /// </summary>
    [HttpPost("documents")]
    [RequestSizeLimit(10_000_000)] // 10MB
    public async Task<IActionResult> UploadDocument([FromForm] string docType, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file provided."));

        var allowed = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest(ApiResponse.Fail("File type not allowed. Accepted: PDF, DOC, DOCX, JPG, PNG, WEBP."));

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("File must be under 10MB."));

        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));

        // Save file to uploads directory
        var uploadsDir = Path.Combine("uploads", "shop-docs", shop.Id.ToString());
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{docType}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        var docUrl = $"/uploads/shop-docs/{shop.Id}/{fileName}";

        // Update or create document record
        var existing = await db.ShopDocuments
            .FirstOrDefaultAsync(d => d.ShopId == shop.Id && d.DocType == docType);

        if (existing is not null)
        {
            existing.DocUrl = docUrl;
            existing.Status = "pending";
            existing.RejectionNote = null;
        }
        else
        {
            db.ShopDocuments.Add(new SpazaSure.Infrastructure.Entities.ShopDocument
            {
                ShopId = shop.Id,
                DocType = docType,
                DocUrl = docUrl,
                Status = "pending"
            });
        }

        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { docType, docUrl, status = "pending" }));
    }

    /// <summary>
    /// Get all documents for the logged-in shop owner
    /// </summary>
    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments()
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));

        var docs = await db.ShopDocuments
            .Where(d => d.ShopId == shop.Id)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.DocType,
                d.DocUrl,
                d.Status,
                d.ExpiryDate,
                d.RejectionNote,
                d.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(docs));
    }
}

public record UpdateShopProfileRequest(
    string? ShopName, string? OwnerName, string? Phone, string? Email,
    string? Address, string? City, string? Province, string? PostalCode,
    double? Latitude = null, double? Longitude = null
);
