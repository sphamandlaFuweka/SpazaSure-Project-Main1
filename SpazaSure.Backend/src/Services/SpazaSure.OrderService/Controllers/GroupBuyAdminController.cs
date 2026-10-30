using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace SpazaSure.OrderService.Controllers;

public record NewGroupBuyProduct(Guid ProductId, int TargetQty, int DiscountPct);

public record NewGroupBuyRequest(
    string? Title,
    string? Description,
    int DurationDays,
    List<NewGroupBuyProduct>? Products,
    Guid? SupplierId = null,
    string? BannerImageUrl = null,
    // Which warehouse supplies the campaign (null = head office) and how far from it shops may join
    Guid? SupplierBranchId = null,
    decimal? AreaRadiusKm = null,
    // Several places at once: the deal is open within AreaRadiusKm of each, and every shop is
    // served by its nearest. When given, these replace SupplierBranchId.
    List<Guid>? SupplierBranchIds = null,
    bool IncludeHeadOffice = false);

public record RejectGroupBuyRequest(string? Note);

internal static class GroupBuyFactory
{
    /// <summary>Validates a request and builds the campaign. Returns an error message when invalid.</summary>
    internal static async Task<(GroupBuy? GroupBuy, string? Error)> BuildAsync(
        SpazaSureDbContext db, Guid supplierId, NewGroupBuyRequest req,
        string role, Guid createdByUserId, string status)
    {
        var title = req.Title?.Trim() ?? "";
        if (title.Length is < 3 or > 80) return (null, "Give the group buy a title (3 to 80 characters).");
        if (req.DurationDays is < 1 or > 60) return (null, "Duration must be between 1 and 60 days.");
        if (req.Products is null || req.Products.Count == 0) return (null, "Add at least one product.");
        if (req.Products.Select(p => p.ProductId).Distinct().Count() != req.Products.Count)
            return (null, "Each product can only be added once.");

        var banner = req.BannerImageUrl?.Trim();
        if (!ValidBanner(banner)) return (null, "The banner image must be an uploaded image.");

        var supplier = await db.Suppliers.AsNoTracking().Include(s => s.Branches)
            .FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null) return (null, "Choose a supplier.");
        // The places the deal is open around: several branches and/or the head office, or one
        var several = req.SupplierBranchIds is { Count: > 0 } || req.IncludeHeadOffice;
        var (places, placesError) = ResolvePlaces(supplier,
            several ? req.SupplierBranchIds ?? [] : req.SupplierBranchId is { } single ? [single] : [],
            several ? req.IncludeHeadOffice : req.SupplierBranchId is null);
        if (placesError is not null) return (null, placesError);
        var from = places[0];
        var radius = req.AreaRadiusKm ?? (await PlatformSettingsStore.LoadAsync(db)).SupplierReachKm;
        if (radius < 1 || radius > GroupBuyArea.MaxRadiusKm)
            return (null, $"The area radius must be between 1 and {GroupBuyArea.MaxRadiusKm:0} km.");

        var ids = req.Products.Select(p => p.ProductId).ToList();
        var products = await db.Products
            .Where(p => ids.Contains(p.Id) && p.SupplierId == supplierId && p.IsApproved && p.IsAvailable)
            .ToListAsync();
        if (products.Count != ids.Count)
            return (null, "Every product must belong to the supplier and be approved and available.");

        var now = DateTime.UtcNow;
        var groupBuy = new GroupBuy
        {
            Title = title,
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            BannerImageUrl = string.IsNullOrEmpty(banner) ? null : banner,
            SupplierId = supplierId,
            ExpiresAt = now.AddDays(req.DurationDays),
            DurationDays = req.DurationDays,
            Status = status,
            CreatedByRole = role,
            CreatedByUserId = createdByUserId,
            SupplierBranchId = from.BranchId,
            AreaCity = string.IsNullOrWhiteSpace(from.City) ? null : from.City.Trim(),
            AreaProvince = string.IsNullOrWhiteSpace(from.Province) ? null : from.Province.Trim(),
            AreaLatitude = from.Latitude,
            AreaLongitude = from.Longitude,
            AreaRadiusKm = Math.Round(radius, 1),
        };
        foreach (var place in places) groupBuy.Branches.Add(NewPlace(place));

