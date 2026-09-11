using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            groupBuy.SupplierId,
            SupplierName = groupBuy.Supplier.CompanyName,
            CreatedByShopName = groupBuy.CreatedByShop.ShopName,
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

    internal static object ProjectSupplier(GroupBuy groupBuy)
    {
        var isExpired = groupBuy.ExpiresAt <= DateTime.UtcNow && groupBuy.Status == "active";
        return new
        {
            groupBuy.Id,
            groupBuy.Title,
            groupBuy.Description,
            groupBuy.SupplierId,
            SupplierName = groupBuy.Supplier.CompanyName,
            CreatedByShopName = groupBuy.CreatedByShop.ShopName,
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
public class ShopGroupBuyController(SpazaSureDbContext db) : ControllerBase
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupBuyRequest req)
    {
        var errors = ValidateCreate(req);
        if (errors.Count > 0) return BadRequest(ApiResponse.Fail("Invalid group buy.", errors));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var productIds = req.Products.Select(p => p.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsAvailable && p.IsApproved)
            .ToListAsync();
        if (products.Count != productIds.Count)
            return BadRequest(ApiResponse.Fail("All products must exist, be available, and be approved."));
        if (products.Select(p => p.SupplierId).Distinct().Count() != 1)
            return BadRequest(ApiResponse.Fail("All products must belong to the same supplier."));

        foreach (var input in req.Products)
        {
            var product = products.Single(p => p.Id == input.ProductId);
            if (product.StockQty <= 0)
                errors.Add($"'{product.Name}' must have stock available.");
            if (input.TargetQty <= 0 || input.TargetQty > product.StockQty)
                errors.Add($"Target quantity for '{product.Name}' must be between 1 and the available stock of {product.StockQty}.");
            if (input.MyQty < product.MinOrderQty || input.MyQty > input.TargetQty)
                errors.Add($"Your quantity for '{product.Name}' must be between {product.MinOrderQty} and {input.TargetQty}.");
            if (input.MyQty > product.StockQty)
                errors.Add($"Requested commitments for '{product.Name}' exceed available stock of {product.StockQty}.");
        }
        if (errors.Count > 0) return BadRequest(ApiResponse.Fail("Invalid group buy products.", errors));

        var firstInput = req.Products[0];
        var firstProduct = products.Single(p => p.Id == firstInput.ProductId);
        var groupBuy = new GroupBuy
        {
            Title = string.IsNullOrWhiteSpace(req.Title) ? $"Group Buy: {firstProduct.Name}" : req.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            ProductId = firstProduct.Id,
            SupplierId = firstProduct.SupplierId,
            TargetQty = firstInput.TargetQty,
            CurrentQty = firstInput.MyQty,
            OriginalPrice = firstProduct.Price,
            DiscountPrice = firstProduct.Price,
            DiscountPct = 0,
            ExpiresAt = DateTime.UtcNow.AddDays(req.DurationDays),
            Status = "active",
            CreatedByShopId = shop.Id
        };
        var participant = new GroupBuyParticipant
        {
            GroupBuyId = groupBuy.Id,
            ShopId = shop.Id,
            Quantity = req.Products.Sum(p => p.MyQty),
            Status = "joined"
        };

        foreach (var input in req.Products)
        {
            var product = products.Single(p => p.Id == input.ProductId);
            var normalizedProduct = new GroupBuyProduct
            {
                GroupBuyId = groupBuy.Id,
                ProductId = product.Id,
                OriginalPrice = product.Price,
                DiscountPrice = product.Price,
                DiscountPct = 0,
                TargetQty = input.TargetQty,
                CurrentQty = input.MyQty,
                Status = input.MyQty >= input.TargetQty ? "qualified" : "active"
            };
            groupBuy.Products.Add(normalizedProduct);
            participant.Items.Add(new GroupBuyParticipantItem
            {
                ParticipantId = participant.Id,
                GroupBuyProductId = normalizedProduct.Id,
                Quantity = input.MyQty,
                Status = "joined",
                GroupBuyProduct = normalizedProduct
            });
        }
        groupBuy.Participants.Add(participant);
        db.GroupBuys.Add(groupBuy);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ApiResponse<object>.Ok(new { groupBuy.Id, Message = "Group buy created successfully." }));
    }

    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, [FromBody] JoinGroupBuyRequest req)
    {
        if (req.Items is null || req.Items.Count == 0)
            return BadRequest(ApiResponse.Fail("At least one item is required."));
        if (req.Items.Select(i => i.GroupBuyProductId).Distinct().Count() != req.Items.Count)
            return BadRequest(ApiResponse.Fail("Group buy product IDs must be unique."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys)
            .FirstOrDefaultAsync(g => g.Id == id);
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
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var refreshed = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking())
            .FirstAsync(g => g.Id == id);
        return Ok(ApiResponse<object>.Ok(GroupBuyResponse.ProjectShop(refreshed), "You have joined the group buy."));
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys)
            .FirstOrDefaultAsync(g => g.Id == id);
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

    private static List<string> ValidateCreate(CreateGroupBuyRequest req)
    {
        var errors = new List<string>();
        if (req.Title?.Length > 200) errors.Add("Title cannot exceed 200 characters.");
        if (req.Description?.Length > 2000) errors.Add("Description cannot exceed 2000 characters.");
        if (req.DurationDays is < 1 or > 30) errors.Add("Duration must be between 1 and 30 days.");
        if (req.Products is null || req.Products.Count is < 1 or > 20)
            errors.Add("Products must contain between 1 and 20 items.");
        else if (req.Products.Select(p => p.ProductId).Distinct().Count() != req.Products.Count)
            errors.Add("Product IDs must be unique.");
        return errors;
    }

    private static decimal Discounted(decimal price, int pct) =>
        Math.Round(price * (1m - pct / 100m), 2, MidpointRounding.AwayFromZero);
}

