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
    Guid? SupplierId = null);

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
            SupplierId = supplierId,
            ExpiresAt = now.AddDays(req.DurationDays),
            Status = status,
            CreatedByRole = role,
            CreatedByUserId = createdByUserId,
        };

        foreach (var input in req.Products)
        {
            var product = products.Single(p => p.Id == input.ProductId);
            if (input.TargetQty < Math.Max(product.MinOrderQty, 2) || input.TargetQty > 100_000)
                return (null, $"Target for '{product.Name}' must be at least {Math.Max(product.MinOrderQty, 2)}.");
            if (input.DiscountPct is < 1 or > 99)
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
}

/// <summary>Admins create campaigns for any supplier and approve, reject, monitor or cancel them.</summary>
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
        if (status is not ("all" or "pending" or "active" or "completed" or "expired" or "rejected" or "cancelled"))
            return BadRequest(ApiResponse.Fail("Unknown status."));

        var now = DateTime.UtcNow;
        var query = GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking());
        query = status switch
        {
            "pending" => query.Where(g => g.Status == "pending_approval"),
            "active" => query.Where(g => g.Status == "active" && g.ExpiresAt > now),
            "expired" => query.Where(g => g.Status == "expired" || (g.Status == "active" && g.ExpiresAt <= now)),
            "all" => query,
            _ => query.Where(g => g.Status == status)
        };

        var items = await query.OrderByDescending(g => g.CreatedAt).Take(200).ToListAsync();
        return Ok(ApiResponse<object>.Ok(items.Select(GroupBuyResponse.ProjectSupplier).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var groupBuy = await GroupBuyResponse.WithGraph(db.GroupBuys.AsNoTracking()).FirstOrDefaultAsync(g => g.Id == id);
        return groupBuy is null
            ? NotFound(ApiResponse.Fail("Group buy not found."))
            : Ok(ApiResponse<object>.Ok(GroupBuyResponse.ProjectSupplier(groupBuy)));
    }

    [HttpGet("suppliers/{supplierId:guid}/products")]
    public async Task<IActionResult> SupplierProducts(Guid supplierId)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.SupplierId == supplierId && p.IsApproved && p.IsAvailable)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Price, p.StockQty, p.MinOrderQty })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(products));
    }

    /// <summary>Admin-created campaigns skip the approval queue and go live straight away.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NewGroupBuyRequest req)
    {
        if (req.SupplierId is not { } supplierId || !await db.Suppliers.AnyAsync(s => s.Id == supplierId))
            return BadRequest(ApiResponse.Fail("Choose a supplier."));

        var (groupBuy, error) = await GroupBuyFactory.BuildAsync(db, supplierId, req, "admin", UserId, "active");
        if (groupBuy is null) return BadRequest(ApiResponse.Fail(error!));

        groupBuy.ApprovedByUserId = UserId;
        groupBuy.ApprovedAt = DateTime.UtcNow;
        db.GroupBuys.Add(groupBuy);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { groupBuy.Id, groupBuy.Status }, "Group buy created and live."));
    }

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
        if (groupBuy.Status is not ("active" or "pending_approval"))
            return BadRequest(ApiResponse.Fail("Only active or pending campaigns can be cancelled."));

        groupBuy.Status = "cancelled";
        groupBuy.RejectionNote = string.IsNullOrWhiteSpace(req?.Note) ? null : req!.Note!.Trim();
        foreach (var p in groupBuy.Products.Where(p => p.Status is "active" or "qualified" or "pending_approval"))
        {
            p.Status = "cancelled";
            p.CurrentQty = 0;
        }
        foreach (var participant in groupBuy.Participants)
        {
            foreach (var item in participant.Items.Where(i => i.Status == "joined")) item.Status = "cancelled";
            if (participant.Status == "joined") participant.Status = "cancelled";
            participant.Quantity = 0;
        }
        GroupBuyResponse.SyncLegacy(groupBuy);
        await db.SaveChangesAsync();

        Notify(groupBuy, "Group buy cancelled", $"An admin cancelled '{groupBuy.Title}'.");
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
            .Append(".cta{background:#F8B217;color:#0E1B42;text-align:center;font-weight:800;padding:14px;border-radius:14px;margin-top:8px}small{color:#607CC8}</style></head><body><div class=\"card\">");

        if (!live)
        {
            html.Append("<div class=\"head\"><h1>This group buy has ended</h1><p>Open the SpazaSure app to see current deals.</p></div>");
        }
        else
        {
            var left = Math.Max((int)Math.Ceiling((groupBuy!.ExpiresAt - DateTime.UtcNow).TotalDays), 0);
            html.Append("<div class=\"head\"><h1>").Append(E(groupBuy.Title)).Append("</h1><p>From ")
                .Append(E(groupBuy.Supplier.CompanyName)).Append(" &middot; ").Append(left).Append(" day(s) left</p></div><div class=\"body\">");
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
            html.Append("<div class=\"cta\">Open the SpazaSure app and tap Group Buy to join</div></div>");
        }

        html.Append("</div></body></html>");
        return Content(html.ToString(), "text/html; charset=utf-8");
    }
}
