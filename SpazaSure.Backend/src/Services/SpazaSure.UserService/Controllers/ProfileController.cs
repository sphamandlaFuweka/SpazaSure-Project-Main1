using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;
using SpazaSure.Shared.Storage;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/supplier/profile")]
[Authorize]
public class ProfileController(SpazaSureDbContext db, IFileStorageService storage, EventPublisher events, AddressGeocodingSignal geocoding) : ControllerBase
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
                d.Id, d.DocType, DocUrl = SpazaSure.Shared.Helpers.PrivateFiles.Link(d.DocUrl), d.Status, d.ExpiryDate, d.RejectionNote,
                // The automatic check against the profile, so the supplier sees what to fix
                d.CheckStatus, d.CheckSummary
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
        var addressError = SpazaSure.Shared.Helpers.AddressValidation.ValidateCoordinates(req.Latitude, req.Longitude);
        if (addressError is not null) return BadRequest(ApiResponse.Fail(addressError));
        if (req.Phone != null && SaValidation.NormalizePhone(req.Phone) is null)
            return BadRequest(ApiResponse.Fail("Enter a valid South African phone number, e.g. 082 123 4567."));
        string? registration = null;
        if (!string.IsNullOrWhiteSpace(req.RegistrationNumber))
        {
            if (!SaValidation.IsValidCompanyRegistration(req.RegistrationNumber))
                return BadRequest(ApiResponse.Fail("Enter your CIPC registration number in the form 2020/123456/07."));
            registration = SaValidation.NormalizeCompanyRegistration(req.RegistrationNumber);
            if (await db.Suppliers.AnyAsync(s => s.Id != supplier.Id && s.RegistrationNumber == registration))
                return Conflict(ApiResponse.Fail("Another supplier is already registered with this CIPC number."));
        }
        // The business check confirmed the old company details, so changed ones have to be checked again.
        if ((registration != null && registration != supplier.RegistrationNumber) ||
            (req.CompanyName != null && req.CompanyName != supplier.CompanyName))
            IdentityChecks.Reset(supplier);

        var addressChanged = (req.Address != null && req.Address != supplier.Address)
            || (req.City != null && req.City != supplier.City)
            || (req.Province != null && req.Province != supplier.Province);

        supplier.CompanyName        = req.CompanyName        ?? supplier.CompanyName;
        supplier.ContactPerson      = req.ContactPerson      ?? supplier.ContactPerson;
        supplier.Phone              = req.Phone              ?? supplier.Phone;
        supplier.Email              = req.Email              ?? supplier.Email;
        supplier.Address            = req.Address            ?? supplier.Address;
        supplier.City               = req.City               ?? supplier.City;
        supplier.Province           = req.Province           ?? supplier.Province;
        supplier.PostalCode         = req.PostalCode         ?? supplier.PostalCode;
        supplier.RegistrationNumber = registration ?? supplier.RegistrationNumber;
        supplier.VatNumber          = req.VatNumber          ?? supplier.VatNumber;
        var bankChanged = (req.BankAccount != null && req.BankAccount != supplier.BankAccount)
            || (req.BankBranchCode != null && req.BankBranchCode != supplier.BankBranchCode);
        if (bankChanged)
        {
            // Payouts go to the new account: a new Paystack recipient, and failed payouts are retried
            supplier.PaystackRecipientCode = null;
            supplier.PaystackSubaccountCode = null;
            await SupplierPayoutResults.ClearFailuresAsync(db, supplier.Id, HttpContext.RequestAborted);
        }
        supplier.BankName           = req.BankName           ?? supplier.BankName;
        supplier.BankAccount        = req.BankAccount        ?? supplier.BankAccount;
        supplier.BankBranchCode     = req.BankBranchCode     ?? supplier.BankBranchCode;
        if (hasLatitude) supplier.Latitude = req.Latitude;
        if (hasLongitude) supplier.Longitude = req.Longitude;
        if (addressChanged && !hasLatitude)
        {
            // New address typed without picking it on the map: look it up again
            supplier.Latitude = null;
            supplier.Longitude = null;
        }
        await db.SaveChangesAsync();
        if (supplier.Latitude is null) geocoding.RequestRun();
        return Ok(ApiResponse<object>.Ok(new
        {
            supplier.Latitude,
            supplier.Longitude,
            Message = "Profile updated."
        }));
    }

    // Note: tier changes are only made by the Paystack subscription flow (PaymentController).
    // A self-service "PATCH tier" endpoint used to live here and let any supplier grant
    // themselves a paid tier and lower commission without paying, so it was removed.
    [HttpPost("documents")]
    [RequestSizeLimit(10_000_000)] // 10MB
    public async Task<IActionResult> UploadDocument([FromForm] string docType, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file provided."));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext))
            return BadRequest(ApiResponse.Fail("File type not allowed. Accepted: PDF, JPG, PNG, WEBP."));
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("File must be under 10MB."));

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var fileName = $"{docType}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var key = $"supplier-docs/{supplier.Id}/{fileName}";
        // Kept in memory for the automatic check against the profile, which runs after this request
        byte[] bytes;
        await using (var read = file.OpenReadStream())
        using (var buffer = new MemoryStream())
        {
            await read.CopyToAsync(buffer, HttpContext.RequestAborted);
            bytes = buffer.ToArray();
        }
        await using (var stream = new MemoryStream(bytes))
        {
            var docUrl = await storage.SaveAsync(stream, key, file.ContentType, HttpContext.RequestAborted);
            var existing = await db.SupplierDocuments
                .FirstOrDefaultAsync(d => d.SupplierId == supplier.Id && d.DocType == docType);
            var resubmitted = existing?.Status == "rejected";
            var replacedUrl = existing?.DocUrl;
            var document = existing ?? db.SupplierDocuments.Add(new SupplierDocument { SupplierId = supplier.Id, DocType = docType }).Entity;
            document.DocUrl = docUrl;
            document.Status = "pending";
            document.RejectionNote = null;
            document.CheckStatus = "checking";
            document.CheckSummary = null;
            document.CheckDetails = null;
            document.CheckedAt = null;
            await db.SaveChangesAsync();
            var (documentId, uploadedName) = (document.Id, file.FileName);
            DocumentChecks.Queue(HttpContext?.RequestServices, c => c.CheckSupplierDocumentAsync(documentId, bytes, uploadedName));
            // Don't keep the replaced copy of the supplier's documents (POPIA s14: no longer than needed).
            if (!string.IsNullOrEmpty(replacedUrl) && replacedUrl != docUrl)
                await storage.DeleteAsync(replacedUrl, HttpContext.RequestAborted);
            await AdminAlerts.NotifyAdminsAsync(db, events, "compliance",
                resubmitted ? "Rejected document resubmitted" : "Document waiting for review",
                $"{supplier.CompanyName} {(resubmitted ? "re-uploaded" : "uploaded")} a {docType.Replace('_', ' ')} document.", supplier.Id.ToString());
            return Ok(ApiResponse<object>.Ok(new { docType, docUrl = SpazaSure.Shared.Helpers.PrivateFiles.Link(docUrl), status = "pending", checkStatus = "checking" }));
        }
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
            await storage.DeleteAsync(supplier.LogoUrl, HttpContext.RequestAborted);
        }
        var key = $"supplier-logos/{supplier.Id}{ext}";
        await using (var stream = logo.OpenReadStream())
        {
            supplier.LogoUrl = await storage.SaveAsync(stream, key, logo.ContentType, HttpContext.RequestAborted);
        }
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
