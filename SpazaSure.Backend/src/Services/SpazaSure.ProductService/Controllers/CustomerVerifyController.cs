using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.ProductService.Controllers;

[ApiController]
[Route("api/customer/verify")]
[Authorize(Roles = "customer")]
public class CustomerVerifyController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("{code}/reward")]
    public async Task<IActionResult> RewardScan(string code, [FromQuery] Guid? productId, [FromQuery] string? source)
    {
        code = code.Trim();
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(ApiResponse.Fail("A product code is required."));

        var today = DateTime.UtcNow.Date;
        var alreadyAwarded = await db.CustomerScanEvents.AnyAsync(s =>
            s.CustomerUserId == UserId && s.Code == code && s.CreatedAt >= today);
        if (alreadyAwarded)
            return Ok(ApiResponse<object>.Ok(new { pointsAwarded = 0, duplicate = true, message = "This product was already counted today." }));

        var scan = new CustomerScanEvent
        {
            CustomerUserId = UserId,
            Code = code,
            ProductId = productId,
            Source = source ?? "unknown",
            PointsAwarded = 5,
        };
        db.CustomerScanEvents.Add(scan);
        db.CustomerRewardTransactions.Add(new CustomerRewardTransaction
        {
            CustomerUserId = UserId,
            Points = 5,
            Type = "scan",
            Reference = code,
        });
        await db.SaveChangesAsync();

        var totalPoints = await db.CustomerRewardTransactions
            .Where(t => t.CustomerUserId == UserId)
            .SumAsync(t => t.Points);
        return Ok(ApiResponse<object>.Ok(new { pointsAwarded = 5, duplicate = false, totalPoints, message = "+5 reward points earned." }));
    }
}