        foreach (var input in req.Products)
        {
            var product = products.Single(p => p.Id == input.ProductId);
            if (input.TargetQty < Math.Max(product.MinOrderQty, 2) || input.TargetQty > 100_000)
                return (null, $"Target for '{product.Name}' must be at least {Math.Max(product.MinOrderQty, 2)}.");
            // An offer may leave the discount to the supplier (0); a live deal needs one
            if (input.DiscountPct is < 0 or > 99 || (status == "active" && input.DiscountPct < 1))
                return (null, $"Discount for '{product.Name}' must be between 1 and 99 percent.");

            groupBuy.Products.Add(new GroupBuyProduct
            {
                ProductId = product.Id,
                OriginalPrice = product.Price,
                DiscountPct = input.DiscountPct,
                DiscountPrice = Math.Round(product.Price * (1m - input.DiscountPct / 100m), 2, MidpointRounding.AwayFromZero),
                TargetQty = input.TargetQty,
                Status = status == "active" ? "active" : "pending_approval",
            });
        }

        GroupBuyResponse.SyncLegacy(groupBuy);
        return (groupBuy, null);
    }

    internal static bool ValidBanner(string? banner) =>
        string.IsNullOrEmpty(banner) ||
        (banner.Length <= 500 &&
         (banner.StartsWith("/uploads/", StringComparison.Ordinal) ||
          banner.StartsWith("https://", StringComparison.OrdinalIgnoreCase)));

    /// <summary>The supplier's places a deal is open around: the chosen active branches and/or the head office.</summary>
    internal static (List<SupplierReach.Location> Places, string? Error) ResolvePlaces(
        Supplier supplier, IEnumerable<Guid> branchIds, bool includeHeadOffice)
    {
        var places = new List<SupplierReach.Location>();
        if (includeHeadOffice)
        {
            var headOffice = SupplierReach.Locations(supplier).First();
            if (headOffice.Latitude is null && string.IsNullOrWhiteSpace(headOffice.City))
                return ([], "The supplier's head office has no address yet. Choose a branch, or ask the supplier to add their address.");
            places.Add(headOffice);
        }
        foreach (var branchId in branchIds.Distinct())
        {
            var branch = supplier.Branches.FirstOrDefault(b => b.Id == branchId && b.IsActive);
            if (branch is null) return ([], "Choose the supplier's active branches.");
            places.Add(new SupplierReach.Location(branch.Id, branch.Name, branch.City, branch.Province, branch.Latitude, branch.Longitude));
        }
        return places.Count == 0 ? ([], "Choose at least one branch (or the head office) for the deal.") : (places, null);
    }

    internal static GroupBuyBranch NewPlace(SupplierReach.Location place) => new()
    {
        SupplierBranchId = place.BranchId,
        Name = place.BranchId is null ? "Head office" : place.Name,
        City = string.IsNullOrWhiteSpace(place.City) ? null : place.City.Trim(),
        Province = string.IsNullOrWhiteSpace(place.Province) ? null : place.Province.Trim(),
        Latitude = place.Latitude,
        Longitude = place.Longitude,
    };

    /// <summary>
    /// An admin edits a deal at any stage, live ones included. On a live deal shops that already
    /// paid keep their price; places and products shops joined through can't be removed, and
    /// products already ordered can't change price. Returns an error, and whether the supplier
    /// should hear about it (prices, targets or products changed). Does not save.
    /// </summary>
    internal static async Task<(string? Error, bool SupplierTermsChanged)> ApplyEditAsync(
        SpazaSureDbContext db, GroupBuy groupBuy, EditGroupBuyRequest req)
    {
        var live = groupBuy.Status == "active";
        var now = DateTime.UtcNow;

        var title = req.Title?.Trim() ?? "";
        if (title.Length is < 3 or > 80) return ("Give the group buy a title (3 to 80 characters).", false);
        var banner = req.BannerImageUrl?.Trim();
        if (!ValidBanner(banner)) return ("The banner image must be an uploaded image.", false);
        groupBuy.Title = title;
        groupBuy.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        groupBuy.BannerImageUrl = string.IsNullOrEmpty(banner) ? null : banner;

        // How long it runs: the length before it is published, the end date once live
        if (req.DurationDays is { } days)
        {
            if (days is < 1 or > 60) return ("Duration must be between 1 and 60 days.", false);
            groupBuy.DurationDays = days;
            if (!live) groupBuy.ExpiresAt = now.AddDays(days);
        }
        if (live && req.ExpiresAt is { } endsAt && endsAt != groupBuy.ExpiresAt)
        {
            if (endsAt <= now || endsAt > now.AddDays(120))
                return ("The end date must be in the future, at most 120 days away.", false);
            groupBuy.ExpiresAt = endsAt;
            groupBuy.EndingReminderSentAt = null; // remind members again before the new end
        }

        // Where it is open
        var radius = req.AreaRadiusKm ?? groupBuy.AreaRadiusKm ?? GroupBuyArea.MaxRadiusKm;
        if (radius < 1 || radius > GroupBuyArea.MaxRadiusKm)
            return ($"The area radius must be between 1 and {GroupBuyArea.MaxRadiusKm:0} km.", false);
        var (places, placesError) = ResolvePlaces(groupBuy.Supplier, req.SupplierBranchIds ?? [], req.IncludeHeadOffice);
        if (placesError is not null) return (placesError, false);
        var keep = places.Select(p => p.BranchId).ToHashSet();
        foreach (var old in GroupBuyArea.Places(groupBuy).Where(p => !keep.Contains(p.SupplierBranchId)).ToList())
        {
            if (groupBuy.Participants.Any(p => p.Status != "cancelled" && GroupBuyArea.Serves(groupBuy, old, p)))
                return ($"{old.Name} can't be removed: shops have joined through it.", false);
            if (old.Id != Guid.Empty)
            {
                groupBuy.Branches.Remove(old);
                db.GroupBuyBranches.Remove(old);
            }
        }
        foreach (var place in places.Where(p => groupBuy.Branches.All(b => b.SupplierBranchId != p.BranchId)))
        {
            var row = NewPlace(place);
            row.GroupBuyId = groupBuy.Id;
            db.GroupBuyBranches.Add(row);
            if (!groupBuy.Branches.Contains(row)) groupBuy.Branches.Add(row);
        }
        var first = GroupBuyArea.Places(groupBuy)[0];
        groupBuy.SupplierBranchId = first.SupplierBranchId;
        groupBuy.AreaCity = first.City;
        groupBuy.AreaProvince = first.Province;
        groupBuy.AreaLatitude = first.Latitude;
        groupBuy.AreaLongitude = first.Longitude;
        groupBuy.AreaRadiusKm = Math.Round(radius, 1);

        // Products: targets and discounts, additions and removals
        if (req.Products is null || req.Products.Count == 0) return ("Keep at least one product.", false);
        if (req.Products.Select(p => p.ProductId).Distinct().Count() != req.Products.Count)
            return ("Each product can only be added once.", false);
        var termsChanged = false;
        var wanted = req.Products.Select(p => p.ProductId).ToHashSet();
        foreach (var gone in groupBuy.Products.Where(p => !wanted.Contains(p.ProductId)).ToList())
        {
            if (gone.Items.Count > 0) return ($"'{gone.Product.Name}' can't be removed: shops have joined it.", false);
            groupBuy.Products.Remove(gone);
            db.GroupBuyProducts.Remove(gone);
            termsChanged = true;
        }
        var newIds = req.Products.Where(p => groupBuy.Products.All(e => e.ProductId != p.ProductId)).Select(p => p.ProductId).ToList();
        var newProducts = await db.Products.Include(p => p.BranchStock)
            .Where(p => newIds.Contains(p.Id) && p.SupplierId == groupBuy.SupplierId && p.IsApproved && p.IsAvailable)
            .ToListAsync();
        if (newProducts.Count != newIds.Count)
            return ("Every product must belong to the supplier and be approved and available.", false);
        foreach (var input in req.Products)
        {
            var existing = groupBuy.Products.FirstOrDefault(p => p.ProductId == input.ProductId);
            var product = existing?.Product ?? newProducts.Single(p => p.Id == input.ProductId);
            if (input.TargetQty < Math.Max(product.MinOrderQty, 2) || input.TargetQty > 100_000)
                return ($"Target for '{product.Name}' must be at least {Math.Max(product.MinOrderQty, 2)}.", false);
            if (input.DiscountPct is < 0 or > 99 || (live && input.DiscountPct < 1))
                return ($"Discount for '{product.Name}' must be between 1 and 99 percent.", false);
            var price = Math.Round(product.Price * (1m - input.DiscountPct / 100m), 2, MidpointRounding.AwayFromZero);
            if (existing is null)
            {
                var added = new GroupBuyProduct
                {
                    GroupBuyId = groupBuy.Id, ProductId = product.Id, Product = product, OriginalPrice = product.Price,
                    DiscountPct = input.DiscountPct, DiscountPrice = price, TargetQty = input.TargetQty,
                    Status = live ? "active" : "pending_approval",
                };
                db.GroupBuyProducts.Add(added);
                if (!groupBuy.Products.Contains(added)) groupBuy.Products.Add(added);
                termsChanged = true;
                continue;
            }
            if (existing.Status is "approved" or "completed" && input.DiscountPct != existing.DiscountPct)
                return ($"'{product.Name}' was already ordered, so its discount can't change.", false);
            if (existing.TargetQty != input.TargetQty || existing.DiscountPct != input.DiscountPct) termsChanged = true;
            existing.TargetQty = input.TargetQty;
            // Shops that already paid keep the price they paid (stored on their items)
            existing.DiscountPct = input.DiscountPct;
            existing.DiscountPrice = price;
        }
        if (live) GroupBuyPayments.Recount(groupBuy);
        else GroupBuyResponse.SyncLegacy(groupBuy);
        return (null, termsChanged);
    }
}

