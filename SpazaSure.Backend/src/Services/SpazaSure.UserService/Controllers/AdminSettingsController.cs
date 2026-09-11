using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = "admin")]
public class AdminSettingsController(SpazaSureDbContext db) : ControllerBase
{
    internal const string OnboardingFeeKey = "shop_onboarding_fee_amount";
    internal const decimal DefaultOnboardingFee = 150m;

    [HttpGet("onboarding-fee")]
    public async Task<IActionResult> GetOnboardingFee()
    {
        var amount = await ReadAmount();
        return Ok(ApiResponse<object>.Ok(new { amount }));
    }

    [HttpPut("onboarding-fee")]
    public async Task<IActionResult> UpdateOnboardingFee(UpdateOnboardingFeeRequest req)
    {
        if (!req.Amount.HasValue)
            return BadRequest(ApiResponse.Fail("Amount is required."));
        var amount = req.Amount.Value;
        if (amount is < 0m or > 100000m || decimal.Round(amount, 2) != amount)
            return BadRequest(ApiResponse.Fail("Amount must be between R0.00 and R100,000.00 with at most two decimal places."));

        var setting = await db.PlatformSettings.SingleOrDefaultAsync(s => s.Key == OnboardingFeeKey);
        if (setting is null)
        {
            setting = new PlatformSetting { Key = OnboardingFeeKey };
            db.PlatformSettings.Add(setting);
        }
        setting.Value = amount.ToString("0.00", CultureInfo.InvariantCulture);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { amount }, "Onboarding fee updated."));
    }

    private async Task<decimal> ReadAmount()
    {
        var value = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == OnboardingFeeKey)
            .Select(s => s.Value)
            .SingleOrDefaultAsync();
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : DefaultOnboardingFee;
    }
}

public record UpdateOnboardingFeeRequest(decimal? Amount);
