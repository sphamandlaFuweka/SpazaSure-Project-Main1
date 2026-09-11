using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.OrderService.Controllers;

[ApiController]
[Route("api/shop/orders")]
[Authorize(Roles = "spaza_owner")]
public class ShopOrderRatingController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Submit a delivery rating for an order
    /// </summary>
    [HttpPost("{orderId:guid}/rate")]
    public async Task<IActionResult> RateDelivery(Guid orderId, [FromBody] RateDeliveryRequest req)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var order = await db.Orders
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shop.Id);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        // Save rating to delivery_ratings table
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO delivery_ratings (id, order_id, shop_id, supplier_id, delivery_rating, supplier_rating, comment, created_at)
            VALUES (gen_random_uuid(), {0}, {1}, {2}, {3}, {4}, {5}, NOW())
            ON CONFLICT (order_id) DO UPDATE SET delivery_rating = {3}, supplier_rating = {4}, comment = {5}",
            orderId, shop.Id, order.SupplierId, req.DeliveryRating, req.SupplierRating, req.Comment ?? "");

        // Notify supplier about the rating
        events.PublishNotification(
            supplierId: order.Supplier.UserId.ToString(),
            type: "order",
            title: "New Delivery Rating",
            message: $"{shop.ShopName} rated order {order.OrderNumber}: Delivery {req.DeliveryRating}★, Supplier {req.SupplierRating}★",
            priority: req.DeliveryRating <= 2 ? "high" : "normal",
            referenceId: order.Id.ToString(),
            routingKey: "order.rated"
        );

        return Ok(ApiResponse.Ok("Rating submitted. Thank you!"));
    }

    /// <summary>
    /// Report an issue with a delivery
    /// </summary>
    [HttpPost("{orderId:guid}/report-issue")]
    public async Task<IActionResult> ReportIssue(Guid orderId, [FromBody] ReportIssueRequest req)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var order = await db.Orders
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.ShopId == shop.Id);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        // Save issue report to delivery_disputes table
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO delivery_disputes (id, order_id, shop_id, supplier_id, issue_type, description, status, created_at)
            VALUES (gen_random_uuid(), {0}, {1}, {2}, {3}, {4}, 'open', NOW())",
            orderId, shop.Id, order.SupplierId, req.IssueType, req.Description);

        // Mark order as disputed
        order.Status = "disputed";
        order.Notes = $"Issue reported: {req.IssueType} - {req.Description}";
        await db.SaveChangesAsync();

        // Notify supplier about the issue (high priority)
        events.PublishNotification(
            supplierId: order.Supplier.UserId.ToString(),
            type: "order",
            title: "⚠️ Delivery Issue Reported",
            message: $"{shop.ShopName} reported an issue with order {order.OrderNumber}: {req.IssueType}",
            priority: "high",
            referenceId: order.Id.ToString(),
            routingKey: "order.disputed"
        );

        return Ok(ApiResponse.Ok("Issue reported. We will investigate and contact you."));
    }

    /// <summary>
    /// Get ratings for supplier's orders (supplier-facing)
    /// </summary>
    [HttpGet("/api/supplier/orders/ratings")]
    [Authorize]
    public async Task<IActionResult> GetRatings()
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var ratings = await db.Database.SqlQueryRaw<RatingResult>(@"
            SELECT dr.id, dr.order_id as ""OrderId"", dr.delivery_rating as ""DeliveryRating"",
                   dr.supplier_rating as ""SupplierRating"", dr.comment as ""Comment"",
                   dr.created_at as ""CreatedAt"", o.order_number as ""OrderNumber"",
                   s.shop_name as ""ShopName""
            FROM delivery_ratings dr
            JOIN orders o ON dr.order_id = o.id
            JOIN spaza_shops s ON dr.shop_id = s.id
            WHERE dr.supplier_id = {0}
            ORDER BY dr.created_at DESC LIMIT 50", supplier.Id).ToListAsync();

        var avgDelivery = ratings.Count > 0 ? ratings.Average(r => r.DeliveryRating) : 0;
        var avgSupplier = ratings.Count > 0 ? ratings.Average(r => r.SupplierRating) : 0;

        return Ok(ApiResponse<object>.Ok(new
        {
            averageDeliveryRating = Math.Round(avgDelivery, 1),
            averageSupplierRating = Math.Round(avgSupplier, 1),
            totalRatings = ratings.Count,
            ratings = ratings.Select(r => new
            {
                r.Id, r.OrderId, r.OrderNumber, r.ShopName,
                r.DeliveryRating, r.SupplierRating, r.Comment, r.CreatedAt
            })
        }));
    }

    /// <summary>
    /// Get delivery disputes (supplier-facing)
    /// </summary>
    [HttpGet("/api/supplier/orders/disputes")]
    [Authorize]
    public async Task<IActionResult> GetDisputes()
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var disputes = await db.Database.SqlQueryRaw<DisputeResult>(@"
            SELECT dd.id, dd.order_id as ""OrderId"", dd.issue_type as ""IssueType"",
                   dd.description as ""Description"", dd.status as ""Status"",
                   dd.created_at as ""CreatedAt"", o.order_number as ""OrderNumber"",
                   s.shop_name as ""ShopName""
            FROM delivery_disputes dd
            JOIN orders o ON dd.order_id = o.id
            JOIN spaza_shops s ON dd.shop_id = s.id
            WHERE dd.supplier_id = {0}
            ORDER BY dd.created_at DESC LIMIT 50", supplier.Id).ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            openCount = disputes.Count(d => d.Status == "open"),
            disputes = disputes.Select(d => new
            {
                d.Id, d.OrderId, d.OrderNumber, d.ShopName,
                d.IssueType, d.Description, d.Status, d.CreatedAt
            })
        }));
    }
}

// DTOs
public record RateDeliveryRequest(int DeliveryRating, int SupplierRating, string? Comment);
public record ReportIssueRequest(string IssueType, string Description);

// Query result models
public class RatingResult
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string ShopName { get; set; } = "";
    public int DeliveryRating { get; set; }
    public int SupplierRating { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class DisputeResult
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string ShopName { get; set; } = "";
    public string IssueType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
