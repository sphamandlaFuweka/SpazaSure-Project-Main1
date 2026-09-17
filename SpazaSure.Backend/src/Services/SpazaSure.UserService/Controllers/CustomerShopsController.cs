using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using System.Security.Claims;

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
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
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

    [HttpGet("{id:guid}/reviews")]
    public async Task<IActionResult> Reviews(Guid id)
    {
        var reviews = await db.ShopReviews
            .Where(r => r.ShopId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.Rating, r.Comment, r.CreatedAt })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(reviews));
    }

    [HttpPost("{id:guid}/reviews")]
    public async Task<IActionResult> CreateReview(Guid id, [FromBody] CreateShopReviewRequest req)
    {
        if (req.Rating is < 1 or > 5) return BadRequest(ApiResponse.Fail("Rating must be between 1 and 5."));
        if (!await db.SpazaShops.AnyAsync(s => s.Id == id && s.Status == "active"))
            return NotFound(ApiResponse.Fail("Shop not found."));

        var review = await db.ShopReviews.FirstOrDefaultAsync(r => r.ShopId == id && r.ReviewerUserId == UserId);
        if (review is null)
        {
            review = new ShopReview { ShopId = id, ReviewerUserId = UserId, Rating = req.Rating, Comment = req.Comment?.Trim() };
            db.ShopReviews.Add(review);
        }
        else
        {
            review.Rating = req.Rating;
            review.Comment = req.Comment?.Trim();
        }

        await db.SaveChangesAsync();
        var summary = await db.ShopReviews.Where(r => r.ShopId == id)
            .GroupBy(r => r.ShopId)
            .Select(g => new { Average = g.Average(r => (decimal)r.Rating), Count = g.Count() })
            .SingleAsync();
        var shop = await db.SpazaShops.FirstAsync(s => s.Id == id);
        shop.RatingAvg = Math.Round(summary.Average, 2);
        shop.RatingCount = summary.Count;
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Review saved."));
    }
}

public record CreateShopReviewRequest(int Rating, string? Comment);
