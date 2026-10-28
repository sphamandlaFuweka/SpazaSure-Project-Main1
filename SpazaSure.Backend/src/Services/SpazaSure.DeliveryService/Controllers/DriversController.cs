using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using SpazaSure.Shared.Storage;

namespace SpazaSure.DeliveryService.Controllers;

/// <summary>
/// Admins register the drivers SpazaSure delivers with. Each driver gets a private link to share
/// their location while delivering, and a badge code shops can check at the door.
/// </summary>
[ApiController]
[Route("api/admin/drivers")]
[Authorize(Roles = "admin")]
public class DriversController(SpazaSureDbContext db, IConfiguration config, IFileStorageService storage) : ControllerBase
{
    /// <summary>Orders a driver is still busy with.</summary>
    internal static readonly string[] OpenStatuses = ["pending", "confirmed", "processing", "ready", "dispatched"];

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = true)
    {
        var drivers = await db.Drivers.AsNoTracking()
            .Include(d => d.Documents)
            .Where(d => includeInactive || d.IsActive)
            .OrderByDescending(d => d.IsActive).ThenBy(d => d.FullName)
            .ToListAsync();
        var ids = drivers.Select(d => d.Id).ToList();
        var openJobs = await db.Orders.AsNoTracking()
            .Where(o => o.DriverId != null && ids.Contains(o.DriverId.Value) && OpenStatuses.Contains(o.Status))
            .GroupBy(o => o.DriverId!.Value)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DriverId, x => x.Count);
        return Ok(ApiResponse<object>.Ok(drivers.Select(d => Project(d, openJobs.GetValueOrDefault(d.Id))).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DriverRequest req)
    {
        var (error, phone) = Validate(req);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));
        if (await db.Drivers.AnyAsync(d => d.Phone == phone))
            return Conflict(ApiResponse.Fail("A driver with this phone number is already registered."));

        var driver = new Driver { AccessToken = NewToken(), BadgeCode = await NewBadgeCodeAsync() };
        Apply(driver, req, phone!);
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(Project(driver, 0), "Driver registered. Send them their link so they can share their location."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] DriverRequest req)
    {
        var driver = await db.Drivers.Include(d => d.Documents).FirstOrDefaultAsync(d => d.Id == id);
        if (driver is null) return NotFound(ApiResponse.Fail("Driver not found."));
        var (error, phone) = Validate(req);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));
        if (await db.Drivers.AnyAsync(d => d.Id != id && d.Phone == phone))
            return Conflict(ApiResponse.Fail("Another driver already uses this phone number."));

        Apply(driver, req, phone!);
        driver.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(Project(driver, await OpenJobsAsync(id)), "Driver updated."));
    }

    /// <summary>Makes the old link stop working, e.g. when a driver's phone is lost or they leave.</summary>
    [HttpPost("{id:guid}/reset-link")]
    public async Task<IActionResult> ResetLink(Guid id)
    {
        var driver = await db.Drivers.Include(d => d.Documents).FirstOrDefaultAsync(d => d.Id == id);
        if (driver is null) return NotFound(ApiResponse.Fail("Driver not found."));
        driver.AccessToken = NewToken();
        driver.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(Project(driver, await OpenJobsAsync(id)), "New link created. The old one no longer works."));
    }

    /// <summary>
    /// Uploads (or replaces) the driver's licence, PrDP or ID. The licence and PrDP need their expiry
    /// date, and a driver isn't allocated deliveries while either is missing or expired.
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(10_000_000)] // 10MB
    public async Task<IActionResult> UploadDocument(Guid id, [FromForm] string? docType, [FromForm] string? expiryDate, IFormFile? file)
    {
        var driver = await db.Drivers.Include(d => d.Documents).FirstOrDefaultAsync(d => d.Id == id);
        if (driver is null) return NotFound(ApiResponse.Fail("Driver not found."));

        docType = docType?.Trim().ToLowerInvariant();
        if (docType is null || !DriverDocument.Types.Contains(docType))
            return BadRequest(ApiResponse.Fail("Choose the driving licence, PrDP or ID document."));
        if (file is null || file.Length == 0) return BadRequest(ApiResponse.Fail("Choose a file to upload."));
        if (file.Length > 10 * 1024 * 1024) return BadRequest(ApiResponse.Fail("File must be under 10MB."));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(ApiResponse.Fail("Upload a PDF, JPG, PNG or WEBP file."));

        DateOnly? expiry = null;
        if (docType != DriverDocument.IdDocument)
        {
            if (!DateOnly.TryParseExact(expiryDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return BadRequest(ApiResponse.Fail($"Enter the {DriverPaperwork.Label(docType)}'s expiry date."));
            if (parsed <= DriverPaperwork.Today)
                return BadRequest(ApiResponse.Fail($"This {DriverPaperwork.Label(docType)} has already expired. Upload a valid one."));
            expiry = parsed;
        }

        // Kept in memory for the automatic check against the driver's name and the typed expiry date
        byte[] bytes;
        await using (var read = file.OpenReadStream())
        using (var buffer = new MemoryStream())
        {
            await read.CopyToAsync(buffer, HttpContext.RequestAborted);
            bytes = buffer.ToArray();
        }
        string url;
        await using (var stream = new MemoryStream(bytes))
            url = await storage.SaveAsync(stream, $"driver-docs/{driver.Id}/{docType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}",
                file.ContentType, HttpContext.RequestAborted);

        var document = driver.Documents.FirstOrDefault(d => d.DocType == docType);
        var replaced = document?.DocUrl;
        if (document is null)
        {
            document = new DriverDocument { DriverId = driver.Id, DocType = docType };
            db.DriverDocuments.Add(document);
            driver.Documents.Add(document);
        }
        document.DocUrl = url;
        document.ExpiryDate = expiry;
        document.UpdatedAt = DateTime.UtcNow;
        document.CheckStatus = "checking";
        document.CheckSummary = null;
        document.CheckDetails = null;
        document.CheckedAt = null;
        await db.SaveChangesAsync();
        var (documentId, uploadedName) = (document.Id, file.FileName);
        DocumentChecks.Queue(HttpContext?.RequestServices, c => c.CheckDriverDocumentAsync(documentId, bytes, uploadedName));
        // Don't keep the old copy of someone's documents (POPIA s14: no longer than needed)
        if (!string.IsNullOrEmpty(replaced) && replaced != url)
            await storage.DeleteAsync(replaced, HttpContext.RequestAborted);

        return Ok(ApiResponse<object>.Ok(Project(driver, await OpenJobsAsync(id)), $"{DriverPaperwork.Label(docType)} uploaded."));
    }

    private Task<int> OpenJobsAsync(Guid driverId) =>
        db.Orders.CountAsync(o => o.DriverId == driverId && OpenStatuses.Contains(o.Status));

    private static (string? Error, string? Phone) Validate(DriverRequest req)
    {
        if ((req.FullName?.Trim().Length ?? 0) is < 2 or > 120) return ("Enter the driver's full name.", null);
        if (SaValidation.NormalizePhone(req.Phone) is not { } phone)
            return ("Enter a valid South African phone number, e.g. 082 123 4567.", null);
        if ((req.VehicleType?.Trim().Length ?? 0) is < 2 or > 40) return ("Enter the vehicle type, e.g. Bakkie.", null);
        if ((req.VehicleRegistration?.Trim().Length ?? 0) is < 2 or > 20) return ("Enter the vehicle registration.", null);
        if (req.Area?.Trim().Length > 100) return ("The area is too long.", null);
        return (null, phone);
    }

    private static void Apply(Driver driver, DriverRequest req, string phone)
    {
        driver.FullName = req.FullName!.Trim();
        driver.Phone = phone;
        driver.VehicleType = req.VehicleType!.Trim();
        driver.VehicleRegistration = req.VehicleRegistration!.Trim().ToUpperInvariant();
        driver.Area = string.IsNullOrWhiteSpace(req.Area) ? null : req.Area.Trim();
        driver.IsActive = req.IsActive ?? driver.IsActive;
    }

    private object Project(Driver d, int openJobs) => new
    {
        d.Id, d.FullName, d.Phone, d.VehicleType, d.VehicleRegistration, d.Area, d.IsActive,
        d.BadgeCode, d.LastLatitude, d.LastLongitude, d.LastSeenAt,
        OpenJobs = openJobs,
        DriverLink = DeliveryLinks.DriverPage(config, Request, d.AccessToken),
        Documents = DriverDocument.Types.Select(type => d.Documents.FirstOrDefault(x => x.DocType == type) is { } doc
            ? new { DocType = type, Label = DriverPaperwork.Label(type), Uploaded = true, DocUrl = PrivateFiles.Link(doc.DocUrl), doc.ExpiryDate, UploadedAt = (DateTime?)(doc.UpdatedAt ?? doc.CreatedAt),
                    // The automatic check against the driver's name and the typed expiry date
                    CheckStatus = (string?)doc.CheckStatus, doc.CheckSummary }
            : new { DocType = type, Label = DriverPaperwork.Label(type), Uploaded = false, DocUrl = (string?)null, ExpiryDate = (DateOnly?)null, UploadedAt = (DateTime?)null,
                    CheckStatus = (string?)null, CheckSummary = (string?)null }).ToList(),
        // Why the driver can't be allocated deliveries yet; empty when their papers are in order
        PaperworkProblems = DriverPaperwork.Problems(d),
    };

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private async Task<string> NewBadgeCodeAsync()
    {
        // No 0/O or 1/I, so it reads clearly over the phone
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        while (true)
        {
            var code = "DRV-" + new string(Enumerable.Range(0, 5)
                .Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
            if (!await db.Drivers.AnyAsync(d => d.BadgeCode == code)) return code;
        }
    }
}

public record DriverRequest(
    string? FullName, string? Phone, string? VehicleType, string? VehicleRegistration,
    string? Area, bool? IsActive = null);

internal static class DeliveryLinks
{
    /// <summary>The public address of the API (behind the gateway), e.g. https://api.spazasure.co.za.</summary>
    public static string PublicBase(IConfiguration config, HttpRequest request) => PublicApi.Base(request, config);

    public static string DriverPage(IConfiguration config, HttpRequest request, string token) =>
        $"{PublicBase(config, request)}/driver/{token}";
}

/// <summary>Whether a driver's documents allow them to deliver: a licence, PrDP and ID, with the licence and PrDP in date.</summary>
internal static class DriverPaperwork
{
    /// <summary>Today in South Africa, so a document expiring today stops counting at midnight local time.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2));

    public static string Label(string docType) => docType switch
    {
        DriverDocument.Licence => "driving licence",
        DriverDocument.Prdp => "PrDP",
        DriverDocument.IdDocument => "ID document",
        _ => docType,
    };

    public static List<string> Problems(Driver driver)
    {
        var problems = new List<string>();
        foreach (var type in DriverDocument.Types)
        {
            var doc = driver.Documents.FirstOrDefault(d => d.DocType == type);
            if (doc is null) problems.Add($"No {Label(type)} uploaded");
            else if (doc.ExpiryDate is { } expiry && expiry <= Today) problems.Add($"{char.ToUpper(Label(type)[0])}{Label(type)[1..]} expired on {expiry:d MMM yyyy}");
        }
        return problems;
    }
}