public record GroupBuyProductEdit(Guid ProductId, int TargetQty, int DiscountPct);

/// <summary>Everything an admin can change on a deal. Places replace the current ones.</summary>
public record EditGroupBuyRequest(
    string? Title,
    string? Description,
    string? BannerImageUrl,
    int? DurationDays,
    DateTime? ExpiresAt,
    decimal? AreaRadiusKm,
    List<Guid>? SupplierBranchIds,
    bool IncludeHeadOffice,
    List<GroupBuyProductEdit>? Products);

/// <summary>The supplier's answer to an offer: their discount per product, or why they decline.</summary>
public record SupplierOfferResponse(bool Accept, List<SupplierOfferDiscount>? Discounts, string? Note);
public record SupplierOfferDiscount(Guid GroupBuyProductId, int DiscountPct);

/// <summary>Only admins create campaigns. They also monitor and cancel them, and clear any older supplier proposals still waiting for approval.</summary>
[ApiController]
[Route("api/admin/group-buy")]
[Authorize(Roles = "admin")]
public class AdminGroupBuyController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "all")
    {
        status = status.Trim().ToLowerInvariant();
        if (status is not ("all" or "pending" or "offers" or "declined" or "ready" or "active" or "completed" or "expired" or "rejected" or "cancelled"))
            return BadRequest(ApiResponse.Fail("Unknown status."));

        var now = DateTime.UtcNow;
        var query = GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking());
        query = status switch
        {
            "pending" => query.Where(g => g.Status == "pending_approval"),
            // The offer stages: waiting for the supplier, declined by it, accepted and ready to publish
            "offers" => query.Where(g => g.Status == "awaiting_supplier"),
            "declined" => query.Where(g => g.Status == "supplier_rejected"),
            "ready" => query.Where(g => g.Status == "supplier_accepted"),
            "active" => query.Where(g => g.Status == "active" && g.ExpiresAt > now),
            "expired" => query.Where(g => g.Status == "expired" || (g.Status == "active" && g.ExpiresAt <= now)),
            "all" => query,
            _ => query.Where(g => g.Status == status)
        };

        var items = await query.OrderByDescending(g => g.CreatedAt).Take(200).ToListAsync();
        var orders = await LoadOrdersAsync(items.Select(g => g.Id).ToList());
        return Ok(ApiResponse<object>.Ok(items
            .Select(g => GroupBuyResponse.ProjectSupplier(g, orders.GetValueOrDefault(g.Id) ?? []))
            .ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking()).FirstOrDefaultAsync(g => g.Id == id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        var orders = await LoadOrdersAsync([id]);
        return Ok(ApiResponse<object>.Ok(GroupBuyResponse.ProjectSupplier(groupBuy, orders.GetValueOrDefault(id) ?? [])));
    }

    /// <summary>The orders each campaign produced once its supplier accepted, so admins can follow dispatch.</summary>
    private async Task<Dictionary<Guid, List<Order>>> LoadOrdersAsync(List<Guid> groupBuyIds)
    {
        if (groupBuyIds.Count == 0) return [];
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Shop)
            .Where(o => o.GroupBuyId != null && groupBuyIds.Contains(o.GroupBuyId.Value))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
        return orders.GroupBy(o => o.GroupBuyId!.Value).ToDictionary(g => g.Key, g => g.ToList());
    }

    [HttpGet("suppliers/{supplierId:guid}/products")]
    public async Task<IActionResult> SupplierProducts(Guid supplierId)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.SupplierId == supplierId)
            .OrderByDescending(p => p.IsApproved && p.IsAvailable).ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Price, p.StockQty, p.MinOrderQty, p.IsApproved, p.IsAvailable })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(products));
    }

    /// <summary>Where the supplier can run a campaign from: its head office and active branches.</summary>
    [HttpGet("suppliers/{supplierId:guid}/branches")]
    public async Task<IActionResult> SupplierBranches(Guid supplierId)
    {
        var supplier = await db.Suppliers.AsNoTracking().Include(s => s.Branches)
            .FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var rules = await PlatformSettingsStore.LoadAsync(db);
        return Ok(ApiResponse<object>.Ok(new
        {
            DefaultRadiusKm = Math.Min(rules.SupplierReachKm, GroupBuyArea.MaxRadiusKm),
            MaxRadiusKm = GroupBuyArea.MaxRadiusKm,
            HeadOffice = new { supplier.Address, supplier.City, supplier.Province, supplier.Latitude, supplier.Longitude },
            Branches = supplier.Branches.Where(b => b.IsActive).OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.Address, b.City, b.Province, b.Latitude, b.Longitude })
                .ToList(),
        }));
    }

    /// <summary>
    /// The admin drafts a deal and sends it to the supplier as an offer. The supplier sets its
    /// discount and accepts or declines; the admin then publishes it. Shops can't see it until then.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NewGroupBuyRequest req)
    {
        if (req.SupplierId is not { } supplierId || !await db.Suppliers.AnyAsync(s => s.Id == supplierId))
            return BadRequest(ApiResponse.Fail("Choose a supplier."));

        var (groupBuy, error) = await GroupBuyFactory.BuildAsync(db, supplierId, req, "admin", UserId, "awaiting_supplier");
        if (groupBuy is null) return BadRequest(ApiResponse.Fail(error!));

        db.GroupBuys.Add(groupBuy);
        await db.SaveChangesAsync();

        var supplierUserId = await db.Suppliers.Where(s => s.Id == supplierId).Select(s => s.UserId).FirstAsync();
        NotifyOffer(supplierUserId, groupBuy);
        return Ok(ApiResponse<object>.Ok(new { groupBuy.Id, groupBuy.Status }, "Offer sent to the supplier."));
    }

    /// <summary>Edits a deal at any stage, live ones included (see GroupBuyFactory.ApplyEditAsync).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditGroupBuyRequest req)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM group_buys WHERE id = {id} FOR UPDATE");
        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys).FirstOrDefaultAsync(g => g.Id == id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.Status is "completed" or "expired" or "cancelled" or "rejected")
            return BadRequest(ApiResponse.Fail("This deal has ended, so it can't be edited."));

        var (error, termsChanged) = await GroupBuyFactory.ApplyEditAsync(db, groupBuy, req);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (termsChanged && groupBuy.Status is "active" or "supplier_accepted")
            Notify(groupBuy, "Group buy updated",
                $"SpazaSure updated the products, targets or discounts of '{groupBuy.Title}'. Open it to see the changes.");
        return Ok(ApiResponse<object>.Ok(new { groupBuy.Id, groupBuy.Status }, "Deal saved."));
    }

    /// <summary>Sends (or re-sends, after changes) the offer to the supplier for its discount and answer.</summary>
    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> SendToSupplier(Guid id)
    {
        var groupBuy = await db.GroupBuys.Include(g => g.Supplier).FirstOrDefaultAsync(g => g.Id == id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.Status is not ("awaiting_supplier" or "supplier_accepted" or "supplier_rejected"))
            return BadRequest(ApiResponse.Fail("Only deals that aren't live yet can be sent to the supplier."));

        groupBuy.Status = "awaiting_supplier";
        groupBuy.SupplierNote = null;
        groupBuy.SupplierRespondedAt = null;
        await db.SaveChangesAsync();
        NotifyOffer(groupBuy.Supplier.UserId, groupBuy);
        return Ok(ApiResponse.Ok("Offer sent to the supplier."));
    }

    /// <summary>
    /// Puts the deal live for shops. Normally after the supplier accepted; an admin may also
    /// publish over a decline once the deal is fixed. Every product needs a discount.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var groupBuy = await db.GroupBuys.Include(g => g.Supplier).Include(g => g.Products).ThenInclude(p => p.Product)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.Status is not ("awaiting_supplier" or "supplier_accepted" or "supplier_rejected"))
            return BadRequest(ApiResponse.Fail("Only deals that aren't live yet can be published."));
        if (groupBuy.Products.FirstOrDefault(p => p.DiscountPct < 1) is { } noDiscount)
            return BadRequest(ApiResponse.Fail(
                $"'{noDiscount.Product.Name}' has no discount yet. Wait for the supplier, or set one when editing the deal."));

        var now = DateTime.UtcNow;
        // The countdown starts when the deal goes live
        groupBuy.ExpiresAt = now.AddDays(groupBuy.DurationDays > 0 ? groupBuy.DurationDays : 7);
        groupBuy.Status = "active";
        groupBuy.ApprovedByUserId = UserId;
        groupBuy.ApprovedAt = now;
        foreach (var p in groupBuy.Products) p.Status = "active";
        GroupBuyResponse.SyncLegacy(groupBuy);
        await db.SaveChangesAsync();

        Notify(groupBuy, "Group buy is live",
            $"'{groupBuy.Title}' is now open to shops. Accept the request once shops reach the target, then dispatch.");
        return Ok(ApiResponse.Ok("Deal published: shops can join now."));
    }

    private void NotifyOffer(Guid supplierUserId, GroupBuy groupBuy) =>
        events.PublishNotification(
            supplierId: supplierUserId.ToString(),
            type: "order",
            title: "New group buy offer",
            message: $"SpazaSure would like to run '{groupBuy.Title}' for your products. Set the discount you offer, then accept or decline it.",
            priority: "high",
            referenceId: groupBuy.Id.ToString(),
            routingKey: "notification.group_buy");

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var groupBuy = await db.GroupBuys.Include(g => g.Supplier).Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.Status != "pending_approval")
            return BadRequest(ApiResponse.Fail("Only campaigns waiting for approval can be approved."));

        // The countdown starts when the campaign goes live, not when it was proposed.
        var duration = groupBuy.ExpiresAt - groupBuy.CreatedAt;
        groupBuy.ExpiresAt = DateTime.UtcNow.Add(duration);
        groupBuy.Status = "active";
        groupBuy.ApprovedByUserId = UserId;
        groupBuy.ApprovedAt = DateTime.UtcNow;
        groupBuy.RejectionNote = null;
        foreach (var p in groupBuy.Products) p.Status = "active";
        await db.SaveChangesAsync();

        Notify(groupBuy, "Group buy approved", $"'{groupBuy.Title}' is now live for shops to join.");
        return Ok(ApiResponse.Ok("Group buy approved and live."));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectGroupBuyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Note))
            return BadRequest(ApiResponse.Fail("Tell the supplier why it was rejected."));

        var groupBuy = await db.GroupBuys.Include(g => g.Supplier).Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.Status != "pending_approval")
            return BadRequest(ApiResponse.Fail("Only campaigns waiting for approval can be rejected."));

        groupBuy.Status = "rejected";
        groupBuy.RejectionNote = req.Note.Trim();
        foreach (var p in groupBuy.Products) p.Status = "rejected";
        await db.SaveChangesAsync();

        Notify(groupBuy, "Group buy rejected", $"'{groupBuy.Title}' was not approved: {groupBuy.RejectionNote}");
        return Ok(ApiResponse.Ok("Group buy rejected."));
    }

    /// <summary>Stops a live or pending campaign and releases every shop's commitment.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] RejectGroupBuyRequest? req)
    {
        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys).FirstOrDefaultAsync(g => g.Id == id);
        if (groupBuy is null) return NotFound(ApiResponse.Fail("Group buy not found."));
        if (groupBuy.Status is not ("active" or "pending_approval" or "awaiting_supplier" or "supplier_accepted" or "supplier_rejected"))
            return BadRequest(ApiResponse.Fail("Only live deals and deals that aren't published yet can be cancelled."));

        groupBuy.RejectionNote = string.IsNullOrWhiteSpace(req?.Note) ? null : req!.Note!.Trim();
        // Money held for items that never made it into an order goes back to the shops' wallets
        var refunds = await GroupBuyPayments.CloseAsync(db, groupBuy, "cancelled");
        await db.SaveChangesAsync();

        Notify(groupBuy, "Group buy cancelled", $"An admin cancelled '{groupBuy.Title}'.");
        GroupBuyNotices.Refunds(events, groupBuy, refunds);
        // Shops that joined without paying (before payment at joining) lose their commitment, so tell them too
        var refunded = refunds.Select(r => r.Participant.Id).ToHashSet();
        events.PublishNotificationToMany(
            groupBuy.Participants.Where(p => p.Status == "cancelled" && !refunded.Contains(p.Id) && p.PaymentStatus is null)
                .Select(p => p.Shop.UserId.ToString()),
            "group_buy", "Group buy cancelled",
            $"'{groupBuy.Title}' was cancelled, so your commitment was released.",
            referenceId: groupBuy.Id.ToString(), routingKey: "notification.group_buy");
        return Ok(ApiResponse.Ok("Group buy cancelled."));
    }

    private void Notify(GroupBuy groupBuy, string title, string message) =>
        events.PublishNotification(
            supplierId: groupBuy.Supplier.UserId.ToString(),
            type: "order",
            title: title,
            message: message,
            priority: "normal",
            referenceId: groupBuy.Id.ToString(),
            routingKey: "notification.group_buy");
}

