using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.AnalyticsService.Controllers;

[ApiController]
[Route("api/supplier/analytics")]
[Authorize]
public class AnalyticsController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfLastMonth = startOfMonth.AddMonths(-1);

        var orders = await db.Orders
            .Where(o => o.SupplierId == supplier.Id && o.Status != "cancelled" && o.Status != "draft")
            .ToListAsync();

        var thisMonth = orders.Where(o => o.CreatedAt >= startOfMonth).ToList();
        var lastMonth = orders.Where(o => o.CreatedAt >= startOfLastMonth && o.CreatedAt < startOfMonth).ToList();

        var totalRevenue = thisMonth.Sum(o => o.TotalAmount);
        var lastRevenue = lastMonth.Sum(o => o.TotalAmount);
        var revenueGrowth = lastRevenue == 0 ? 0 : Math.Round((double)((totalRevenue - lastRevenue) / lastRevenue * 100), 1);

        var totalOrders = thisMonth.Count;
        var lastOrders = lastMonth.Count;
        var ordersGrowth = lastOrders == 0 ? 0 : Math.Round((double)((totalOrders - lastOrders) / (double)lastOrders * 100), 1);

        var products = await db.Products.CountAsync(p => p.SupplierId == supplier.Id && p.IsAvailable);

        return Ok(ApiResponse<object>.Ok(new
        {
            totalRevenue,
            revenueGrowth,
            totalOrders,
            ordersGrowth,
            activeProducts = products,
            pendingOrders = orders.Count(o => o.Status == "pending")
        }));
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] string period = "month")
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var now = DateTime.UtcNow;
        DateTime from = period switch
        {
            "week" => now.AddDays(-7),
            "year" => now.AddYears(-1),
            _ => now.AddMonths(-1)
        };

        var orders = await db.Orders
            .Where(o => o.SupplierId == supplier.Id && o.Status != "cancelled"
                     && o.Status != "draft" && o.CreatedAt >= from)
            .ToListAsync();

        var grouped = period == "year"
            ? orders.GroupBy(o => o.CreatedAt.ToString("yyyy-MM"))
                    .Select(g => new { date = g.Key, revenue = g.Sum(o => o.TotalAmount), orders = g.Count() })
            : orders.GroupBy(o => o.CreatedAt.Date.ToString("yyyy-MM-dd"))
                    .Select(g => new { date = g.Key, revenue = g.Sum(o => o.TotalAmount), orders = g.Count() });

        return Ok(ApiResponse<object>.Ok(grouped.OrderBy(g => g.date)));
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts()
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));

        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var top = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.Product)
            .Where(i => i.Order.SupplierId == supplier.Id
                     && i.Order.Status != "cancelled"
                     && i.Order.CreatedAt >= startOfMonth)
            .GroupBy(i => new { i.ProductId, i.Product.Name })
            .Select(g => new {
                productId = g.Key.ProductId,
                name = g.Key.Name,
                unitsSold = g.Sum(i => i.Quantity),
                revenue = g.Sum(i => i.LineTotal)
            })
            .OrderByDescending(g => g.revenue)
            .Take(5)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(top));
    }
}
