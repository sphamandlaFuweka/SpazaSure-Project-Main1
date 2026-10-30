using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Shops pay for a group buy when they join, from their wallet or by card. Each buys at least
/// <see cref="MinPerShop"/> items in total (any mix of the products) and never more of a product
/// than it still needs, so the target is shared out between shops. SpazaSure holds the money: when the supplier accepts, it
/// moves into the shop's order; whatever never makes it into an order (the product expired or
/// was cancelled) goes back to the shop's wallet. Payment is final, so shops can't leave.
/// </summary>
public static class GroupBuyPayments
{
    /// <summary>The fewest items one shop buys, counted across all the products it picks.</summary>
    public const int MinPerShop = 10;
    public const string Wallet = ShopWallet.PaymentMethod;
    public const string Card = "paystack";
    /// <summary>How long a card checkout holds its quantities.</summary>
    public static readonly TimeSpan CardReservation = TimeSpan.FromHours(1);

    public static bool IsPaid(GroupBuyParticipant p) => p.PaymentStatus == "paid";

    private static bool HoldsReservation(GroupBuyParticipant p, DateTime now) =>
        p.Status == "pending_payment" && p.ReservedUntil > now;

    private static int JoinedQty(GroupBuyProduct product) =>
        product.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);

    private static int ReservedQty(GroupBuy groupBuy, Guid productId, DateTime now, Guid? exceptParticipantId) =>
        groupBuy.Participants
            .Where(p => p.Id != exceptParticipantId && HoldsReservation(p, now))
            .SelectMany(p => p.Items)
            .Where(i => i.Status == "reserved" && i.GroupBuyProductId == productId)
            .Sum(i => i.Quantity);

    /// <summary>Units still needed to reach the target, after shops that joined or are paying now.</summary>
    public static int Remaining(GroupBuy groupBuy, GroupBuyProduct product, DateTime now, Guid? exceptParticipantId = null) =>
        Math.Max(0, product.TargetQty - JoinedQty(product) - ReservedQty(groupBuy, product.Id, now, exceptParticipantId));

    /// <summary>Units of this product taken by shops (joined or paying now): what the supplying branch must have in stock.</summary>
    public static int Committed(GroupBuy groupBuy, GroupBuyProduct product, DateTime now, Guid? exceptParticipantId = null) =>
        JoinedQty(product) + ReservedQty(groupBuy, product.Id, now, exceptParticipantId);

    /// <summary>The least a shop may take of one product: the product's own minimum, unless less than that is left.</summary>
    public static int MinimumFor(GroupBuyProduct product, int remaining) =>
        Math.Min(remaining, Math.Max(1, product.Product.MinOrderQty));

    /// <summary>
    /// The fewest items a shop must buy in total: <see cref="MinPerShop"/>, or everything the open
    /// products still need when that is less, so the last shop can still complete the targets.
    /// </summary>
    public static int MinimumTotal(GroupBuy groupBuy, DateTime now, Guid? exceptParticipantId = null) =>
        Math.Min(MinPerShop, groupBuy.Products
            .Where(p => p.Status is "active" or "qualified")
            .Sum(p => Remaining(groupBuy, p, now, exceptParticipantId)));

    /// <summary>Why a shop can't buy this many items in total, or null when it can.</summary>
    public static string? TotalProblem(GroupBuy groupBuy, int totalQuantity, DateTime now, Guid? exceptParticipantId = null)
    {
        var minimum = MinimumTotal(groupBuy, now, exceptParticipantId);
        if (totalQuantity >= minimum) return null;
        return minimum < MinPerShop
            ? $"Only {minimum} items are left in this group buy, so take all {minimum} to complete it."
            : $"Buy at least {MinPerShop} items in total. You can mix products, for example 3 of one and 7 of another.";
    }

    /// <summary>Why a shop can't take this quantity, or null when it can.</summary>
    public static string? QuantityProblem(GroupBuy groupBuy, GroupBuyProduct product, int quantity, DateTime now, Guid? exceptParticipantId = null)
    {
        var name = product.Product.Name;
        var remaining = Remaining(groupBuy, product, now, exceptParticipantId);
        if (remaining <= 0) return $"'{name}' has already reached its target.";
        if (quantity > remaining) return $"Only {remaining} more of '{name}' are needed to reach the target.";
        var minimum = MinimumFor(product, remaining);
        if (quantity < minimum) return $"The minimum for '{name}' is {minimum}.";
        return null;
    }

    public static decimal LineTotal(GroupBuyParticipantItem item) => item.ShopUnitPrice * item.Quantity;

    /// <summary>
    /// Money SpazaSure still holds for this shop: items waiting on their target, plus the delivery
    /// fee until it goes into the shop's first order. What was paid and is no longer held either
    /// moved into orders or went back to the wallet.
    /// </summary>
    public static decimal Held(GroupBuyParticipant p)
    {
        if (!IsPaid(p)) return 0m;
        var waiting = p.Items.Where(i => i.Status == "joined").ToList();
        var ordered = p.Items.Any(i => i.Status == "confirmed");
        return waiting.Sum(LineTotal) + (waiting.Count > 0 && !ordered ? p.DeliveryFee : 0m);
    }

    /// <summary>
    /// Brings the shop's wallet in line with this participation: a wallet payment debits what is
    /// held, a card payment credits what is no longer held (orders debit their own total). Safe
    /// to repeat. Call once per participant before saving.
    /// </summary>
    public static Task SyncWalletAsync(SpazaSureDbContext db, GroupBuyParticipant p, string title, CancellationToken ct = default)
    {
        if (!IsPaid(p)) return Task.CompletedTask;
        var desired = (p.PaymentMethod == Card ? p.AmountPaid : 0m) - Held(p);
        return ShopWallet.SyncAsync(db, p.ShopId, p.PaymentId, desired,
            ShopWallet.Entry.GroupBuyRefund, ShopWallet.Entry.GroupBuyPayment,
            "Group buy: " + title, p.PaymentReference, ct);
    }

    /// <summary>Product totals and statuses from the joined quantities.</summary>
    public static void Recount(GroupBuy groupBuy)
    {
        foreach (var product in groupBuy.Products.Where(p => p.Status is "active" or "qualified"))
        {
            product.CurrentQty = JoinedQty(product);
            product.Status = product.CurrentQty >= product.TargetQty ? "qualified" : "active";
        }
        foreach (var participant in groupBuy.Participants)
            participant.Quantity = participant.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);
        SyncLegacy(groupBuy);
    }

    /// <summary>Older screens read the campaign's first product from the group buy row itself.</summary>
    public static void SyncLegacy(GroupBuy groupBuy)
    {
        var first = groupBuy.Products.FirstOrDefault(p => p.ProductId == groupBuy.ProductId)
            ?? groupBuy.Products.First();
        groupBuy.ProductId = first.ProductId;
        groupBuy.TargetQty = first.TargetQty;
        groupBuy.CurrentQty = first.CurrentQty;
        groupBuy.OriginalPrice = first.OriginalPrice;
        groupBuy.DiscountPrice = first.DiscountPrice;
        groupBuy.DiscountPct = first.DiscountPct;
    }

    /// <summary>A campaign that reaches its end date without its targets gets one more week.</summary>
    public static readonly TimeSpan Extension = TimeSpan.FromDays(7);
    public const int MaxExtensions = 1;
    /// <summary>Members hear this long before the end date.</summary>
    public static readonly TimeSpan ReminderBefore = TimeSpan.FromDays(2);

    public sealed record EndResult(bool Extended, List<(GroupBuyParticipant Participant, decimal Refund)> Refunds);

    /// <summary>
    /// A live campaign's end date has passed. The first time, it runs another week; after that it
    /// closes and the money for products that didn't reach their target goes back to the wallets.
    /// Does not save.
    /// </summary>
    public static async Task<EndResult> ReachEndAsync(SpazaSureDbContext db, GroupBuy groupBuy, DateTime now, CancellationToken ct = default)
    {
        if (groupBuy.ExtensionCount < MaxExtensions)
        {
            groupBuy.ExtensionCount++;
            var extended = groupBuy.ExpiresAt.Add(Extension);
            groupBuy.ExpiresAt = extended > now ? extended : now.Add(Extension);
            groupBuy.EndingReminderSentAt = null; // remind again before the new end date
            return new EndResult(true, []);
        }
        return new EndResult(false, await CloseAsync(db, groupBuy, "expired", ct));
    }

    /// <summary>Items the open products still need, across all of them.</summary>
    public static int ItemsStillNeeded(GroupBuy groupBuy, DateTime now) =>
        groupBuy.Products.Where(p => p.Status is "active" or "qualified").Sum(p => Remaining(groupBuy, p, now));

    /// <summary>
    /// Ends a campaign (status "expired" or "cancelled"). Items still waiting on a target are
    /// released and their money goes back to the shops' wallets. Returns each shop's refund.
    /// Does not save.
    /// </summary>
    public static async Task<List<(GroupBuyParticipant Participant, decimal Refund)>> CloseAsync(
        SpazaSureDbContext db, GroupBuy groupBuy, string status, CancellationToken ct = default)
    {
        groupBuy.Status = status;
        foreach (var product in groupBuy.Products.Where(p => p.Status is "active" or "qualified" or "pending_approval"))
        {
            product.Status = status;
            if (status == "cancelled") product.CurrentQty = 0;
        }

        var refunds = new List<(GroupBuyParticipant, decimal)>();
        foreach (var participant in groupBuy.Participants)
        {
            var heldBefore = Held(participant);
            foreach (var item in participant.Items.Where(i => i.Status is "joined" or "reserved"))
                item.Status = "cancelled";
            if (participant.Status is "joined" or "pending_payment" && !participant.Items.Any(i => i.Status == "confirmed"))
                participant.Status = "cancelled";
            participant.Quantity = 0;
            await SyncWalletAsync(db, participant, groupBuy.Title, ct);
            var refund = heldBefore - Held(participant);
            if (refund > 0m) refunds.Add((participant, refund));
        }
        SyncLegacy(groupBuy);
        return refunds;
    }

    public enum CardResult { AlreadyPaid, Joined, RefundedToWallet, WrongAmount }

    /// <summary>
    /// Paystack confirmed a card checkout for <paramref name="paidCents"/>. The shop joins with the
    /// quantities it reserved; if the campaign closed or another shop took those units meanwhile
    /// (the reservation ran out), the payment goes to the shop's wallet instead. Does not save.
    /// </summary>
    public static async Task<CardResult> ConfirmCardAsync(
        SpazaSureDbContext db, GroupBuy groupBuy, GroupBuyParticipant participant, long paidCents, DateTime now,
        CancellationToken ct = default)
    {
        if (IsPaid(participant)) return CardResult.AlreadyPaid;
        if (paidCents != ToCents(participant.AmountPaid)) return CardResult.WrongAmount;

        participant.PaymentStatus = "paid";
        participant.PaidAt = now;
        var reserved = participant.Items.Where(i => i.Status == "reserved").ToList();
        var fits = participant.Status == "pending_payment" && reserved.Count > 0 &&
                   groupBuy.Status == "active" && groupBuy.ExpiresAt > now &&
                   reserved.All(item =>
                   {
                       var product = groupBuy.Products.Single(p => p.Id == item.GroupBuyProductId);
                       return product.Status is "active" or "qualified" &&
                              item.Quantity <= Remaining(groupBuy, product, now, participant.Id);
                   });
        foreach (var item in reserved) item.Status = fits ? "joined" : "cancelled";
        participant.Status = fits ? "joined" : "cancelled";
        participant.ReservedUntil = null;
        Recount(groupBuy);
        await SyncWalletAsync(db, participant, groupBuy.Title, ct);
        return fits ? CardResult.Joined : CardResult.RefundedToWallet;
    }

    private const string ReferencePrefix = "SPZ-GBJ-";

    /// <summary>A new payment reference that names the participant, e.g. SPZ-GBJ-{participant}-{random}.</summary>
    public static string NewReference(Guid participantId) =>
        $"{ReferencePrefix}{participantId:N}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>The participant a group buy payment reference was made for, or null for other references.</summary>
    public static Guid? ParticipantIdFrom(string reference) =>
        reference.StartsWith(ReferencePrefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(reference.Substring(ReferencePrefix.Length, 32), "N", out var id)
            ? id
            : null;

    /// <summary>
    /// The shop paid an older card checkout after starting a newer one: nothing is waiting on that
    /// payment any more, so it goes into the shop's wallet as a top-up. Safe to repeat. Does not save.
    /// </summary>
    public static async Task CreditOldCheckoutAsync(
        SpazaSureDbContext db, GroupBuyParticipant participant, string reference, long paidCents, string title,
        CancellationToken ct = default)
    {
        if (await db.ShopWalletTransactions.AnyAsync(t => t.Reference == reference, ct)) return;
        var topUp = new ShopWalletTransaction
        {
            ShopId = participant.ShopId,
            Amount = paidCents / 100m,
            Type = "top_up",
            Status = "approved",
            Method = Card,
            Reference = reference,
            Notes = $"Card payment for group buy '{title}' that was replaced by a newer checkout.",
            ApprovedAt = DateTime.UtcNow,
        };
        db.ShopWalletTransactions.Add(topUp);
        await ShopWallet.SyncTopUpAsync(db, topUp, ct);
    }

    public static long ToCents(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Loads a campaign with everything payments need and locks its row until the transaction
    /// ends, so two shops can't take the same last units. No lock on non-relational test databases.
    /// </summary>
    public static async Task<GroupBuy?> LoadForUpdateAsync(SpazaSureDbContext db, Guid groupBuyId, CancellationToken ct = default)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM group_buys WHERE id = {groupBuyId} FOR UPDATE", ct);
        return await db.GroupBuys
            .Include(g => g.Supplier)
            .Include(g => g.Products).ThenInclude(p => p.Product).ThenInclude(p => p.BranchStock)
            .Include(g => g.Products).ThenInclude(p => p.Items)
            .Include(g => g.Participants).ThenInclude(p => p.Shop)
            .Include(g => g.Participants).ThenInclude(p => p.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == groupBuyId, ct);
    }
}
