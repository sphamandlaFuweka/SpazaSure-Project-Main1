using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.OrderService.Controllers;

public record GroupBuyRequestLine(Guid ProductId, int Quantity);

public record CreateGroupBuyRequest(
    List<GroupBuyRequestLine>? Items,
    int? ShopsWanted,
    DateOnly? OpensOn,
    DateOnly? ClosesOn,
    string? Area,
    int? TargetDiscountPct,
    string? Note);

public record UpdateGroupBuyRequestStatus(string? Status, string? Note);

/// <summary>
/// Shops ask for a group buy they would like to see, from products already on the marketplace,
/// and follow what happens to the request. A request has to meet the same standards as a deal.
/// </summary>
[ApiController]
[Route("api/shop/group-buy/requests")]
[Authorize(Roles = "spaza_owner")]
public class ShopGroupBuyRequestsController(SpazaSureDbContext db) : ControllerBase
{
    /// <summary>Requests still waiting for an answer, so one shop can't flood the admins.</summary>
    private const int MaxOpenRequests = 10;
    /// <summary>The longest a deal can run (see GroupBuyFactory).</summary>
    private const int MaxDays = 60;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var requests = await db.GroupBuyRequests.AsNoTracking()
            .Where(r => r.ShopId == shop.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new
            {
                r.Id, r.ProductName, r.Quantity, r.Note, r.Status, r.AdminNote, r.CreatedAt,
                r.SupplierName, r.ShopsWanted, r.OpensOn, r.ClosesOn, r.Area, r.TargetDiscountPct,
                Items = r.Items.Select(i => new { i.ProductName, i.Quantity }).ToList(),
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(requests));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupBuyRequest req)
    {
        var rules = await PlatformSettingsStore.LoadAsync(db);
        var (error, products) = await CheckAsync(req, rules);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));

        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var open = await db.GroupBuyRequests.CountAsync(r => r.ShopId == shop.Id && r.Status == "new");
        if (open >= MaxOpenRequests)
            return BadRequest(ApiResponse.Fail("You have 10 requests waiting for an answer. Please wait for us to review them."));

        var lines = req.Items!.Select(l => (Line: l, Product: products[l.ProductId])).ToList();
        var first = lines[0].Product;
        var request = new GroupBuyRequest
        {
            ShopId = shop.Id,
            // The admin list and notifications name the request by its products
            ProductName = lines.Count == 1 ? first.Name : $"{first.Name} +{lines.Count - 1} more",
            Quantity = lines.Sum(l => l.Line.Quantity),
            SupplierId = first.SupplierId,
            SupplierName = await db.Suppliers.Where(s => s.Id == first.SupplierId).Select(s => s.CompanyName).FirstOrDefaultAsync(),
            ShopsWanted = req.ShopsWanted,
            OpensOn = req.OpensOn,
            ClosesOn = req.ClosesOn,
            Area = req.Area!.Trim(),
            TargetDiscountPct = req.TargetDiscountPct,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
        };
        foreach (var (line, product) in lines)
            request.Items.Add(new GroupBuyRequestItem { ProductId = product.Id, ProductName = product.Name, Quantity = line.Quantity });
        db.GroupBuyRequests.Add(request);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Request sent. We'll let you know what happens."));
    }

    /// <summary>Why this request can't be sent, or the products it asks for.</summary>
    private async Task<(string? Error, Dictionary<Guid, Product> Products)> CheckAsync(CreateGroupBuyRequest req, PlatformRules rules)
    {
        var none = new Dictionary<Guid, Product>();
        if (req.Items is null) return ("Please update the SpazaSure app to request a group buy.", none);
        if (req.Items.Count == 0) return ("Choose at least one product.", none);
        var ids = req.Items.Select(l => l.ProductId).ToList();
        if (ids.Distinct().Count() != ids.Count) return ("Each product can only be added once.", none);
        if (req.Items.Any(l => l.Quantity is < 1 or > 100_000)) return ("Enter a quantity between 1 and 100,000 for each product.", none);
        var totalItems = req.Items.Sum(l => l.Quantity);
        if (totalItems < GroupBuyPayments.MinPerShop)
            return ($"Each shop must buy not less than {GroupBuyPayments.MinPerShop} items. You can mix products.", none);

        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsApproved && p.IsAvailable)
            .ToDictionaryAsync(p => p.Id);
        if (products.Count != ids.Count) return ("One of these products is no longer available. Choose it again.", none);
        if (products.Values.Select(p => p.SupplierId).Distinct().Count() > 1)
            return ("A group buy is with one supplier, so choose products from the same supplier.", none);

        var spend = req.Items.Sum(l => l.Quantity * ShopPricing.ShopPrice(products[l.ProductId].Price, rules));
        if (rules.MinOrderAmount > 0 && spend < rules.MinOrderAmount)
            return ($"Each shop must spend not less than R{rules.MinOrderAmount:N0}. This comes to R{spend:N0}.", none);

        if (req.ShopsWanted is not { } shops || shops < rules.GroupBuyMinShops || shops > 1000)
            return ($"Enter the number of shops, not less than {rules.GroupBuyMinShops}.", none);

        if (req.OpensOn is not { } opens || req.ClosesOn is not { } closes) return ("Choose the opening and closing dates.", none);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2)); // South African time
        if (opens < today) return ("The opening date can't be in the past.", none);
        if (closes <= opens) return ("The closing date must be after the opening date.", none);
        if (closes.DayNumber - opens.DayNumber > MaxDays) return ($"A group buy can run for {MaxDays} days at most.", none);

        var area = req.Area?.Trim();
        if (string.IsNullOrEmpty(area)) return ("Tell us the area the deal is for.", none);
        if (area.Length > 120) return ("Keep the area under 120 characters.", none);
        if (req.TargetDiscountPct is < 1 or > 99) return ("Enter a discount between 1 and 99 percent.", none);
        if (req.Note?.Trim().Length > 500) return ("Keep the note under 500 characters.", none);
        return (null, products);
    }
}

