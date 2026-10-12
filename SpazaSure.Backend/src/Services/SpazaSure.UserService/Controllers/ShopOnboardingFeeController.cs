using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/shop/profile/onboarding-fee")]
[Authorize(Roles = "spaza_owner")]
public class ShopOnboardingFeeController(
    SpazaSureDbContext db,
    StripePaymentService stripe) : ControllerBase
{
    [HttpPost("stripe/checkout-session")]
    public async Task<IActionResult> StripeCheckout()
    {
        var shop = await db.SpazaShops.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));
        if (shop.OnboardingFeePaid) return Ok(ApiResponse<object>.Ok(new { paid = true }));
        var amount = await GetConfiguredAmount();
        if (amount <= 0) return Ok(ApiResponse<object>.Ok(new { paid = true, amount = 0 }));
        var payment = await db.ShopOnboardingPayments.FirstOrDefaultAsync(p => p.ShopId == shop.Id && p.Status == "pending" && p.ExpiresAt > DateTime.UtcNow);
        if (payment is null)
        {
            payment = new ShopOnboardingPayment { ShopId = shop.Id, Amount = amount, Status = "pending", ExpiresAt = DateTime.UtcNow.AddHours(24) };
            db.ShopOnboardingPayments.Add(payment);
            await db.SaveChangesAsync();
        }
        var url = await stripe.CreateCheckoutSessionAsync(amount, "zar", "SpazaSure shop onboarding fee", "shop_onboarding", payment.Id, shop.Email ?? shop.User.Email ?? string.Empty);
        payment.ProviderPaymentId = $"stripe-pending-{payment.Id:N}";
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { paid = false, paymentId = payment.Id, amount, checkoutUrl = url }));
    }
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));
        var amount = await GetConfiguredAmount();
        var pending = await db.ShopOnboardingPayments.AsNoTracking()
            .Where(p => p.ShopId == shop.Id && p.Status == "pending" && p.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Amount, p.ExpiresAt })
            .FirstOrDefaultAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            amount,
            paid = shop.OnboardingFeePaid,
            paymentReference = shop.OnboardingFeeRef,
            pendingPayment = pending
        }));
    }

    private async Task<decimal> GetConfiguredAmount()
    {
        var value = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == AdminSettingsController.OnboardingFeeKey)
            .Select(s => s.Value)
            .SingleOrDefaultAsync();
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : AdminSettingsController.DefaultOnboardingFee;
    }
}
