using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// The Customer app's "Shops" tab — a directory of active, verified spaza
/// shops. Deliberately read-only and limited to public-safe fields (no
/// owner ID numbers, onboarding fee refs, etc. — see ShopProfileController
/// for the shop owner's own, fuller view of their profile).
/// </summary>
[ApiController]
[Route("api/customer/shops")]
[Authorize]
public class CustomerShopsController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetShops([FromQuery] string? search)
    {
        var query = db.SpazaShops
            .Where(s => s.Status == "active")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.ShopName.Contains(search) || (s.City != null && s.City.Contains(search)));

        var shops = await query
            .OrderByDescending(s => s.RatingAvg)
            .Select(s => new
            {
                s.Id,
                s.ShopName,
                s.Address,
                s.City,
                s.Province,
                s.Latitude,
                s.Longitude,
                s.RatingAvg,
                s.RatingCount,
                s.ComplianceStatus,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(shops));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var shop = await db.SpazaShops
            .Where(s => s.Id == id && s.Status == "active")
            .Select(s => new
            {
                s.Id,
                s.ShopName,
                s.Address,
                s.City,
                s.Province,
                s.Latitude,
                s.Longitude,
                s.RatingAvg,
                s.RatingCount,
                s.ComplianceStatus,
            })
            .FirstOrDefaultAsync();

        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        return Ok(ApiResponse<object>.Ok(shop));
    }
}
