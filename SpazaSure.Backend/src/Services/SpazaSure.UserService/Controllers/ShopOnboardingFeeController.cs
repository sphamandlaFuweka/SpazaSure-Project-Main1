using System.Data;
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
    OnboardingPayFastService payFast,
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
        payment.PayFastPaymentId = $"stripe-pending-{payment.Id:N}";
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

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate()
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var shop = await db.SpazaShops.Include(s => s.User).FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));
        if (shop.OnboardingFeePaid)
            return Ok(ApiResponse<object>.Ok(new { paid = true, paymentReference = shop.OnboardingFeeRef }));

        var now = DateTime.UtcNow;
        var expired = await db.ShopOnboardingPayments
            .Where(p => p.ShopId == shop.Id && p.Status == "pending" && p.ExpiresAt <= now)
            .ToListAsync();
        foreach (var expiredPayment in expired) expiredPayment.Status = "expired";

        var payment = await db.ShopOnboardingPayments
            .FirstOrDefaultAsync(p => p.ShopId == shop.Id && p.Status == "pending" && p.ExpiresAt > now);
        if (payment is null)
        {
            var amount = await GetConfiguredAmount();
            if (amount == 0m)
            {
                shop.OnboardingFeePaid = true;
                shop.OnboardingFeeRef = "WAIVED";
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(ApiResponse<object>.Ok(new { paid = true, amount, paymentReference = shop.OnboardingFeeRef }));
            }
            payment = new ShopOnboardingPayment
            {
                ShopId = shop.Id,
                Amount = amount,
                Status = "pending",
                ExpiresAt = now.AddHours(24)
            };
            db.ShopOnboardingPayments.Add(payment);
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var form = payFast.Generate(
            payment.Id,
            payment.Amount,
            shop.Email ?? shop.User.Email ?? string.Empty,
            shop.OwnerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Shop owner");
        return Ok(ApiResponse<object>.Ok(new
        {
            paid = false,
            paymentId = payment.Id,
            amount = payment.Amount,
            expiresAt = payment.ExpiresAt,
            actionUrl = form.ActionUrl,
            fields = form.Fields,
            returnUrl = form.ReturnUrl,
            cancelUrl = form.CancelUrl
        }));
    }
    [AllowAnonymous]
    [HttpPost("notify")]
    public async Task<IActionResult> Notify()
    {
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync();
        var data = form.ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
        if (!await payFast.ValidateItnAsync(data)) return BadRequest();
        if (!Guid.TryParse(data.GetValueOrDefault("m_payment_id"), out var paymentId)) return BadRequest();
        if (!decimal.TryParse(data.GetValueOrDefault("amount_gross"), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var paidAmount)) return BadRequest();
        var providerId = data.GetValueOrDefault("pf_payment_id");
        if (string.IsNullOrWhiteSpace(providerId)) return BadRequest();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var payment = await db.ShopOnboardingPayments.Include(p => p.Shop)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment is null) return NotFound();
        if (paidAmount != payment.Amount) return BadRequest();
        if (payment.Status == "completed")
        {
            if (!string.Equals(payment.PayFastPaymentId, providerId, StringComparison.Ordinal))
                return BadRequest();
            await transaction.CommitAsync();
            return Ok();
        }
        if (payment.Status is not ("pending" or "expired")) return BadRequest();
        if (await db.ShopOnboardingPayments.AnyAsync(p => p.PayFastPaymentId == providerId && p.Id != payment.Id))
            return BadRequest();

        var otherPendingPayments = await db.ShopOnboardingPayments
            .Where(p => p.ShopId == payment.ShopId && p.Id != payment.Id && p.Status == "pending")
            .ToListAsync();
        foreach (var otherPayment in otherPendingPayments) otherPayment.Status = "cancelled";

        payment.Status = "completed";
        payment.PayFastPaymentId = providerId;
        payment.CompletedAt = DateTime.UtcNow;
        payment.Shop.OnboardingFeePaid = true;
        payment.Shop.OnboardingFeeRef = providerId;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("return")]
    public ContentResult Return() => CheckoutResult("Payment submitted", "Return to the SpazaSure app to confirm your payment status.");

    [AllowAnonymous]
    [HttpGet("cancel")]
    public ContentResult Cancel() => CheckoutResult("Payment cancelled", "No payment was recorded. You can return to the app and try again.");

    private static ContentResult CheckoutResult(string title, string message) => new()
    {
        ContentType = "text/html; charset=utf-8",
        Content = $"<!doctype html><html><head><meta name=\"viewport\" content=\"width=device-width\"></head><body style=\"font-family:sans-serif;padding:32px;text-align:center\"><h2>{title}</h2><p>{message}</p></body></html>"
    };

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
