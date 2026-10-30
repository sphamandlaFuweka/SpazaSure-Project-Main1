using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.Data;
using System.Security.Claims;

namespace SpazaSure.OrderService.Controllers;

internal static class GroupBuyResponse
{
    internal static IQueryable<GroupBuy> WithGraph(IQueryable<GroupBuy> query) => query
        .Include(g => g.Supplier).ThenInclude(s => s.Branches)
        .Include(g => g.SupplierBranch)
        .Include(g => g.CreatedByShop)
        .Include(g => g.Products).ThenInclude(p => p.Product).ThenInclude(p => p.BranchStock)
        .Include(g => g.Products).ThenInclude(p => p.Items)
        .Include(g => g.Participants).ThenInclude(p => p.Shop)
        .Include(g => g.Participants).ThenInclude(p => p.Items)
            .ThenInclude(i => i.GroupBuyProduct).ThenInclude(p => p.Product)
        .AsSplitQuery();

    /// <summary>Shops that are in: paid (or joined before payment existed), not just holding an open card checkout.</summary>
    private static bool Counts(GroupBuyParticipant p) => p.Status is not ("cancelled" or "pending_payment");

    /// <summary>
    /// A group buy as shops see it: prices include SpazaSure's fee (markup, see ShopPricing).
    /// With <paramref name="shopId"/>, also how much more each product needs and what this shop paid.
    /// </summary>
    internal static object ProjectShop(GroupBuy groupBuy, decimal markup, Guid? shopId = null, decimal minOrderAmount = 0m)
    {
        var now = DateTime.UtcNow;
        var isExpired = groupBuy.ExpiresAt <= now && groupBuy.Status == "active";
        var mine = shopId is null ? null : groupBuy.Participants.SingleOrDefault(p => p.ShopId == shopId);
        return new
        {
            groupBuy.Id,
            groupBuy.Title,
            groupBuy.Description,
            groupBuy.BannerImageUrl,
            groupBuy.SupplierId,
            SupplierName = groupBuy.Supplier.CompanyName,
            CreatedByShopName = groupBuy.CreatedByShop?.ShopName ?? "",
            groupBuy.CreatedByRole,
            Area = ProjectArea(groupBuy),
            groupBuy.ExpiresAt,
            Status = isExpired ? "expired" : groupBuy.Status,
            ParticipantCount = groupBuy.Participants.Count(Counts),
            groupBuy.CreatedAt,
            Products = groupBuy.Products.Select(p =>
            {
                // Units no shop has taken yet
                var remaining = GroupBuyPayments.Remaining(groupBuy, p, now, mine?.Id);
                return new
                {
                    p.Id,
                    p.ProductId,
                    ProductName = p.Product.Name,
                    MinOrderQty = p.Product.MinOrderQty,
                    MinPerShop = GroupBuyPayments.MinimumFor(p, remaining),
                    Remaining = remaining,
                    OriginalPrice = ShopPricing.ShopPrice(p.OriginalPrice, markup),
                    DiscountPrice = ShopPricing.ShopPrice(p.DiscountPrice, markup),
                    p.DiscountPct,
                    p.TargetQty,
                    p.CurrentQty,
                    ParticipantCount = p.Items.Where(i => i.Status is "joined" or "confirmed")
                        .Select(i => i.ParticipantId).Distinct().Count(),
                    Progress = p.TargetQty <= 0 ? 0m : Math.Clamp(
                        Math.Round((decimal)p.CurrentQty / p.TargetQty * 100m, 1), 0m, 100m),
                    Status = isExpired && p.Status is "active" or "qualified" ? "expired" : p.Status
                };
            }).ToList(),
            // The fewest items this shop must buy in total, across products
            MinTotal = GroupBuyPayments.MinimumTotal(groupBuy, now, mine?.Id),
            // ...and the smallest amount (R, products only), unless the shop takes everything left
            MinAmount = minOrderAmount,
            Participants = ProjectParticipants(groupBuy),
            My = mine is null || !Counts(mine) ? null : new
            {
                mine.Status,
                mine.PaymentStatus,
                mine.PaymentMethod,
                mine.AmountPaid,
                mine.DeliveryFee,
                Items = mine.Items.Where(i => i.Status is "joined" or "confirmed").Select(i => new
                {
                    i.GroupBuyProductId,
                    i.Quantity,
                    i.ShopUnitPrice,
                    i.Status,
                }).ToList(),
            },
        };
    }

    internal static object ProjectSupplier(GroupBuy groupBuy, IEnumerable<Order>? orders = null)
    {
        var isExpired = groupBuy.ExpiresAt <= DateTime.UtcNow && groupBuy.Status == "active";
        return new
        {
            groupBuy.Id,
            groupBuy.Title,
            groupBuy.Description,
            groupBuy.BannerImageUrl,
            groupBuy.SupplierId,
            SupplierName = groupBuy.Supplier.CompanyName,
            CreatedByShopName = groupBuy.CreatedByShop?.ShopName ?? "",
            groupBuy.CreatedByRole,
            Area = ProjectArea(groupBuy),
            groupBuy.RejectionNote,
            groupBuy.ApprovedAt,
            groupBuy.ExpiresAt,
            Status = isExpired ? "expired" : groupBuy.Status,
            ParticipantCount = groupBuy.Participants.Count(Counts),
            groupBuy.CreatedAt,
            Products = groupBuy.Products.Select(p => new
            {
                p.Id,
                p.ProductId,
                ProductName = p.Product.Name,
                MinOrderQty = p.Product.MinOrderQty,
                p.OriginalPrice,
                p.DiscountPrice,
                p.DiscountPct,
                p.TargetQty,
                p.CurrentQty,
                ParticipantCount = p.Items.Where(i => i.Status is "joined" or "confirmed")
                    .Select(i => i.ParticipantId).Distinct().Count(),
                Progress = p.TargetQty <= 0 ? 0m : Math.Clamp(
                    Math.Round((decimal)p.CurrentQty / p.TargetQty * 100m, 1), 0m, 100m),
                Status = isExpired && p.Status is "active" or "qualified" ? "expired" : p.Status
            }).ToList(),
            Participants = ProjectParticipants(groupBuy),
            Orders = orders?.Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.ShopId,
                ShopName = o.Shop?.ShopName ?? "",
                o.Status,
                o.PaymentStatus,
                o.Subtotal
            }).ToList()
        };
    }

    /// <summary>Where shops must be to join; null for campaigns from before areas existed.</summary>
    private static object? ProjectArea(GroupBuy groupBuy) => GroupBuyArea.HasArea(groupBuy)
        ? new
        {
            groupBuy.SupplierBranchId,
            BranchName = groupBuy.SupplierBranchId is null ? "Head office" : groupBuy.SupplierBranch?.Name ?? "Branch",
            City = groupBuy.AreaCity,
            Province = groupBuy.AreaProvince,
            Latitude = groupBuy.AreaLatitude,
            Longitude = groupBuy.AreaLongitude,
            RadiusKm = groupBuy.AreaRadiusKm,
            Description = GroupBuyArea.Describe(groupBuy),
        }
        : null;

    private static object ProjectParticipants(GroupBuy groupBuy) =>
        groupBuy.Participants.Where(p => p.Status != "pending_payment").Select(p => new
        {
            p.Id,
            p.ShopId,
            ShopName = p.Shop.ShopName,
            p.Status,
            p.PaymentMethod,
            p.PaymentStatus,
            p.AmountPaid,
            Items = p.Items.Where(i => i.Status != "reserved").Select(i => new
            {
                i.GroupBuyProductId,
                i.GroupBuyProduct.ProductId,
                ProductName = i.GroupBuyProduct.Product.Name,
                i.Quantity,
                i.Status
            }).ToList()
        }).ToList();

    internal static void SyncLegacy(GroupBuy groupBuy) => GroupBuyPayments.SyncLegacy(groupBuy);
}

