using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.UserService.Services;

/// <summary>
/// Saving a card: the person pays a R1 card check through Paystack's checkout, Paystack hands
/// back a reusable authorization for that card, we keep it and refund the R1.
/// </summary>
public static class SavedCards
{
    public const decimal CheckAmount = 1m;
    public const string ReferencePrefix = "CARD-";
    public const int MaximumCards = 5;

    public static string NewReference() => $"{ReferencePrefix}{Guid.NewGuid():N}";

    public enum Outcome { Saved, AlreadySaved, NotPaid, NotUsable }

    /// <summary>
    /// Asks Paystack whether a pending card's check went through and, if it did, saves the card
    /// and refunds the check. Safe to call more than once: the app's return, the overview and
    /// the webhook may all try.
    /// </summary>
    public static async Task<Outcome> ConfirmAsync(
        SpazaSureDbContext db,
        PaystackPaymentService paystack,
        ILogger logger,
        SavedCard card,
        CancellationToken ct = default)
    {
        if (card.Status == "active") return Outcome.Saved;

        PaystackTransaction transaction;
        try
        {
            transaction = await paystack.VerifyTransactionAsync(card.Reference, ct);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException
                                          or KeyNotFoundException)
        {
            // Not paid yet, or Paystack is unreachable: it can be confirmed later
            return Outcome.NotPaid;
        }

        var paid = transaction.IsSuccessful &&
                   string.Equals(transaction.Reference, card.Reference, StringComparison.Ordinal) &&
                   string.Equals(transaction.Currency, "ZAR", StringComparison.OrdinalIgnoreCase) &&
                   transaction.Amount == (long)(CheckAmount * 100m);
        if (!paid) return Outcome.NotPaid;

        // From here the R1 was taken, so it is refunded whatever happens to the card
        await RefundAsync(paystack, logger, card.Reference, ct);

        var details = transaction.Card;
        if (details is null || !details.Reusable ||
            !string.Equals(details.Channel, "card", StringComparison.OrdinalIgnoreCase))
        {
            db.SavedCards.Remove(card);
            await db.SaveChangesAsync(ct);
            return Outcome.NotUsable;
        }

        // The same card saved before: keep the old one
        if (details.Signature is { Length: > 0 } signature &&
            await db.SavedCards.AnyAsync(c => c.UserId == card.UserId && c.Status == "active" &&
                                              c.Signature == signature, ct))
        {
            db.SavedCards.Remove(card);
            await db.SaveChangesAsync(ct);
            return Outcome.AlreadySaved;
        }

        card.Status = "active";
        card.AuthorizationCode = details.AuthorizationCode;
        card.Signature = details.Signature;
        card.Brand = details.Brand;
        card.Last4 = details.Last4;
        card.ExpMonth = details.ExpMonth;
        card.ExpYear = details.ExpYear;
        card.Bank = details.Bank;
        card.VerifiedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;
        // The first card becomes the default
        card.IsDefault = !await db.SavedCards.AnyAsync(
            c => c.UserId == card.UserId && c.Status == "active" && c.IsDefault, ct);
        await db.SaveChangesAsync(ct);
        return Outcome.Saved;
    }

    /// <summary>Confirms the person's card checks from the last day and clears older ones.</summary>
    public static async Task ConfirmPendingAsync(
        SpazaSureDbContext db,
        PaystackPaymentService paystack,
        ILogger logger,
        Guid userId,
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-1);
        var stale = await db.SavedCards
            .Where(c => c.UserId == userId && c.Status == "pending" && c.CreatedAt <= since)
            .ToListAsync(ct);
        if (stale.Count > 0)
        {
            db.SavedCards.RemoveRange(stale);
            await db.SaveChangesAsync(ct);
        }

        if (!paystack.IsConfigured) return;
        var pending = await db.SavedCards
            .Where(c => c.UserId == userId && c.Status == "pending")
            .OrderByDescending(c => c.CreatedAt)
            .Take(3)
            .ToListAsync(ct);
        foreach (var card in pending)
            await ConfirmAsync(db, paystack, logger, card, ct);
    }

    private static async Task RefundAsync(
        PaystackPaymentService paystack, ILogger logger, string reference, CancellationToken ct)
    {
        try
        {
            await paystack.RefundAsync(reference, ct);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException)
        {
            // Paystack refuses a second refund of the same charge, which is fine; anything else
            // needs a person to refund it from the Paystack dashboard.
            logger.LogWarning(error, "Could not refund card check {Reference}", reference);
        }
    }
}