/// <summary>Public, read-only advert page that shops share on WhatsApp and social media.</summary>
[ApiController]
[Route("g")]
[AllowAnonymous]
public class GroupBuyAdvertController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ContentResult> Advert(Guid id)
    {
        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking()).FirstOrDefaultAsync(g => g.Id == id);
        var live = groupBuy is { Status: "active" } && groupBuy.ExpiresAt > DateTime.UtcNow;

        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"robots\" content=\"noindex\">")
            .Append("<title>").Append(E(live ? groupBuy!.Title : "Group buy")).Append(" | SpazaSure</title>")
            .Append("<style>body{font-family:system-ui,sans-serif;background:#EEF2FF;margin:0;padding:24px;color:#0E1B42}")
            .Append(".card{max-width:480px;margin:0 auto;background:#fff;border-radius:20px;overflow:hidden;box-shadow:0 8px 30px rgba(37,68,154,.15)}")
            .Append(".head{background:#25449A;color:#fff;padding:24px}.head h1{margin:0 0 6px;font-size:22px}.head p{margin:0;opacity:.85}")
            .Append(".body{padding:20px}.row{margin-bottom:16px}.bar{height:8px;background:#DCE4FF;border-radius:6px;overflow:hidden}")
            .Append(".bar i{display:block;height:100%;background:#F8B217}.pill{display:inline-block;background:#FFF0BD;color:#AA7206;font-weight:700;font-size:12px;padding:3px 10px;border-radius:99px}")
            .Append(".cta{display:block;text-decoration:none;background:#F8B217;color:#0E1B42;text-align:center;font-weight:800;padding:14px;border-radius:14px;margin-top:8px}.note{text-align:center;font-size:13px;color:#607CC8;margin:10px 0 0}small{color:#607CC8}</style></head><body><div class=\"card\">");

        if (!live)
        {
            html.Append("<div class=\"head\"><h1>This group buy has ended</h1><p>Open the SpazaSure app to see current deals.</p></div>");
        }
        else
        {
            var left = Math.Max((int)Math.Ceiling((groupBuy!.ExpiresAt - DateTime.UtcNow).TotalDays), 0);
            html.Append("<div class=\"head\"><h1>").Append(E(groupBuy.Title)).Append("</h1><p>From ")
                .Append(E(groupBuy.Supplier.CompanyName)).Append(" &middot; ").Append(left).Append(" day(s) left</p></div><div class=\"body\">")
                .Append("<p class=\"pill\">Open to ").Append(E(GroupBuyArea.Describe(groupBuy))).Append("</p>");
            if (!string.IsNullOrWhiteSpace(groupBuy.BannerImageUrl))
                html.Append("<img src=\"").Append(E(groupBuy.BannerImageUrl)).Append("\" alt=\"\" style=\"width:100%;border-radius:12px;margin-bottom:12px\">");
            if (!string.IsNullOrWhiteSpace(groupBuy.Description))
                html.Append("<p>").Append(E(groupBuy.Description)).Append("</p>");
            foreach (var p in groupBuy.Products)
            {
                var pct = p.TargetQty <= 0 ? 0 : Math.Clamp(p.CurrentQty * 100 / p.TargetQty, 0, 100);
                html.Append("<div class=\"row\"><b>").Append(E(p.Product.Name)).Append("</b> <span class=\"pill\">Save ")
                    .Append(p.DiscountPct).Append("%</span><br><small>R").Append(p.DiscountPrice.ToString("0.00"))
                    .Append(" each when the target is reached &middot; ").Append(p.CurrentQty).Append(" of ").Append(p.TargetQty)
                    .Append(" joined</small><div class=\"bar\"><i style=\"width:").Append(pct).Append("%\"></i></div></div>");
            }
            // Opens this deal in the app. On Android an intent link comes back to this page with
            // ?app=missing when the app isn't installed, instead of a Play Store "not found" page.
            html.Append("<a id=\"open\" class=\"cta\" href=\"spazasure://group-buy/").Append(groupBuy.Id)
                .Append("\">Open in the SpazaSure app to join</a>");
            if (Request.Query["app"] == "missing")
                html.Append("<p class=\"note\">The SpazaSure app isn't on this phone yet. Install it, sign in as a shop, then tap the button again.</p>");
            else
                html.Append("<p class=\"note\">Already in the app? Tap Group Buy on the home screen.</p>");
            html.Append("</div><script>if(/Android/i.test(navigator.userAgent)){var back=location.origin+location.pathname+'?app=missing';")
                .Append("document.getElementById('open').href='intent://group-buy/").Append(groupBuy.Id)
                .Append("#Intent;scheme=spazasure;package=com.spazasure.spazasure_app;S.browser_fallback_url='+encodeURIComponent(back)+';end';}</script>");
        }

        html.Append("</div></body></html>");
        return Content(html.ToString(), "text/html; charset=utf-8");
    }
}