[ApiController]
[Route("api/supplier/group-buy")]
[Authorize(Roles = "supplier")]
public class SupplierGroupBuyController(
    SpazaSureDbContext db,
    DeliveryPricingService pricing,
    EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "all")
    {
        status = status.Trim().ToLowerInvariant();
        if (status is not ("all" or "active" or "completed" or "expired"))
            return BadRequest(ApiResponse.Fail("Status must be all, active, completed, or expired."));

        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var query = GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking())
            .Where(g => g.SupplierId == supplier.Id);
        var now = DateTime.UtcNow;
        query = status switch
        {
            "active" => query.Where(g => g.Status == "active" && g.ExpiresAt > now),
            "expired" => query.Where(g => g.Status == "expired" ||
                (g.Status == "active" && g.ExpiresAt <= now)),
            "completed" => query.Where(g => g.Status == "completed"),
            _ => query
        };

        var campaigns = await query.OrderByDescending(g => g.CreatedAt).Take(50).ToListAsync();
        return Ok(ApiResponse<object>.Ok(campaigns.Select(GroupBuyResponse.ProjectSupplier).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking())
            .FirstOrDefaultAsync(g => g.Id == id && g.SupplierId == supplier.Id);
        return groupBuy is null
            ? NotFound(ApiResponse.Fail("Group buy not found."))
            : Ok(ApiResponse<object>.Ok(GroupBuyResponse.ProjectSupplier(groupBuy)));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveGroupBuyRequest? req)
    {
        if (req?.Products is null || req.Products.Count == 0)
            return BadRequest(ApiResponse.Fail("At least one qualified product discount is required."));
        if (req.Products.Select(p => p.GroupBuyProductId).Distinct().Count() != req.Products.Count)
            return BadRequest(ApiResponse.Fail("Group buy product IDs must be unique."));
        if (req.Products.Any(p => p.DiscountPct is < 1 or > 99))
            return BadRequest(ApiResponse.Fail("Discount percentages must be integers between 1 and 99."));

        var notifications = new List<(Guid UserId, Guid OrderId, string OrderNumber)>();
        List<Guid> orderIds;

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
                return BadRequest(ApiResponse.Fail("Only an active, unexpired group buy can be approved."));
            }
            if (groupBuy.Status is not ("active" or "completed"))
                return BadRequest(ApiResponse.Fail("Only an active, unexpired group buy can be approved."));

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
                        Message = "Qualified group buy products were already approved.",
                        OrderCount = existingOrderIds.Count,
                        OrderIds = existingOrderIds
                    }));
                }
                return BadRequest(ApiResponse.Fail("At least one product must be qualified before approval."));
            }

            var requestedDiscounts = req.Products.ToDictionary(p => p.GroupBuyProductId, p => p.DiscountPct);
            if (!qualified.Keys.ToHashSet().SetEquals(requestedDiscounts.Keys))
                return BadRequest(ApiResponse.Fail(
                    "Products must exactly match the current qualified group buy product set."));

            foreach (var product in qualified.Values)
            {
                product.DiscountPct = requestedDiscounts[product.Id];
                product.DiscountPrice = Discounted(product.OriginalPrice, product.DiscountPct);
            }
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
                    "All joined quantities for qualified products must be included in approval.", omittedItems));

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
                    var delivery = pricing.Calculate(
                        supplier.Latitude!.Value, supplier.Longitude!.Value,
                        shop.Latitude!.Value, shop.Longitude!.Value);
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
                order.PlatformCommission = Math.Round(
                    order.Subtotal * supplier.CommissionRate / 100m, 2,
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
                title: "Group Buy Approved",
                message: $"Your group buy order {notification.OrderNumber} has been approved.",
                priority: "normal",
                referenceId: notification.OrderId.ToString(),
                routingKey: "order.group_buy_approved");
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            Message = "Qualified group buy products approved successfully.",
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

public record ApproveGroupBuyRequest
{
    public List<ApproveGroupBuyProductRequest> Products { get; init; } = [];
}

public record ApproveGroupBuyProductRequest
{
    public Guid GroupBuyProductId { get; init; }
    public int DiscountPct { get; init; }
}

public record CreateGroupBuyRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public int DurationDays { get; init; }
    public List<CreateGroupBuyProductRequest> Products { get; init; } = [];
}

public record CreateGroupBuyProductRequest
{
    public Guid ProductId { get; init; }
    public int TargetQty { get; init; }
    public int MyQty { get; init; }
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
