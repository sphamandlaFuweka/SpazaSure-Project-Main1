using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/customer/rewards")]
[Authorize(Roles = "customer")]
public class CustomerRewardsController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetSummary()
    {
        var points = await db.CustomerRewardTransactions
            .Where(t => t.CustomerUserId == UserId)
            .SumAsync(t => t.Points);
        var vouchers = await db.CustomerVouchers
            .Where(v => v.CustomerUserId == UserId && v.Status == "active")
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new { v.Id, v.Code, v.Amount, v.Status, v.CreatedAt })
            .ToListAsync();
        var scans = await db.CustomerScanEvents.CountAsync(s => s.CustomerUserId == UserId);

        return Ok(ApiResponse<object>.Ok(new
        {
            points,
            pointsPerScan = 5,
            pointsRequiredForVoucher = 500,
            voucherAmount = 20m,
            scans,
            vouchers,
        }));
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem()
    {
        var points = await db.CustomerRewardTransactions
            .Where(t => t.CustomerUserId == UserId)
            .SumAsync(t => t.Points);
        if (points < 500)
            return BadRequest(ApiResponse.Fail($"You need {500 - points} more points to redeem a R20 voucher."));

        var voucher = new CustomerVoucher
        {
            CustomerUserId = UserId,
            Code = $"SPZ-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Amount = 20m,
        };
        db.CustomerRewardTransactions.Add(new CustomerRewardTransaction
        {
            CustomerUserId = UserId,
            Points = -500,
            Type = "voucher_redemption",
            Reference = voucher.Code,
        });
        db.CustomerVouchers.Add(voucher);
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { voucher.Code, voucher.Amount, remainingPoints = points - 500 }, "Your R20 voucher is ready to use at a participating spaza shop."));
    }

    [HttpGet("vouchers")]
    public async Task<IActionResult> Vouchers()
    {
        var vouchers = await db.CustomerVouchers
            .Where(v => v.CustomerUserId == UserId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new { v.Id, v.Code, v.Amount, v.Status, v.CreatedAt, v.RedeemedAt })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(vouchers));
    }
}
