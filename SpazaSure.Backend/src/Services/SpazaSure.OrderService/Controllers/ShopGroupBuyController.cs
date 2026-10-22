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
        .Include(g => g.Supplier)
        .Include(g => g.CreatedByShop)
        .Include(g => g.Products).ThenInclude(p => p.Product)
        .Include(g => g.Products).ThenInclude(p => p.Items)
        .Include(g => g.Participants).ThenInclude(p => p.Shop)
        .Include(g => g.Participants).ThenInclude(p => p.Items)
            .ThenInclude(i => i.GroupBuyProduct).ThenInclude(p => p.Product)
        .AsSplitQuery();

    internal static object ProjectShop(GroupBuy groupBuy)
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
            groupBuy.ExpiresAt,
            Status = isExpired ? "expired" : groupBuy.Status,
            ParticipantCount = groupBuy.Participants.Count(p => p.Status != "cancelled"),
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
                ParticipantCount = p.Items.Where(i => i.Status != "cancelled")
                    .Select(i => i.ParticipantId).Distinct().Count(),
                Progress = p.TargetQty <= 0 ? 0m : Math.Clamp(
                    Math.Round((decimal)p.CurrentQty / p.TargetQty * 100m, 1), 0m, 100m),
                Status = isExpired && p.Status is "active" or "qualified" ? "expired" : p.Status
            }).ToList(),
            Participants = ProjectParticipants(groupBuy)
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
            groupBuy.RejectionNote,
            groupBuy.ApprovedAt,
            groupBuy.ExpiresAt,
            Status = isExpired ? "expired" : groupBuy.Status,
            ParticipantCount = groupBuy.Participants.Count(p => p.Status != "cancelled"),
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
                ParticipantCount = p.Items.Where(i => i.Status != "cancelled")
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

    private static object ProjectParticipants(GroupBuy groupBuy) =>
        groupBuy.Participants.Select(p => new
        {
            p.Id,
            p.ShopId,
            ShopName = p.Shop.ShopName,
            p.Status,
            Items = p.Items.Select(i => new
            {
                i.GroupBuyProductId,
                i.GroupBuyProduct.ProductId,
                ProductName = i.GroupBuyProduct.Product.Name,
                i.Quantity,
                i.Status
            }).ToList()
        }).ToList();

    internal static void SyncLegacy(GroupBuy groupBuy)
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
}

[ApiController]
[Route("api/shop/group-buy")]
[Authorize(Roles = "spaza_owner")]
public class ShopGroupBuyController(
    SpazaSureDbContext db,
    ILogger<ShopGroupBuyController> logger) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<GroupBuy?> LoadGroupBuyForUpdate(Guid id)
    {
        var lockedGroupId = await db.GroupBuys
            .FromSqlInterpolated($"SELECT * FROM group_buys WHERE id = {id} FOR UPDATE")
            .AsNoTracking()
            .Select(groupBuy => groupBuy.Id)
            .SingleOrDefaultAsync();
        if (lockedGroupId == Guid.Empty) return null;

        return await GroupBuyResponse.WithGraph(db.GroupBuys)
            .FirstOrDefaultAsync(groupBuy => groupBuy.Id == lockedGroupId);
    }

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
        query = status switch
        {
            "active" => query.Where(g => g.Status == "active" && g.ExpiresAt > now),
            "my" => query.Where(g => g.Participants.Any(p =>
                p.ShopId == shop.Id && p.Status != "cancelled")),
            "other" => query.Where(g => g.Status == "active" && g.ExpiresAt > now &&
                !g.Participants.Any(p => p.ShopId == shop.Id && p.Status != "cancelled")),
            "expired" => query.Where(g => g.Status == "expired" ||
                (g.Status == "active" && g.ExpiresAt <= now)),
            _ => query.Where(g => g.Status == status)
        };
        // Open group buys only from suppliers near the shop; ones it already joined stay listed
        if (status is "active" or "other")
        {
            var rules = await PlatformSettingsStore.LoadAsync(db);
            var reachable = await SupplierReach.ReachableSupplierIdsAsync(db, shop, rules.SupplierReachKm);
            query = query.Where(g => reachable.Contains(g.SupplierId));
        }

        var campaigns = await query.OrderByDescending(g => g.CreatedAt).Take(50).ToListAsync();
        return Ok(ApiResponse<object>.Ok(campaigns.Select(GroupBuyResponse.ProjectShop).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var shopExists = await db.SpazaShops.AnyAsync(s => s.UserId == UserId);
        if (!shopExists) return NotFound(ApiResponse.Fail("Shop not found."));

        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking())
            .FirstOrDefaultAsync(g => g.Id == id);
        return groupBuy is null
            ? NotFound(ApiResponse.Fail("Group buy not found."))
            : Ok(ApiResponse<object>.Ok(GroupBuyResponse.ProjectShop(groupBuy)));
    }

