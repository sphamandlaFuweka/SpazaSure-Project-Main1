using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.OrderService.Controllers;

public record CreateGroupBuyRequest(string? ProductName, int? Quantity, string? Note);

public record UpdateGroupBuyRequestStatus(string? Status, string? Note);

/// <summary>Shops ask for a group buy they would like to see, and follow what happens to the request.</summary>
[ApiController]
[Route("api/shop/group-buy/requests")]
[Authorize(Roles = "spaza_owner")]
public class ShopGroupBuyRequestsController(SpazaSureDbContext db) : ControllerBase
{
    /// <summary>Requests still waiting for an answer, so one shop can't flood the admins.</summary>
    private const int MaxOpenRequests = 10;

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
            .Select(r => new { r.Id, r.ProductName, r.Quantity, r.Note, r.Status, r.AdminNote, r.CreatedAt })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(requests));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupBuyRequest req)
    {
        var productName = req.ProductName?.Trim();
        if (string.IsNullOrEmpty(productName))
            return BadRequest(ApiResponse.Fail("Tell us which product you want."));
        if (productName.Length > 120)
            return BadRequest(ApiResponse.Fail("Keep the product name under 120 characters."));
        if (req.Quantity is < 1 or > 100_000)
            return BadRequest(ApiResponse.Fail("Enter a quantity between 1 and 100,000."));
        var note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        if (note?.Length > 500)
            return BadRequest(ApiResponse.Fail("Keep the note under 500 characters."));

        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var open = await db.GroupBuyRequests.CountAsync(r => r.ShopId == shop.Id && r.Status == "new");
        if (open >= MaxOpenRequests)
            return BadRequest(ApiResponse.Fail("You have 10 requests waiting for an answer. Please wait for us to review them."));

        db.GroupBuyRequests.Add(new GroupBuyRequest
        {
            ShopId = shop.Id,
            ProductName = productName,
            Quantity = req.Quantity,
            Note = note,
        });
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Request sent. We'll let you know what happens."));
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
