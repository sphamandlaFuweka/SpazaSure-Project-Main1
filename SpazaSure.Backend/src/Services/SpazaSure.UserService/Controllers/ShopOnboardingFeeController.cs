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
    PaystackPaymentService paystack) : ControllerBase
{
    [HttpPost("paystack/checkout-session")]
    public async Task<IActionResult> PaystackCheckout(CancellationToken cancellationToken)
    {
        if (!paystack.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail("Paystack checkout is not configured."));

        var shop = await db.SpazaShops.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));
        if (shop.OnboardingFeePaid) return Ok(ApiResponse<object>.Ok(new { paid = true }));
        // Shops often have no email; Paystack still needs one (see PaymentEmail).
        var email = SpazaSure.Shared.Helpers.PaymentEmail.For(shop.Email ?? shop.User.Email, shop.Phone, shop.UserId);

        var amount = await GetConfiguredAmount();
        if (amount <= 0) return Ok(ApiResponse<object>.Ok(new { paid = true, amount = 0 }));
        var previousPendingPayments = await db.ShopOnboardingPayments
            .Where(p => p.ShopId == shop.Id && p.Status == "pending")
            .ToListAsync(cancellationToken);
        foreach (var previousPayment in previousPendingPayments)
            previousPayment.Status = "cancelled";
        if (previousPendingPayments.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        var reference = $"SS-{Guid.NewGuid():N}";
        var payment = new ShopOnboardingPayment
        {
            ShopId = shop.Id,
            Amount = amount,
            Status = "pending",
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            ProviderPaymentId = reference
        };
        db.ShopOnboardingPayments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        var checkout = await paystack.CreateOnboardingCheckoutAsync(
            amount,
            email,
            reference,
            paystack.GetCallbackUrl("ShopCheckout"),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new
        {
            paid = false,
            paymentId = payment.Id,
            amount,
            checkoutUrl = checkout.AuthorizationUrl
        }));
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