[HttpPost("{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, [FromBody] JoinGroupBuyRequest req)
    {
        if (req.Items is null || req.Items.Count == 0)
            return BadRequest(ApiResponse.Fail("At least one item is required."));
        if (req.Items.Select(i => i.GroupBuyProductId).Distinct().Count() != req.Items.Count)
            return BadRequest(ApiResponse.Fail("Group buy product IDs must be unique."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var groupBuy = await LoadGroupBuyForUpdate(id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.ExpiresAt <= DateTime.UtcNow && groupBuy.Status == "active")
        {
            groupBuy.Status = "expired";
            foreach (var product in groupBuy.Products.Where(p => p.Status is "active" or "qualified"))
                product.Status = "expired";
            GroupBuyResponse.SyncLegacy(groupBuy);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return BadRequest(ApiResponse.Fail("This group buy is no longer active."));
        }
        if (groupBuy.Status != "active")
            return BadRequest(ApiResponse.Fail("This group buy is no longer active."));
        if (!SupplierReach.CanServe(shop, groupBuy.Supplier, (await PlatformSettingsStore.LoadAsync(db)).SupplierReachKm))
            return BadRequest(ApiResponse.Fail($"{groupBuy.Supplier.CompanyName} doesn't deliver to your area."));

        var requestedIds = req.Items.Select(i => i.GroupBuyProductId).ToHashSet();
        var requestedProducts = groupBuy.Products.Where(p => requestedIds.Contains(p.Id)).ToList();
        if (requestedProducts.Count != requestedIds.Count)
            return BadRequest(ApiResponse.Fail("One or more requested group buy products were not found."));
        if (requestedProducts.Any(p => p.Status is not ("active" or "qualified")))
            return BadRequest(ApiResponse.Fail("Requested products must be active or qualified."));

        foreach (var input in req.Items)
        {
            var product = requestedProducts.Single(p => p.Id == input.GroupBuyProductId);
            if (input.Quantity <= 0 || input.Quantity < product.Product.MinOrderQty)
                return BadRequest(ApiResponse.Fail(
                    $"Minimum order for '{product.Product.Name}' is {product.Product.MinOrderQty}."));
            var committed = product.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);
            if (committed + input.Quantity > product.Product.StockQty)
                return BadRequest(ApiResponse.Fail(
                    $"Requested commitments for '{product.Product.Name}' exceed available stock of {product.Product.StockQty}."));
        }

        var participant = groupBuy.Participants.SingleOrDefault(p => p.ShopId == shop.Id);
        if (participant is not null && (participant.Status == "joined" || participant.Items.Any(i => i.Status == "joined")))
            return BadRequest(ApiResponse.Fail("You have already joined this group buy."));

        if (participant is null)
        {
            participant = new GroupBuyParticipant
            {
                GroupBuyId = groupBuy.Id,
                ShopId = shop.Id,
                Status = "joined"
            };
            // Ids are pre-assigned, so EF would treat graph-discovered entities as existing rows (UPDATE) unless added explicitly.
            db.GroupBuyParticipants.Add(participant);
            groupBuy.Participants.Add(participant);
        }
        else
        {
            participant.Status = "joined";
        }

        foreach (var input in req.Items)
        {
            var product = requestedProducts.Single(p => p.Id == input.GroupBuyProductId);
            var item = participant.Items.SingleOrDefault(i => i.GroupBuyProductId == product.Id);
            if (item is null)
            {
                item = new GroupBuyParticipantItem
                {
                    ParticipantId = participant.Id,
                    GroupBuyProductId = product.Id,
                    GroupBuyProduct = product
                };
                db.GroupBuyParticipantItems.Add(item);
                participant.Items.Add(item);
            }
            else if (item.Status != "cancelled")
            {
                return BadRequest(ApiResponse.Fail($"Participation for '{product.Product.Name}' cannot be reactivated."));
            }

            item.Quantity = input.Quantity;
            item.Status = "joined";
        }

        foreach (var product in groupBuy.Products)
        {
            product.CurrentQty = product.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);
            if (product.Status is "active" or "qualified")
                product.Status = product.CurrentQty >= product.TargetQty ? "qualified" : "active";
        }
        participant.Quantity = participant.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);
        GroupBuyResponse.SyncLegacy(groupBuy);
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
        await transaction.CommitAsync();

        var refreshed = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking())
            .FirstAsync(g => g.Id == id);
        return Ok(ApiResponse<object>.Ok(GroupBuyResponse.ProjectShop(refreshed), "You have joined the group buy."));
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var groupBuy = await LoadGroupBuyForUpdate(id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.ExpiresAt <= DateTime.UtcNow && groupBuy.Status == "active")
        {
            groupBuy.Status = "expired";
            foreach (var product in groupBuy.Products.Where(p => p.Status is "active" or "qualified"))
                product.Status = "expired";
            GroupBuyResponse.SyncLegacy(groupBuy);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return BadRequest(ApiResponse.Fail("This group buy is no longer active."));
        }
        if (groupBuy.Status != "active")
            return BadRequest(ApiResponse.Fail("This group buy is no longer active."));
        var participant = groupBuy.Participants.SingleOrDefault(p => p.ShopId == shop.Id && p.Status == "joined");
        if (participant is null || !participant.Items.Any(i => i.Status == "joined"))
            return BadRequest(ApiResponse.Fail("You are not an active participant in this group buy."));

        foreach (var item in participant.Items.Where(i => i.Status == "joined"))
            item.Status = "cancelled";
        foreach (var product in groupBuy.Products)
        {
            product.CurrentQty = product.Items.Where(i => i.Status == "joined").Sum(i => i.Quantity);
            if (product.Status is "active" or "qualified")
                product.Status = product.CurrentQty >= product.TargetQty ? "qualified" : "active";
        }
        participant.Status = "cancelled";
        participant.Quantity = 0;
        GroupBuyResponse.SyncLegacy(groupBuy);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { Message = "You have left the group buy." }));
    }

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
            if (order.PaymentMethod == ShopOrdersController.CashOnDelivery)
                order.DeliveryCode ??= DeliveryCodes.Generate();
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
                groupBuy.Status = "expired";
                foreach (var product in groupBuy.Products.Where(p => p.Status is "active" or "qualified"))
                    product.Status = "expired";
                GroupBuyResponse.SyncLegacy(groupBuy);
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return BadRequest(ApiResponse.Fail("Only an active, unexpired group buy can be accepted."));
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

            if (!ValidCoordinates(supplier.Latitude, supplier.Longitude))
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
                .Where(p => quantitiesByProduct.GetValueOrDefault(p.Id) > p.Product.StockQty)
                .Select(p => $"{p.Product.Name}: requested {quantitiesByProduct.GetValueOrDefault(p.Id)}, available {p.Product.StockQty}")
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
            foreach (var entry in eligible)
            {
                var shop = entry.Participant.Shop;
                var newItems = entry.Items.Select(item =>
                {
                    var product = qualified[item.GroupBuyProductId];
                    return new OrderItem
                    {
                        ProductId = product.ProductId,
                        Quantity = item.Quantity,
                        OriginalUnitPrice = product.OriginalPrice,
                        UnitPrice = product.DiscountPrice,
                        DiscountPct = product.DiscountPct,
                        LineTotal = Math.Round(product.DiscountPrice * item.Quantity, 2,
                            MidpointRounding.AwayFromZero)
                    };
                }).ToList();

                if (!ordersByParticipant.TryGetValue(entry.Participant.Id, out var order))
                {
                    rules ??= await PlatformSettingsStore.LoadAsync(db);
                    var delivery = pricing.Calculate(
                        supplier.Latitude!.Value, supplier.Longitude!.Value,
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
                        Status = "pending",
                        DeliveryType = "standard",
                        DeliveryAddress = shop.Address!.Trim(),
                        DeliveryLatitude = shop.Latitude,
                        DeliveryLongitude = shop.Longitude,
                        DeliveryFee = delivery.DeliveryFee,
                        DeliveryMarkup = 0m,
                        DeliveryDistanceKm = delivery.DistanceKm,
                        PaymentMethod = "invoice",
                        PaymentStatus = "pending",
                        Notes = $"Group Buy: {groupBuy.Title}",
                        Items = newItems
                    };
                    newOrders.Add(order);
                    ordersByParticipant.Add(entry.Participant.Id, order);
                }
                else
                {
                    foreach (var item in newItems) order.Items.Add(item);
                }

                order.Subtotal = order.Items.Sum(i => i.LineTotal);
                rules ??= await PlatformSettingsStore.LoadAsync(db);
                order.PlatformCommission = Math.Round(
                    order.Subtotal * (supplier.CommissionRate + rules.ShopServiceFeePercent) / 100m, 2,
                    MidpointRounding.AwayFromZero);
                order.TotalAmount = order.Subtotal + order.DeliveryFee + order.PlatformCommission;
                notifications.Add((shop.UserId, order.Id, order.OrderNumber));

                foreach (var item in entry.Items) item.Status = "confirmed";
            }

            foreach (var product in qualified.Values)
            {
                var approvedQuantity = quantitiesByProduct.GetValueOrDefault(product.Id);
                product.Product.StockQty -= approvedQuantity;
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
}

public record JoinGroupBuyItemRequest
{
    public Guid GroupBuyProductId { get; init; }
    public int Quantity { get; init; }
}