[ApiController]
[Route("api/shop/group-buy")]
[Authorize(Roles = "spaza_owner")]
public class ShopGroupBuyController(
    SpazaSureDbContext db,
    ILogger<ShopGroupBuyController> logger,
    DeliveryPricingService pricing,
    OrderPaystackPaymentService paystack,
    EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "active")
    {
        status = status?.Trim().ToLowerInvariant() ?? "active";
        var validStatuses = new[] { "active", "my", "other", "completed", "expired", "cancelled" };
        if (!validStatuses.Contains(status))
            return BadRequest(ApiResponse.Fail(
                "Status must be active, my, other, completed, expired, or cancelled."));

        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var now = DateTime.UtcNow;
        var query = GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking());
        // A shop is in a group buy once it has paid; an open card checkout doesn't count yet
        query = status switch
        {
            "active" => query.Where(g => g.Status == "active" && g.ExpiresAt > now),
            "my" => query.Where(g => g.Participants.Any(p =>
                p.ShopId == shop.Id && p.Status != "cancelled" && p.Status != "pending_payment")),
            "other" => query.Where(g => g.Status == "active" && g.ExpiresAt > now &&
                !g.Participants.Any(p => p.ShopId == shop.Id && p.Status != "cancelled" && p.Status != "pending_payment")),
            "expired" => query.Where(g => g.Status == "expired" ||
                (g.Status == "active" && g.ExpiresAt <= now)),
            _ => query.Where(g => g.Status == status)
        };
        // Open group buys only for the shop's area; ones it already joined stay listed
        if (status is "active" or "other")
        {
            var visible = await OpenToShopAsync(shop, now);
            query = query.Where(g => visible.Contains(g.Id));
        }

        var campaigns = await query.OrderByDescending(g => g.CreatedAt).Take(50).ToListAsync();
        var rules = await PlatformSettingsStore.LoadAsync(db);
        var markup = ShopPricing.Markup(rules);
        return Ok(ApiResponse<object>.Ok(campaigns.Select(g => GroupBuyResponse.ProjectShop(g, markup, shop.Id, rules.MinOrderAmount)).ToList()));
    }

    /// <summary>
    /// Ids of the live campaigns whose area contains the shop. Older campaigns without an area
    /// are open to shops their supplier can reach.
    /// </summary>
    private async Task<HashSet<Guid>> OpenToShopAsync(SpazaShop shop, DateTime now)
    {
        var live = await db.GroupBuys.AsNoTracking()
            .Where(g => g.Status == "active" && g.ExpiresAt > now)
            .Select(g => new GroupBuy
            {
                Id = g.Id, SupplierId = g.SupplierId, AreaCity = g.AreaCity, AreaProvince = g.AreaProvince,
                AreaLatitude = g.AreaLatitude, AreaLongitude = g.AreaLongitude, AreaRadiusKm = g.AreaRadiusKm,
            })
            .ToListAsync();
        HashSet<Guid>? reachable = null;
        if (live.Any(g => !GroupBuyArea.HasArea(g)))
        {
            var rules = await PlatformSettingsStore.LoadAsync(db);
            reachable = await SupplierReach.ReachableSupplierIdsAsync(db, shop, rules.SupplierReachKm);
        }
        return live
            .Where(g => GroupBuyArea.HasArea(g) ? GroupBuyArea.Contains(g, shop, 0m) : reachable!.Contains(g.SupplierId))
            .Select(g => g.Id)
            .ToHashSet();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking())
            .FirstOrDefaultAsync(g => g.Id == id);
        return groupBuy is null
            ? NotFound(ApiResponse.Fail("Group buy not found."))
            : Ok(ApiResponse<object>.Ok(await ProjectAsync(groupBuy.Id, shop.Id)));
    }

    /// <summary>What joining with these quantities costs: items at the group price plus delivery.</summary>
    [HttpPost("{id:guid}/quote")]
    public async Task<IActionResult> Quote(Guid id, [FromBody] JoinGroupBuyRequest req)
    {
        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var groupBuy = await GroupBuyPayments.LoadForUpdateAsync(db, id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));

        var own = groupBuy.Participants.SingleOrDefault(p => p.ShopId == shop.Id);
        var (error, price) = await PriceAsync(groupBuy, shop, req, own?.Status == "pending_payment" ? own.Id : null);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));
        return Ok(ApiResponse<object>.Ok(new
        {
            Items = price!.Lines.Select(l => new { l.Product.Id, ProductName = l.Product.Product.Name, l.Quantity, l.UnitPrice, l.LineTotal }),
            price.ItemsTotal,
            price.DeliveryFee,
            price.Total,
            WalletBalance = await ShopWallet.BalanceAsync(db, shop.Id),
        }));
    }

    /// <summary>
    /// Joins and pays. Wallet: paid and joined at once. Card: the quantities are held while the
    /// shop pays on Paystack (checkoutUrl); the webhook or confirm-payment then joins it.
    /// </summary>
    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, [FromBody] JoinGroupBuyRequest req)
    {
        if (req.Items is null || req.Items.Count == 0)
            return BadRequest(ApiResponse.Fail("At least one item is required."));
        if (req.Items.Select(i => i.GroupBuyProductId).Distinct().Count() != req.Items.Count)
            return BadRequest(ApiResponse.Fail("Group buy product IDs must be unique."));

        // Group buying is for onboarded shops: the once-off onboarding fee comes first
        var unpaid = await db.SpazaShops.AsNoTracking().AnyAsync(s => s.UserId == UserId && !s.OnboardingFeePaid);
        if (unpaid && await OnboardingFee.AmountAsync(db) is > 0m and var fee)
            return StatusCode(StatusCodes.Status402PaymentRequired, ApiResponse.Fail(OnboardingFee.NeededForGroupBuys(fee)));

        var method = req.PaymentMethod?.Trim().ToLowerInvariant();
        if (method is not (GroupBuyPayments.Wallet or GroupBuyPayments.Card))
            return BadRequest(ApiResponse.Fail("Group buys are now paid when you join. Please update the SpazaSure app to join."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var groupBuy = await GroupBuyPayments.LoadForUpdateAsync(db, id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        var now = DateTime.UtcNow;
        // Past its end date: the first time it runs another week, after that it closes
        if (groupBuy.ExpiresAt <= now && groupBuy.Status == "active" && !await ReachEndAsync(groupBuy))
        {
            await transaction.CommitAsync();
            return BadRequest(ApiResponse.Fail("This group buy is no longer active."));
        }
        if (groupBuy.Status != "active")
            return BadRequest(ApiResponse.Fail("This group buy is no longer active."));
        if (!GroupBuyArea.Contains(groupBuy, shop, (await PlatformSettingsStore.LoadAsync(db)).SupplierReachKm))
            return BadRequest(ApiResponse.Fail(GroupBuyArea.HasArea(groupBuy)
                ? $"This group buy is only open to {GroupBuyArea.Describe(groupBuy)}, and your shop is outside that area."
                : $"{groupBuy.Supplier.CompanyName} doesn't deliver to your area."));

        var participant = groupBuy.Participants.SingleOrDefault(p => p.ShopId == shop.Id);
        if (participant is not null && (participant.Status is "joined" or "confirmed" || participant.Items.Any(i => i.Status == "joined")))
            return BadRequest(ApiResponse.Fail("You have already joined this group buy."));
        if (participant?.Status == "pending_payment")
        {
            // An earlier card checkout may have gone through after all: don't charge twice
            if (await TryConfirmCardAsync(groupBuy, participant, now) is { } result)
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                NotifyCardResult(groupBuy, participant, result);
                return BadRequest(ApiResponse.Fail(result == GroupBuyPayments.CardResult.Joined
                    ? "Your earlier payment went through, so you have already joined this group buy."
                    : "Your earlier payment went through, but this group buy filled up meanwhile, so it was added to your wallet."));
            }
        }

        var (error, price) = await PriceAsync(groupBuy, shop, req, participant?.Id);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));

        if (participant is null)
        {
            participant = new GroupBuyParticipant { GroupBuyId = groupBuy.Id, ShopId = shop.Id };
            // Ids are pre-assigned, so EF would treat graph-discovered entities as existing rows (UPDATE) unless added explicitly.
            db.GroupBuyParticipants.Add(participant);
            // EF links tracked entities itself; adding again would count this shop twice
            if (!groupBuy.Participants.Contains(participant)) groupBuy.Participants.Add(participant);
        }
        // A new attempt replaces whatever this shop held before
        foreach (var old in participant.Items.Where(i => i.Status == "reserved")) old.Status = "cancelled";

        var isWallet = method == GroupBuyPayments.Wallet;
        foreach (var line in price!.Lines)
        {
            var item = participant.Items.SingleOrDefault(i => i.GroupBuyProductId == line.Product.Id);
            if (item is null)
            {
                item = new GroupBuyParticipantItem { ParticipantId = participant.Id, GroupBuyProductId = line.Product.Id, GroupBuyProduct = line.Product };
                db.GroupBuyParticipantItems.Add(item);
                if (!participant.Items.Contains(item)) participant.Items.Add(item);
            }
            item.Quantity = line.Quantity;
            item.ShopUnitPrice = line.UnitPrice;
            item.Status = isWallet ? "joined" : "reserved";
        }
        participant.PaymentId = Guid.NewGuid();
        participant.PaymentMethod = method;
        participant.AmountPaid = price.Total;
        participant.DeliveryFee = price.DeliveryFee;

        if (isWallet)
        {
            // Hold the wallet until this commits, so the balance can't be spent twice
            await ShopWallet.LockAsync(db, shop.Id);
            var balance = await ShopWallet.BalanceAsync(db, shop.Id);
            if (balance < price.Total)
                return BadRequest(ApiResponse.Fail(
                    $"Your wallet balance is R{Math.Max(balance, 0):N2} but this group buy costs R{price.Total:N2}. Top up your wallet or pay by card."));
            participant.Status = "joined";
            participant.PaymentStatus = "paid";
            participant.PaidAt = now;
            participant.PaymentReference = GroupBuyPayments.NewReference(participant.Id);
            participant.ReservedUntil = null;
            GroupBuyPayments.Recount(groupBuy);
            await GroupBuyPayments.SyncWalletAsync(db, participant, groupBuy.Title);
            await SaveAsync();
            await transaction.CommitAsync();
            return Ok(ApiResponse<object>.Ok(new
            {
                Paid = true,
                CheckoutUrl = (string?)null,
                Amount = price.Total,
                GroupBuy = await ProjectAsync(id, shop.Id),
            }, $"You joined and paid R{price.Total:N2} from your wallet."));
        }

        participant.Status = "pending_payment";
        participant.PaymentStatus = "pending";
        participant.PaidAt = null;
        participant.ReservedUntil = now.Add(GroupBuyPayments.CardReservation);
        participant.PaymentReference = GroupBuyPayments.NewReference(participant.Id);
        GroupBuyPayments.Recount(groupBuy);
        OrderPaystackCheckout checkout;
        try
        {
            checkout = await paystack.CreateHeldCheckoutAsync(
                price.Total,
                // Shops often have no email; Paystack still needs one (see PaymentEmail).
                PaymentEmail.For(shop.Email, shop.Phone, shop.UserId),
                participant.PaymentReference,
                HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            // Nothing is kept: the reservation is rolled back with the transaction.
            logger.LogWarning(ex, "Paystack checkout failed for group buy {GroupBuyId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponse.Fail(
                "We couldn't open the payment page right now. Please try again in a moment, or pay from your SpazaSure wallet."));
        }
        await SaveAsync();
        await transaction.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            Paid = false,
            checkout.Reference,
            CheckoutUrl = checkout.AuthorizationUrl,
            Amount = price.Total,
        }));
    }

    /// <summary>
    /// Called by the app when the shop comes back from Paystack: asks Paystack whether the card
    /// payment went through, so the shop joins without waiting for the webhook.
    /// </summary>
    [HttpPost("{id:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var groupBuy = await GroupBuyPayments.LoadForUpdateAsync(db, id);
        var participant = groupBuy?.Participants.SingleOrDefault(p => p.ShopId == shop.Id);
        if (groupBuy is null || participant?.PaymentStatus is null)
            return NotFound(ApiResponse.Fail("No payment was started for this group buy."));

        if (participant.PaymentStatus == "pending" &&
            await TryConfirmCardAsync(groupBuy, participant, DateTime.UtcNow) is { } result)
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            NotifyCardResult(groupBuy, participant, result);
        }

        var status = !GroupBuyPayments.IsPaid(participant) ? "pending"
            : participant.Status is "joined" or "confirmed" ? "joined"
            : "refunded";
        return Ok(ApiResponse<object>.Ok(new { Status = status }, status switch
        {
            "joined" => "Payment received. You have joined the group buy.",
            "refunded" => "Payment received, but this group buy filled up or closed meanwhile, so the money is in your wallet.",
            _ => "Paystack hasn't confirmed the payment yet.",
        }));
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var groupBuy = await GroupBuyPayments.LoadForUpdateAsync(db, id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.ExpiresAt <= DateTime.UtcNow && groupBuy.Status == "active" && !await ReachEndAsync(groupBuy))
        {
            await transaction.CommitAsync();
            return BadRequest(ApiResponse.Fail("This group buy is no longer active."));
        }
        if (groupBuy.Status != "active")
            return BadRequest(ApiResponse.Fail("This group buy is no longer active."));
        var participant = groupBuy.Participants.SingleOrDefault(p => p.ShopId == shop.Id && p.Status == "joined");
        if (participant is null || !participant.Items.Any(i => i.Status == "joined"))
            return BadRequest(ApiResponse.Fail("You are not an active participant in this group buy."));
        // Payment is final: the money comes back only if the group buy doesn't reach its target
        if (GroupBuyPayments.IsPaid(participant))
            return BadRequest(ApiResponse.Fail(
                "You have paid for this group buy, so you can't leave it. If it doesn't reach its target, your money goes back to your wallet."));

        foreach (var item in participant.Items.Where(i => i.Status == "joined"))
            item.Status = "cancelled";
        participant.Status = "cancelled";
        GroupBuyPayments.Recount(groupBuy);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { Message = "You have left the group buy." }));
    }

    private sealed record PricedLine(GroupBuyProduct Product, int Quantity, decimal UnitPrice, decimal LineTotal);
    private sealed record Price(List<PricedLine> Lines, decimal ItemsTotal, decimal DeliveryFee, decimal Total);

    /// <summary>Checks the quantities (min 10 each, no more than still needed, in stock) and prices them with delivery.</summary>
    private async Task<(string? Error, Price? Price)> PriceAsync(
        GroupBuy groupBuy, SpazaShop shop, JoinGroupBuyRequest req, Guid? exceptParticipantId)
    {
        if (req.Items is null || req.Items.Count == 0) return ("Choose at least one product.", null);
        var now = DateTime.UtcNow;
        var rules = await PlatformSettingsStore.LoadAsync(db);
        var markup = ShopPricing.Markup(rules);
        var lines = new List<PricedLine>();
        foreach (var input in req.Items)
        {
            var product = groupBuy.Products.SingleOrDefault(p => p.Id == input.GroupBuyProductId);
            if (product is null) return ("One or more products were not found in this group buy.", null);
            if (product.Status is not ("active" or "qualified"))
                return ($"'{product.Product.Name}' is no longer open in this group buy.", null);
            if (GroupBuyPayments.QuantityProblem(groupBuy, product, input.Quantity, now, exceptParticipantId) is { } problem)
                return (problem, null);
            // Stock at the branch the campaign ships from
            var committed = GroupBuyPayments.Committed(groupBuy, product, now, exceptParticipantId);
            var available = BranchStock.AvailableAt(product.Product, groupBuy.SupplierBranchId);
            if (committed + input.Quantity > available)
                return (available - committed <= 0
                    ? $"'{product.Product.Name}' is out of stock at the branch supplying this group buy."
                    : $"Only {available - committed} more of '{product.Product.Name}' can be bought; that is what the supplying branch has left.", null);
            var (unit, total) = ShopPricing.Line(product.DiscountPrice, input.Quantity, markup);
            lines.Add(new PricedLine(product, input.Quantity, unit, total));
        }
        // At least 10 items in total, from one product or a mix
        if (GroupBuyPayments.TotalProblem(groupBuy, lines.Sum(l => l.Quantity), now, exceptParticipantId) is { } tooFew)
            return (tooFew, null);
        // And the minimum order amount, unless this shop takes everything still needed (completing the targets)
        var takesEverythingLeft = lines.Sum(l => l.Quantity) >= groupBuy.Products
            .Where(p => p.Status is "active" or "qualified")
            .Sum(p => GroupBuyPayments.Remaining(groupBuy, p, now, exceptParticipantId));
        if (!takesEverythingLeft &&
            MinimumOrder.Problem(rules, groupBuy.Supplier.CompanyName, lines.Sum(l => l.LineTotal)) is { } tooSmall)
            return (tooSmall, null);

        // Delivered from the branch the campaign runs from (its area centre), else the head office
        var (fromLatitude, fromLongitude) = groupBuy.AreaLatitude is not null && groupBuy.AreaLongitude is not null
            ? (groupBuy.AreaLatitude, groupBuy.AreaLongitude)
            : (groupBuy.Supplier.Latitude, groupBuy.Supplier.Longitude);
        if (!ValidCoordinates(shop.Latitude, shop.Longitude) || string.IsNullOrWhiteSpace(shop.Address))
            return ("Add your shop's address and location in your profile so we can work out the delivery fee.", null);
        if (!ValidCoordinates(fromLatitude, fromLongitude))
            return ("This group buy can't take orders right now: the supplier's location is missing. Please try again later.", null);
        var delivery = pricing.Calculate(fromLatitude!.Value, fromLongitude!.Value, shop.Latitude!.Value, shop.Longitude!.Value,
            rules.GroupBuyBaseDeliveryFee, rules.GroupBuyPerKmRate);

        var itemsTotal = lines.Sum(l => l.LineTotal);
        return (null, new Price(lines, itemsTotal, delivery.DeliveryFee, itemsTotal + delivery.DeliveryFee));
    }

    /// <summary>Asks Paystack about an open card checkout; null when it isn't paid (yet).</summary>
    private async Task<GroupBuyPayments.CardResult?> TryConfirmCardAsync(GroupBuy groupBuy, GroupBuyParticipant participant, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(participant.PaymentReference)) return null;
        long? cents;
        try { cents = await paystack.PaidAmountAsync(participant.PaymentReference, HttpContext.RequestAborted); }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException) { return null; }
        if (cents is null) return null;
        var result = await GroupBuyPayments.ConfirmCardAsync(db, groupBuy, participant, cents.Value, now);
        return result is GroupBuyPayments.CardResult.Joined or GroupBuyPayments.CardResult.RefundedToWallet ? result : null;
    }

    private void NotifyCardResult(GroupBuy groupBuy, GroupBuyParticipant participant, GroupBuyPayments.CardResult result) =>
        GroupBuyNotices.CardPayment(events, groupBuy, participant, result);

    /// <summary>The campaign passed its end date: extends it a week (true) or closes it and refunds (false).</summary>
    private async Task<bool> ReachEndAsync(GroupBuy groupBuy)
    {
        var end = await GroupBuyPayments.ReachEndAsync(db, groupBuy, DateTime.UtcNow);
        await db.SaveChangesAsync();
        if (end.Extended) GroupBuyNotices.Extended(events, groupBuy);
        else GroupBuyNotices.Refunds(events, groupBuy, end.Refunds);
        return end.Extended;
    }

    private async Task SaveAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                var key = entry.Metadata.FindPrimaryKey()?.Properties
                    .Select(property => $"{property.Name}={entry.Property(property.Name).CurrentValue}");
                logger.LogError(
                    ex,
                    "Group buy join concurrency failure for {EntityType} in state {EntityState}; key {EntityKey}",
                    entry.Metadata.ClrType.Name,
                    entry.State,
                    key is null ? "unknown" : string.Join(", ", key));
            }

            throw;
        }
    }

    private async Task<object> ProjectAsync(Guid id, Guid shopId)
    {
        var refreshed = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking()).FirstAsync(g => g.Id == id);
        var rules = await PlatformSettingsStore.LoadAsync(db);
        return GroupBuyResponse.ProjectShop(refreshed, ShopPricing.Markup(rules), shopId, rules.MinOrderAmount);
    }

    private static bool ValidCoordinates(double? latitude, double? longitude) =>
        latitude.HasValue && longitude.HasValue &&
        double.IsFinite(latitude.Value) && double.IsFinite(longitude.Value) &&
        latitude.Value is >= -90d and <= 90d && longitude.Value is >= -180d and <= 180d;
}