/// <summary>Admins review what shops asked for. A request is only a suggestion: deals are still created as offers to suppliers.</summary>
[ApiController]
[Route("api/admin/group-buy/requests")]
[Authorize(Roles = "admin")]
public class AdminGroupBuyRequestsController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var requests = await db.GroupBuyRequests.AsNoTracking()
            .OrderByDescending(r => r.Status == "new")
            .ThenByDescending(r => r.CreatedAt)
            .Take(200)
            .Select(r => new
            {
                r.Id, r.ProductName, r.Quantity, r.Note, r.Status, r.AdminNote, r.CreatedAt,
                r.Shop.ShopName, r.Shop.City, r.Shop.Province,
                r.SupplierName, r.ShopsWanted, r.OpensOn, r.ClosesOn, r.Area, r.TargetDiscountPct,
                Items = r.Items.Select(i => new { i.ProductName, i.Quantity }).ToList(),
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(requests));
    }

    /// <summary>Moves a request on and tells the shop: planned, created or declined.</summary>
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateGroupBuyRequestStatus req)
    {
        var note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        if (note?.Length > 500)
            return BadRequest(ApiResponse.Fail("Keep the note under 500 characters."));
        if (req.Status is not ("planned" or "created" or "declined"))
            return BadRequest(ApiResponse.Fail("Status must be planned, created or declined."));

        var request = await db.GroupBuyRequests.Include(r => r.Shop).FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return NotFound(ApiResponse.Fail("Request not found."));

        var unchanged = request.Status == req.Status;
        request.Status = req.Status;
        request.AdminNote = note;
        await db.SaveChangesAsync();

        if (!unchanged)
        {
            var (title, message) = req.Status switch
            {
                "planned" => ("We're working on your group buy", $"We're talking to suppliers about '{request.ProductName}'."),
                "created" => ("Your group buy is on its way", $"We've arranged a deal for '{request.ProductName}'. Look out for it in Group Buys."),
                _ => ("Update on your group buy request", $"We can't arrange a deal for '{request.ProductName}' right now."),
            };
            events.PublishNotification(request.Shop.UserId.ToString(), "group_buy", title,
                note is null ? message : $"{message} {note}", routingKey: "notification.group_buy");
        }
        return Ok(ApiResponse.Ok("Request updated."));
    }
}
