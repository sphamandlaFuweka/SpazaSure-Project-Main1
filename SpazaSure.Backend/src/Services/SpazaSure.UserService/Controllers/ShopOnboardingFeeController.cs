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
        // An earlier checkout may have been paid while its webhook is still on the way:
        // confirm it rather than cancel it and charge the shop twice.
        if (await TryCompleteRecentPaymentAsync(shop, cancellationToken))
            return Ok(ApiResponse<object>.Ok(new { paid = true }));
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
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId, cancellationToken);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop profile not found."));
        // The app checks this right after checkout; don't wait for Paystack's webhook
        if (!shop.OnboardingFeePaid) await TryCompleteRecentPaymentAsync(shop, cancellationToken);
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

    /// <summary>
    /// Asks Paystack about the shop's checkouts from the last day and marks the fee paid when one
    /// went through. Cancelled ones count too: they were replaced by a newer checkout, but the
    /// shop may already have paid them.
    /// </summary>
    private async Task<bool> TryCompleteRecentPaymentAsync(SpazaShop shop, CancellationToken cancellationToken)
    {
        if (!paystack.IsConfigured) return false;
        var since = DateTime.UtcNow.AddHours(-24);
        var recent = await db.ShopOnboardingPayments
            .Where(p => p.ShopId == shop.Id && (p.Status == "pending" || p.Status == "cancelled") &&
                        p.ProviderPaymentId != null && p.CreatedAt > since)
            .OrderByDescending(p => p.CreatedAt)
            .Take(3)
            .ToListAsync(cancellationToken);

        foreach (var payment in recent)
        {
            PaystackTransaction transaction;
            try
            {
                transaction = await paystack.VerifyTransactionAsync(payment.ProviderPaymentId!, cancellationToken);
            }
            catch (Exception error) when (error is HttpRequestException or InvalidOperationException
                                              or KeyNotFoundException)
            {
                // Not paid yet, or Paystack is unreachable: the webhook still confirms it later
                continue;
            }
            if (!IsPaid(transaction, payment)) continue;

            payment.Status = "completed";
            payment.CompletedAt = DateTime.UtcNow;
            shop.OnboardingFeePaid = true;
            shop.OnboardingFeeRef = payment.ProviderPaymentId;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        return false;
    }

    internal static bool IsPaid(PaystackTransaction transaction, ShopOnboardingPayment payment) =>
        transaction.IsSuccessful &&
        string.Equals(transaction.Reference, payment.ProviderPaymentId, StringComparison.Ordinal) &&
        string.Equals(transaction.Currency, "ZAR", StringComparison.OrdinalIgnoreCase) &&
        transaction.Amount == decimal.ToInt64(decimal.Round(payment.Amount * 100m, 0, MidpointRounding.AwayFromZero));

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