/// <summary>
/// Suppliers no longer create group buys: admins run the campaigns. A supplier sees the campaigns
/// for its products, accepts the request once products hit their target (which creates one order
/// per shop at the advertised group price) and then dispatches those orders.
/// </summary>
[ApiController]
[Route("api/supplier/group-buy")]
[Authorize(Roles = "supplier")]
public class SupplierGroupBuyController(
    SpazaSureDbContext db,
    DeliveryPricingService pricing,
    EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Order states a supplier can still move to dispatched in one step.</summary>
    private static readonly string[] Dispatchable = ["pending", "confirmed", "processing"];

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "all")
    {
        status = status.Trim().ToLowerInvariant();
        if (status is not ("all" or "requests" or "active" or "to_dispatch" or "completed" or "expired" or "cancelled"))
            return BadRequest(ApiResponse.Fail("Status must be all, requests, active, to_dispatch, completed, expired, or cancelled."));

        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        // Proposals that never went live are an admin matter, not a supplier request.
        var query = GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking())
            .Where(g => g.SupplierId == supplier.Id && g.Status != "pending_approval" && g.Status != "rejected");
        var now = DateTime.UtcNow;
        query = status switch
        {
            "requests" => query.Where(g => g.Status == "active" && g.ExpiresAt > now &&
                g.Products.Any(p => p.Status == "qualified")),
            "active" => query.Where(g => g.Status == "active" && g.ExpiresAt > now),
            "to_dispatch" => query.Where(g => db.Orders.Any(o =>
                o.GroupBuyId == g.Id && o.SupplierId == supplier.Id && Dispatchable.Contains(o.Status))),
            "expired" => query.Where(g => g.Status == "expired" ||
                (g.Status == "active" && g.ExpiresAt <= now)),
            "completed" => query.Where(g => g.Status == "completed"),
            "cancelled" => query.Where(g => g.Status == "cancelled"),
            _ => query
        };

        var campaigns = await query.OrderByDescending(g => g.CreatedAt).Take(50).ToListAsync();
        var orders = await LoadOrdersAsync(supplier.Id, campaigns.Select(g => g.Id).ToList());
        return Ok(ApiResponse<object>.Ok(campaigns
            .Select(g => GroupBuyResponse.ProjectSupplier(g, orders.GetValueOrDefault(g.Id) ?? []))
            .ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking())
            .FirstOrDefaultAsync(g => g.Id == id && g.SupplierId == supplier.Id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));

        var orders = await LoadOrdersAsync(supplier.Id, [id]);
        return Ok(ApiResponse<object>.Ok(GroupBuyResponse.ProjectSupplier(groupBuy, orders.GetValueOrDefault(id) ?? [])));
    }

    /// <summary>
    /// Dispatches every order this campaign produced that has not left yet, and tells each shop.
    /// Orders the supplier already moved on (or cancelled) are left alone.
    /// </summary>
    [HttpPost("{id:guid}/dispatch")]
    public async Task<IActionResult> Dispatch(Guid id)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        if (!await db.GroupBuys.AnyAsync(g => g.Id == id && g.SupplierId == supplier.Id))
            return NotFound(ApiResponse.Fail("Group buy not found."));

        var orders = await db.Orders
            .Where(o => o.GroupBuyId == id && o.SupplierId == supplier.Id && Dispatchable.Contains(o.Status))
            .ToListAsync();
        // Same rule as single orders: card payments must clear before goods leave.
        var waitingForPayment = orders
            .Where(o => o.PaymentMethod == "paystack" && o.PaymentStatus != "paid")
            .ToList();
        var ready = orders.Except(waitingForPayment).ToList();
        if (ready.Count == 0)
            return BadRequest(ApiResponse.Fail(orders.Count == 0
                ? "There are no group buy orders waiting to be dispatched. Accept the request first."
                : "Every remaining order is still waiting for payment."));

        foreach (var order in ready)
        {
            order.Status = "dispatched";
            order.DeliveryCode ??= await OrderDeliveryCodes.NewAsync(db);
            await ShopWallet.SyncOrderAsync(db, order);
        }
        await db.SaveChangesAsync();

        var shopIds = ready.Select(o => o.ShopId).Distinct().ToList();
        var shopUsers = await db.SpazaShops.AsNoTracking()
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.UserId);
        foreach (var order in ready)
        {
            if (!shopUsers.TryGetValue(order.ShopId, out var shopUserId)) continue;
            events.PublishNotification(
                supplierId: shopUserId.ToString(),
                type: "delivery",
                title: "Order Dispatched",
                message: $"Your group buy order {order.OrderNumber} has been dispatched and is on its way!",
                priority: "normal",
                referenceId: order.Id.ToString(),
                routingKey: "order.dispatched");
        }

        var message = $"{ready.Count} order{(ready.Count == 1 ? "" : "s")} dispatched.";
        if (waitingForPayment.Count > 0)
            message += $" {waitingForPayment.Count} still waiting for payment.";
        return Ok(ApiResponse<object>.Ok(new
        {
            Message = message,
            DispatchedCount = ready.Count,
            WaitingForPaymentCount = waitingForPayment.Count
        }, message));
    }

    private async Task<Dictionary<Guid, List<Order>>> LoadOrdersAsync(Guid supplierId, List<Guid> groupBuyIds)
    {
        if (groupBuyIds.Count == 0) return [];
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Shop)
            .Where(o => o.SupplierId == supplierId && o.GroupBuyId != null && groupBuyIds.Contains(o.GroupBuyId.Value))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
        return orders.GroupBy(o => o.GroupBuyId!.Value).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Accepts the shops' request: qualified products are confirmed at the discount advertised to
    /// shops when the admin created the campaign, and each shop gets one order to dispatch.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [HttpPost("{id:guid}/approve")] // older portal builds
    public async Task<IActionResult> Accept(Guid id)
    {
        var notifications = new List<(Guid UserId, Guid OrderId, string OrderNumber)>();
        List<Guid> orderIds;
        PlatformRules? rules = null; // loaded once, on first use

        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
            if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

            var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys)
                .FirstOrDefaultAsync(g => g.Id == id && g.SupplierId == supplier.Id);
            if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));

            var existingOrders = await db.Orders
                .Include(o => o.Items)
                .Where(o => o.GroupBuyId == id && o.GroupBuyParticipantId != null)
                .ToListAsync();
            var ordersByParticipant = existingOrders.ToDictionary(
                o => o.GroupBuyParticipantId!.Value);
            var existingOrderIds = existingOrders.Select(o => o.Id).ToList();

            if (groupBuy.ExpiresAt <= DateTime.UtcNow && groupBuy.Status == "active")
            {
                // Past its end date: the first time it runs another week, after that it closes
                var end = await GroupBuyPayments.ReachEndAsync(db, groupBuy, DateTime.UtcNow);
                await db.SaveChangesAsync();
                if (end.Extended) GroupBuyNotices.Extended(events, groupBuy);
                else
                {
                    await transaction.CommitAsync();
                    GroupBuyNotices.Refunds(events, groupBuy, end.Refunds);
                    return BadRequest(ApiResponse.Fail("Only an active, unexpired group buy can be accepted."));
                }
            }
            if (groupBuy.Status is not ("active" or "completed"))
                return BadRequest(ApiResponse.Fail("Only an active, unexpired group buy can be accepted."));

            foreach (var product in groupBuy.Products.Where(p => p.Status is "active" or "qualified"))
            {
                product.CurrentQty = product.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);
                product.Status = product.CurrentQty >= product.TargetQty ? "qualified" : "active";
            }

            var qualified = groupBuy.Products
                .Where(p => p.Status == "qualified" && p.CurrentQty >= p.TargetQty)
                .ToDictionary(p => p.Id);
            if (qualified.Count == 0)
            {
                if (existingOrderIds.Count > 0 &&
                    (groupBuy.Status == "completed" || groupBuy.Products.Any(p => p.Status == "approved")))
                {
                    await transaction.CommitAsync();
                    return Ok(ApiResponse<object>.Ok(new
                    {
                        Message = "This request was already accepted.",
                        OrderCount = existingOrderIds.Count,
                        OrderIds = existingOrderIds
                    }));
                }
                return BadRequest(ApiResponse.Fail("No product has reached its target yet, so there is nothing to accept."));
            }

            // Shops committed at the advertised group price, so that is the price they are charged.
            foreach (var product in qualified.Values)
                product.DiscountPrice = Discounted(product.OriginalPrice, product.DiscountPct);
            GroupBuyResponse.SyncLegacy(groupBuy);

            var eligible = groupBuy.Participants
                .Select(p => new
                {
                    Participant = p,
                    Items = p.Items.Where(i => i.Status == "joined" &&
                        qualified.ContainsKey(i.GroupBuyProductId)).ToList()
                })
                .Where(x => x.Items.Count > 0)
                .ToList();
            if (eligible.Count == 0)
            {
                if (existingOrderIds.Count > 0)
                {
                    await transaction.CommitAsync();
                    return Ok(ApiResponse<object>.Ok(new
                    {
                        Message = "Eligible participant orders already exist.",
                        OrderCount = existingOrderIds.Count,
                        OrderIds = existingOrderIds
                    }));
                }
                return BadRequest(ApiResponse.Fail("There are no eligible joined participants to order for."));
            }

            var nonPendingOrders = eligible
                .Where(x => ordersByParticipant.TryGetValue(x.Participant.Id, out var order) &&
                    order.Status != "pending")
                .Select(x => x.Participant.Shop.ShopName)
                .Distinct()
                .ToList();
            if (nonPendingOrders.Count > 0)
                return BadRequest(ApiResponse.Fail(
                    "New qualified items cannot be added because an affected group buy order is no longer pending.",
                    nonPendingOrders));

            // Orders leave from the branch the campaign runs from (its area centre), else the head office
            var (fromLatitude, fromLongitude) = groupBuy.AreaLatitude is not null && groupBuy.AreaLongitude is not null
                ? (groupBuy.AreaLatitude, groupBuy.AreaLongitude)
                : (supplier.Latitude, supplier.Longitude);
            if (!ValidCoordinates(fromLatitude, fromLongitude))
                return BadRequest(ApiResponse.Fail("Supplier delivery coordinates are missing or invalid."));
            var invalidShops = eligible
                .Where(x => string.IsNullOrWhiteSpace(x.Participant.Shop.Address) ||
                    !ValidCoordinates(x.Participant.Shop.Latitude, x.Participant.Shop.Longitude))
                .Select(x => x.Participant.Shop.ShopName).Distinct().ToList();
            if (invalidShops.Count > 0)
                return BadRequest(ApiResponse.Fail(
                    "One or more eligible shops have a blank delivery address or missing/invalid delivery coordinates.", invalidShops));

            var duplicateLines = eligible
                .Where(x => ordersByParticipant.TryGetValue(x.Participant.Id, out var order) &&
                    x.Items.Any(item => order.Items.Any(line =>
                        line.ProductId == qualified[item.GroupBuyProductId].ProductId)))
                .Select(x => x.Participant.Shop.ShopName)
                .Distinct()
                .ToList();
            if (duplicateLines.Count > 0)
                return BadRequest(ApiResponse.Fail(
                    "An affected order already contains one or more joined qualified products.", duplicateLines));

            var quantitiesByProduct = eligible.SelectMany(x => x.Items)
                .GroupBy(i => i.GroupBuyProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            var insufficientStock = qualified.Values
                .Where(p => quantitiesByProduct.GetValueOrDefault(p.Id) > BranchStock.AvailableAt(p.Product, groupBuy.SupplierBranchId))
                .Select(p => $"{p.Product.Name}: requested {quantitiesByProduct.GetValueOrDefault(p.Id)}, available {BranchStock.AvailableAt(p.Product, groupBuy.SupplierBranchId)}")
                .ToList();
            if (insufficientStock.Count > 0)
                return BadRequest(ApiResponse.Fail("Confirmed quantities exceed available stock.", insufficientStock));

            var eligibleItemIds = eligible.SelectMany(x => x.Items).Select(i => i.Id).ToHashSet();
            var omittedItems = qualified.Values
                .Where(p => p.Items.Any(i => i.Status == "joined" && !eligibleItemIds.Contains(i.Id)))
                .Select(p => p.Product.Name)
                .ToList();
            if (omittedItems.Count > 0)
                return BadRequest(ApiResponse.Fail(
                    "All joined quantities for qualified products must be included.", omittedItems));

            var newOrders = new List<Order>();
            var newCodes = new HashSet<string>();
            foreach (var entry in eligible)
            {
                var shop = entry.Participant.Shop;
                rules ??= await PlatformSettingsStore.LoadAsync(db);
                var markup = ShopPricing.Markup(rules);
                // Shops that paid when joining get an order already paid from the money SpazaSure
                // holds, at the prices they paid
                var prepaid = GroupBuyPayments.IsPaid(entry.Participant);
                var newItems = entry.Items.Select(item =>
                {
                    var product = qualified[item.GroupBuyProductId];
                    var (shopUnitPrice, shopLineTotal) = prepaid
                        ? (item.ShopUnitPrice, GroupBuyPayments.LineTotal(item))
                        : ShopPricing.Line(product.DiscountPrice, item.Quantity, markup);
                    return new OrderItem
                    {
                        ProductId = product.ProductId,
                        Quantity = item.Quantity,
                        OriginalUnitPrice = product.OriginalPrice,
                        UnitPrice = product.DiscountPrice,
                        DiscountPct = product.DiscountPct,
                        LineTotal = Math.Round(product.DiscountPrice * item.Quantity, 2,
                            MidpointRounding.AwayFromZero),
                        ShopUnitPrice = shopUnitPrice,
                        ShopLineTotal = shopLineTotal,
                    };
                }).ToList();

                if (!ordersByParticipant.TryGetValue(entry.Participant.Id, out var order))
                {
                    rules ??= await PlatformSettingsStore.LoadAsync(db);
                    var delivery = pricing.Calculate(
                        fromLatitude!.Value, fromLongitude!.Value,
                        shop.Latitude!.Value, shop.Longitude!.Value,
                        rules.GroupBuyBaseDeliveryFee, rules.GroupBuyPerKmRate);
                    order = new Order
                    {
                        OrderNumber = $"SPZ-GB-{Guid.NewGuid():N}".ToUpperInvariant(),
                        ShopId = shop.Id,
                        SupplierId = supplier.Id,
                        GroupBuyId = groupBuy.Id,
                        GroupBuyParticipantId = entry.Participant.Id,
                        GroupBuy = groupBuy,
                        GroupBuyParticipant = entry.Participant,
                        SupplierBranchId = groupBuy.SupplierBranchId,
                        Status = "pending",
                        DeliveryType = "standard",
                        DeliveryAddress = shop.Address!.Trim(),
                        DeliveryLatitude = shop.Latitude,
                        DeliveryLongitude = shop.Longitude,
                        DeliveryFee = prepaid ? entry.Participant.DeliveryFee : delivery.DeliveryFee,
                        DeliveryMarkup = 0m,
                        DeliveryDistanceKm = delivery.DistanceKm,
                        PaymentMethod = prepaid ? ShopWallet.PaymentMethod : "invoice",
                        PaymentStatus = prepaid ? "paid" : "pending",
                        Notes = $"Group Buy: {groupBuy.Title}",
                        // The code the driver checks at the shop; unique across all orders
                        DeliveryCode = await OrderDeliveryCodes.NewAsync(db, newCodes),
                        Items = newItems
                    };
                    newOrders.Add(order);
                    ordersByParticipant.Add(entry.Participant.Id, order);
                }
                else
                {
                    foreach (var item in newItems) order.Items.Add(item);
                }

                // The supplier is paid the subtotal; SpazaSure's fee is built into the shop prices
                order.Subtotal = order.Items.Sum(i => i.LineTotal);
                order.PlatformCommission = order.Items.Sum(i => i.ShopLineTotal) - order.Subtotal;
                order.TotalAmount = order.Subtotal + order.DeliveryFee + order.PlatformCommission;
                notifications.Add((shop.UserId, order.Id, order.OrderNumber));

                foreach (var item in entry.Items) item.Status = "confirmed";
                if (prepaid)
                {
                    // The held money moves into the order: the order debits the wallet, the hold gives it back
                    await ShopWallet.SyncOrderAsync(db, order);
                    await GroupBuyPayments.SyncWalletAsync(db, entry.Participant, groupBuy.Title);
                }
            }

            foreach (var product in qualified.Values)
            {
                var approvedQuantity = quantitiesByProduct.GetValueOrDefault(product.Id);
                BranchStock.Take(product.Product, groupBuy.SupplierBranchId, approvedQuantity);
                if (approvedQuantity > 0 && !product.Items.Any(i => i.Status == "joined"))
                    product.Status = "approved";
            }
            foreach (var participant in groupBuy.Participants)
            {
                var joinedQuantity = participant.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);
                participant.Quantity = joinedQuantity;
                if (participant.Items.Any(i => i.Status == "confirmed") && joinedQuantity == 0)
                    participant.Status = "confirmed";
                else if (joinedQuantity > 0)
                    participant.Status = "joined";
            }
            foreach (var product in groupBuy.Products)
            {
                if (product.Status == "approved")
                {
                    product.CurrentQty = product.Items.Where(i => i.Status == "confirmed").Sum(i => i.Quantity);
                }
                else if (product.Status is "active" or "qualified")
                {
                    product.CurrentQty = product.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);
                    product.Status = product.CurrentQty >= product.TargetQty ? "qualified" : "active";
                }
            }
            groupBuy.Status = groupBuy.Products.Any(p => p.Status is "active" or "qualified")
                ? "active"
                : "completed";
            GroupBuyResponse.SyncLegacy(groupBuy);
            db.Orders.AddRange(newOrders);

            var affectedParticipantIds = eligible.Select(e => e.Participant.Id).ToHashSet();
            try
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
                { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
            {
                await transaction.RollbackAsync();
                db.ChangeTracker.Clear();
                var racedOrders = await db.Orders.AsNoTracking()
                    .Where(o => o.GroupBuyId == id && o.GroupBuyParticipantId != null &&
                        affectedParticipantIds.Contains(o.GroupBuyParticipantId.Value))
                    .Select(o => new { o.Id, ParticipantId = o.GroupBuyParticipantId!.Value })
                    .ToListAsync();
                if (!affectedParticipantIds.SetEquals(racedOrders.Select(o => o.ParticipantId)))
                    throw;
                orderIds = racedOrders.Select(o => o.Id).ToList();
                return Ok(ApiResponse<object>.Ok(new
                {
                    Message = "Eligible participant orders already exist.",
                    OrderCount = orderIds.Count,
                    OrderIds = orderIds
                }));
            }

            orderIds = existingOrderIds.Concat(newOrders.Select(o => o.Id)).Distinct().ToList();
        }

        foreach (var notification in notifications)
        {
            events.PublishNotification(
                supplierId: notification.UserId.ToString(),
                type: "order",
                title: "Group Buy Accepted",
                message: $"The supplier accepted your group buy order {notification.OrderNumber}. It will be dispatched soon.",
                priority: "normal",
                referenceId: notification.OrderId.ToString(),
                routingKey: "order.group_buy_approved");
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            Message = "Request accepted. The shop orders are ready to dispatch.",
            OrderCount = orderIds.Count,
            OrderIds = orderIds
        }));
    }

    private static decimal Discounted(decimal price, int pct) =>
        Math.Round(price * (1m - pct / 100m), 2, MidpointRounding.AwayFromZero);

    private static bool ValidCoordinates(double? latitude, double? longitude) =>
        latitude.HasValue && longitude.HasValue &&
        double.IsFinite(latitude.Value) && double.IsFinite(longitude.Value) &&
        latitude.Value is >= -90d and <= 90d && longitude.Value is >= -180d and <= 180d;
}

public record JoinGroupBuyRequest
{
    public List<JoinGroupBuyItemRequest> Items { get; init; } = [];
    /// <summary>wallet or paystack (card).</summary>
    public string? PaymentMethod { get; init; }
}

public record JoinGroupBuyItemRequest
{
    public Guid GroupBuyProductId { get; init; }
    public int Quantity { get; init; }
}
