using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.DeliveryService.Controllers;

/// <summary>
/// Admins register the drivers SpazaSure delivers with. Each driver gets a private link to share
/// their location while delivering, and a badge code shops can check at the door.
/// </summary>
[ApiController]
[Route("api/admin/drivers")]
[Authorize(Roles = "admin")]
public class DriversController(SpazaSureDbContext db, IConfiguration config) : ControllerBase
{
    /// <summary>Orders a driver is still busy with.</summary>
    internal static readonly string[] OpenStatuses = ["pending", "confirmed", "processing", "ready", "dispatched"];

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = true)
    {
        var drivers = await db.Drivers.AsNoTracking()
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
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == id);
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
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == id);
        if (driver is null) return NotFound(ApiResponse.Fail("Driver not found."));
        driver.AccessToken = NewToken();
        driver.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(Project(driver, await OpenJobsAsync(id)), "New link created. The old one no longer works."));
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
    public static string PublicBase(IConfiguration config, HttpRequest request)
    {
        if (config["PublicApiUrl"] is { Length: > 0 } configured) return configured.TrimEnd('/');
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        return $"{scheme}://{host}";
    }

    public static string DriverPage(IConfiguration config, HttpRequest request, string token) =>
        $"{PublicBase(config, request)}/driver/{token}";
}
