using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// The customer app's "Scan History" screen. Reports already have their own
/// list at GET /api/customer/reports — the client merges the two into a
/// single timeline (scanned vs reported) rather than duplicating that data
/// here.
/// </summary>
[ApiController]
[Route("api/customer/scans")]
[Authorize(Roles = "customer")]
public class CustomerScansController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyScans()
    {
        var scans = await db.CustomerScanEvents
            .Where(s => s.CustomerUserId == UserId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Code,
                s.ProductId,
                productName = s.Product != null ? s.Product.Name : null,
                productImage = s.Product != null ? s.Product.Images : null,
                s.Source,
                s.PointsAwarded,
                s.CreatedAt,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(scans));
    }
}
