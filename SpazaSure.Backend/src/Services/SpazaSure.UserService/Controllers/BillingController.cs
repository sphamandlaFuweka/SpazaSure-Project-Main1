using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// The app's Billing section, for shop owners and customers: billing info for invoices, saved
/// cards (through Paystack) and, for shops, the bank account withdrawals are paid into.
/// </summary>
[ApiController]
[Route("api/billing")]
[Authorize(Roles = "spaza_owner,customer")]
public partial class BillingController(
    SpazaSureDbContext db,
    PaystackPaymentService paystack,
    ILogger<BillingController> logger) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsShop => User.IsInRole("spaza_owner");

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        // Someone who added a card and closed the app before coming back still gets it saved
        await SavedCards.ConfirmPendingAsync(db, paystack, logger, UserId, ct);

        var profile = await db.BillingProfiles.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == UserId, ct);
        var cards = await db.SavedCards.AsNoTracking()
            .Where(c => c.UserId == UserId && c.Status == "active")
            .OrderByDescending(c => c.IsDefault).ThenByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            info = profile is null ? null : new
            {
                profile.BillingName, profile.CompanyName, profile.VatNumber, profile.BillingEmail,
                profile.AddressLine1, profile.AddressLine2, profile.City, profile.Province, profile.PostalCode
            },
            cards = cards.Select(CardView),
            cardsEnabled = paystack.IsConfigured,
            cardCheckAmount = SavedCards.CheckAmount,
            // Only shops are paid out (wallet withdrawals)
            payoutAccountAllowed = IsShop,
            payoutAccount = IsShop && profile?.AccountNumber is { Length: > 0 } ? new
            {
                profile.BankName, profile.AccountHolder, profile.BranchCode, profile.AccountType,
                profile.AccountNumber,
                accountNumberLast4 = profile.AccountNumber[^Math.Min(4, profile.AccountNumber.Length)..]
            } : null
        }));
    }

    // ─── Billing info ──────────────────────────────────────────────────────────

    [HttpPut("info")]
    public async Task<IActionResult> SaveInfo([FromBody] BillingInfoRequest req, CancellationToken ct)
    {
        var name = Clean(req.BillingName, 120);
        if (name is null) return BadRequest(ApiResponse.Fail("Enter the name invoices should be made out to."));
        var vat = req.VatNumber is null ? null : SaValidation.Digits(req.VatNumber);
        if (!string.IsNullOrEmpty(vat) && !VatNumber().IsMatch(vat))
            return BadRequest(ApiResponse.Fail("A South African VAT number is 10 digits and starts with 4."));
        var email = Clean(req.BillingEmail, 200);
        if (email is not null && !PaymentEmail.IsValid(email))
            return BadRequest(ApiResponse.Fail("Enter a valid billing email address."));
        var postalCode = req.PostalCode is null ? null : SaValidation.Digits(req.PostalCode);
        if (!string.IsNullOrEmpty(postalCode) && postalCode.Length != 4)
            return BadRequest(ApiResponse.Fail("The postal code must be 4 digits."));

        var profile = await ProfileAsync(ct);
        profile.BillingName = name;
        profile.CompanyName = Clean(req.CompanyName, 160);
        profile.VatNumber = string.IsNullOrEmpty(vat) ? null : vat;
        profile.BillingEmail = email;
        profile.AddressLine1 = Clean(req.AddressLine1, 200);
        profile.AddressLine2 = Clean(req.AddressLine2, 200);
        profile.City = Clean(req.City, 100);
        profile.Province = Clean(req.Province, 60);
        profile.PostalCode = string.IsNullOrEmpty(postalCode) ? null : postalCode;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("Billing details saved."));
    }

    [HttpDelete("info")]
    public async Task<IActionResult> RemoveInfo(CancellationToken ct)
    {
        var profile = await db.BillingProfiles.FirstOrDefaultAsync(b => b.UserId == UserId, ct);
        if (profile is not null)
        {
            profile.BillingName = profile.CompanyName = profile.VatNumber = profile.BillingEmail = null;
            profile.AddressLine1 = profile.AddressLine2 = profile.City = profile.Province = profile.PostalCode = null;
            profile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Ok(ApiResponse.Ok("Billing details removed."));
    }

    // ─── Payout bank account (shops) ───────────────────────────────────────────

    [HttpPut("payout-account")]
    public async Task<IActionResult> SavePayoutAccount([FromBody] PayoutAccountRequest req, CancellationToken ct)
    {
        if (!IsShop) return Forbid();
        var bankName = Clean(req.BankName, 80);
        var holder = Clean(req.AccountHolder, 120);
        var accountNumber = SaValidation.Digits(req.AccountNumber);
        var branchCode = SaValidation.Digits(req.BranchCode);
        var accountType = req.AccountType?.Trim().ToLowerInvariant();
        if (bankName is null || holder is null)
            return BadRequest(ApiResponse.Fail("Enter the bank name and the account holder's name."));
        if (accountNumber.Length is < 6 or > 20)
            return BadRequest(ApiResponse.Fail("Enter a valid bank account number."));
        if (branchCode.Length is not (0 or 6))
            return BadRequest(ApiResponse.Fail("The branch code must be 6 digits."));
        if (accountType is not (null or "" or "cheque" or "savings"))
            return BadRequest(ApiResponse.Fail("The account type must be cheque or savings."));

        var profile = await ProfileAsync(ct);
        profile.BankName = bankName;
        profile.AccountHolder = holder;
        profile.AccountNumber = accountNumber;
        profile.BranchCode = branchCode.Length == 0 ? null : branchCode;
        profile.AccountType = string.IsNullOrEmpty(accountType) ? null : accountType;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("Payout account saved. Withdrawals will be paid into it."));
    }

    [HttpDelete("payout-account")]
    public async Task<IActionResult> RemovePayoutAccount(CancellationToken ct)
    {
        if (!IsShop) return Forbid();
        var profile = await db.BillingProfiles.FirstOrDefaultAsync(b => b.UserId == UserId, ct);
        if (profile is not null)
        {
            profile.BankName = profile.AccountHolder = profile.AccountNumber = null;
            profile.BranchCode = profile.AccountType = null;
            profile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Ok(ApiResponse.Ok("Payout account removed."));
    }

    // ─── Saved cards ───────────────────────────────────────────────────────────

    /// <summary>Starts saving a card: returns Paystack's checkout for the R1 card check.</summary>
    [HttpPost("cards")]
    public async Task<IActionResult> AddCard(CancellationToken ct)
    {
        if (!paystack.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail("Card payments are not switched on yet. Please try again later."));

        await SavedCards.ConfirmPendingAsync(db, paystack, logger, UserId, ct);
        var active = await db.SavedCards.CountAsync(c => c.UserId == UserId && c.Status == "active", ct);
        if (active >= SavedCards.MaximumCards)
            return BadRequest(ApiResponse.Fail($"You can save up to {SavedCards.MaximumCards} cards. Remove one first."));

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId, ct);
        if (user is null) return NotFound(ApiResponse.Fail("Account not found."));
        var shopEmail = IsShop
            ? await db.SpazaShops.AsNoTracking().Where(s => s.UserId == UserId).Select(s => s.Email).FirstOrDefaultAsync(ct)
            : null;
        // Shops often have no email; Paystack still needs one (see PaymentEmail).
        var email = PaymentEmail.For(shopEmail ?? user.Email, user.Phone, user.Id);

        var card = new SavedCard { UserId = UserId, Reference = SavedCards.NewReference(), Email = email };
        db.SavedCards.Add(card);
        await db.SaveChangesAsync(ct);

        var checkout = await paystack.CreateCardCheckCheckoutAsync(
            SavedCards.CheckAmount, email, card.Reference, paystack.GetCallbackUrl("ShopCheckout"), ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            cardId = card.Id,
            amount = SavedCards.CheckAmount,
            checkoutUrl = checkout.AuthorizationUrl
        }));
    }

    /// <summary>Called when the person comes back from Paystack's checkout.</summary>
    [HttpPost("cards/{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmCard(Guid id, CancellationToken ct)
    {
        var card = await db.SavedCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId, ct);
        if (card is null) return NotFound(ApiResponse.Fail("Card not found."));

        return await SavedCards.ConfirmAsync(db, paystack, logger, card, ct) switch
        {
            SavedCards.Outcome.Saved => Ok(ApiResponse<object>.Ok(new { status = "saved", card = CardView(card) },
                "Card saved. The R1 check will be refunded to it.")),
            SavedCards.Outcome.AlreadySaved => Ok(ApiResponse<object>.Ok(new { status = "already_saved" },
                "That card is already saved. The R1 check will be refunded to it.")),
            SavedCards.Outcome.NotUsable => Ok(ApiResponse<object>.Ok(new { status = "not_usable" },
                "That card can't be saved for later payments. The R1 check will be refunded to it.")),
            _ => Ok(ApiResponse<object>.Ok(new { status = "pending" },
                "We haven't received the R1 card check from Paystack yet."))
        };
    }

    [HttpPost("cards/{id:guid}/default")]
    public async Task<IActionResult> MakeDefault(Guid id, CancellationToken ct)
    {
        var cards = await db.SavedCards.Where(c => c.UserId == UserId && c.Status == "active").ToListAsync(ct);
        if (cards.All(c => c.Id != id)) return NotFound(ApiResponse.Fail("Card not found."));
        foreach (var c in cards) c.IsDefault = c.Id == id;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("Default card updated."));
    }

    [HttpDelete("cards/{id:guid}")]
    public async Task<IActionResult> RemoveCard(Guid id, CancellationToken ct)
    {
        var card = await db.SavedCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId, ct);
        if (card is null) return NotFound(ApiResponse.Fail("Card not found."));

        if (card.AuthorizationCode is { Length: > 0 } code && paystack.IsConfigured)
        {
            try
            {
                await paystack.DeactivateAuthorizationAsync(code, ct);
            }
            catch (Exception error) when (error is HttpRequestException or InvalidOperationException)
            {
                // We forget the card either way, so it can't be charged from SpazaSure again
                logger.LogWarning(error, "Could not deactivate the Paystack authorization of card {CardId}", card.Id);
            }
        }

        db.SavedCards.Remove(card);
        if (card.IsDefault)
        {
            var next = await db.SavedCards
                .Where(c => c.UserId == UserId && c.Status == "active" && c.Id != card.Id)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (next is not null) next.IsDefault = true;
        }
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("Card removed."));
    }

    private async Task<BillingProfile> ProfileAsync(CancellationToken ct)
    {
        var profile = await db.BillingProfiles.FirstOrDefaultAsync(b => b.UserId == UserId, ct);
        if (profile is not null) return profile;
        profile = new BillingProfile { UserId = UserId };
        db.BillingProfiles.Add(profile);
        return profile;
    }

    private static object CardView(SavedCard c) => new
    {
        c.Id, c.Brand, c.Last4, c.ExpMonth, c.ExpYear, c.Bank, c.IsDefault,
        expired = IsExpired(c)
    };

    private static bool IsExpired(SavedCard c) =>
        int.TryParse(c.ExpYear, out var year) && int.TryParse(c.ExpMonth, out var month) &&
        new DateTime(year, Math.Clamp(month, 1, 12), 1).AddMonths(1) <= DateTime.UtcNow;

    private static string? Clean(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    [GeneratedRegex("^4\\d{9}$")]
    private static partial Regex VatNumber();
}

public record BillingInfoRequest(
    string? BillingName, string? CompanyName, string? VatNumber, string? BillingEmail,
    string? AddressLine1, string? AddressLine2, string? City, string? Province, string? PostalCode);

public record PayoutAccountRequest(
    string? BankName, string? AccountHolder, string? AccountNumber, string? BranchCode, string? AccountType);
