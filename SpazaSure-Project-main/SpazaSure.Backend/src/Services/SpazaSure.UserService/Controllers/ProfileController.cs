using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/supplier/profile")]
[Authorize]
public class ProfileController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var supplier = await db.Suppliers
            .Include(s => s.Documents)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        return Ok(ApiResponse<object>.Ok(new {
            supplier.Id, supplier.CompanyName, supplier.RegistrationNumber, supplier.VatNumber,
            supplier.ContactPerson, supplier.Phone, supplier.Email, supplier.Address,
            supplier.City, supplier.Province, supplier.PostalCode,
            supplier.Latitude, supplier.Longitude, supplier.Tier, supplier.Status,
            supplier.BankName, supplier.BankAccount, supplier.BankBranchCode,
            supplier.LogoUrl, supplier.CommissionRate,
            Documents = supplier.Documents.Select(d => new {
                d.Id, d.DocType, d.DocUrl, d.Status, d.ExpiryDate, d.RejectionNote
            })
        }));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateProfileRequest req)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var hasLatitude = req.Latitude.HasValue;
        var hasLongitude = req.Longitude.HasValue;
        if (hasLatitude && (!double.IsFinite(req.Latitude!.Value) || req.Latitude < -90 || req.Latitude > 90))
            return BadRequest(ApiResponse.Fail("Latitude must be between -90 and 90."));
        if (hasLongitude && (!double.IsFinite(req.Longitude!.Value) || req.Longitude < -180 || req.Longitude > 180))
            return BadRequest(ApiResponse.Fail("Longitude must be between -180 and 180."));

        supplier.CompanyName        = req.CompanyName        ?? supplier.CompanyName;
        supplier.ContactPerson      = req.ContactPerson      ?? supplier.ContactPerson;
        supplier.Phone              = req.Phone              ?? supplier.Phone;
        supplier.Email              = req.Email              ?? supplier.Email;
        supplier.Address            = req.Address            ?? supplier.Address;
        supplier.City               = req.City               ?? supplier.City;
        supplier.Province           = req.Province           ?? supplier.Province;
        supplier.PostalCode         = req.PostalCode         ?? supplier.PostalCode;
        supplier.RegistrationNumber = req.RegistrationNumber ?? supplier.RegistrationNumber;
        supplier.VatNumber          = req.VatNumber          ?? supplier.VatNumber;
        supplier.BankName           = req.BankName           ?? supplier.BankName;
        supplier.BankAccount        = req.BankAccount        ?? supplier.BankAccount;
        supplier.BankBranchCode     = req.BankBranchCode     ?? supplier.BankBranchCode;
        if (hasLatitude) supplier.Latitude = req.Latitude;
        if (hasLongitude) supplier.Longitude = req.Longitude;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            supplier.Latitude,
            supplier.Longitude,
            Message = "Profile updated."
        }));
    }

    [HttpPatch("tier")]
    public async Task<IActionResult> UpdateTier([FromBody] UpdateTierRequest req)
    {
        var valid = new[] { "basic", "bronze", "silver", "gold" };
        if (string.IsNullOrWhiteSpace(req.Tier) || !valid.Contains(req.Tier.ToLower()))
            return BadRequest(ApiResponse.Fail("Invalid tier. Must be basic, bronze, silver or gold."));
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var map = new Dictionary<string, decimal>
        {
            ["basic"] = 5m, ["bronze"] = 4m, ["silver"] = 3m, ["gold"] = 2m
        };
        supplier.Tier           = req.Tier.ToLower();
        supplier.CommissionRate = map[supplier.Tier];
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { tier = supplier.Tier, commissionRate = supplier.CommissionRate }));
    }

    [HttpPost("documents")]
    public async Task<IActionResult> UploadDocument([FromForm] string docType, IFormFile file)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var uploadsDir = Path.Combine("uploads", "supplier-docs", supplier.Id.ToString());
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{docType}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
        await using var stream = System.IO.File.Create(Path.Combine(uploadsDir, fileName));
        await file.CopyToAsync(stream);
        var docUrl = $"/uploads/supplier-docs/{supplier.Id}/{fileName}";
        var existing = await db.SupplierDocuments
            .FirstOrDefaultAsync(d => d.SupplierId == supplier.Id && d.DocType == docType);
        if (existing is not null)
        {
            existing.DocUrl = docUrl; existing.Status = "pending"; existing.RejectionNote = null;
        }
        else
        {
            db.SupplierDocuments.Add(new SupplierDocument {
                SupplierId = supplier.Id, DocType = docType, DocUrl = docUrl, Status = "pending"
            });
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { docType, docUrl, status = "pending" }));
    }

    [HttpPost("logo")]
    public async Task<IActionResult> UploadLogo(IFormFile logo)
    {
        if (logo is null || logo.Length == 0) return BadRequest(ApiResponse.Fail("No file provided."));
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) return BadRequest(ApiResponse.Fail("Only JPG, PNG and WEBP are allowed."));
        if (logo.Length > 5 * 1024 * 1024) return BadRequest(ApiResponse.Fail("Image must be under 5MB."));
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        if (!string.IsNullOrEmpty(supplier.LogoUrl))
        {
            var old = Path.Combine("uploads", supplier.LogoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
        }
        Directory.CreateDirectory(Path.Combine("uploads", "supplier-logos"));
        var fn = $"{supplier.Id}{ext}";
        await using var s2 = System.IO.File.Create(Path.Combine("uploads", "supplier-logos", fn));
        await logo.CopyToAsync(s2);
        supplier.LogoUrl = $"/uploads/supplier-logos/{fn}";
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { logoUrl = supplier.LogoUrl }));
    }
}

public record UpdateProfileRequest(
    string? CompanyName, string? ContactPerson, string? Phone, string? Email,
    string? Address, string? City, string? Province, string? PostalCode,
    string? RegistrationNumber, string? VatNumber,
    string? BankName, string? BankAccount, string? BankBranchCode,
    double? Latitude = null, double? Longitude = null
);

public record UpdateTierRequest(string Tier);
