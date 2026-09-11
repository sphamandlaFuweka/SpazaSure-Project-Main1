using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/shop/wallet")]
[Authorize(Roles = "spaza_owner")]
public class ShopWalletController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get wallet summary: balance and recent transactions.</summary>
    [HttpGet]
    public async Task<IActionResult> GetWallet()
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop == null) return NotFound(ApiResponse.Fail("Shop not found."));

        // Calculate wallet balance from orders (total spent)
        var totalSpent = await db.Orders
            .Where(o => o.ShopId == shop.Id && (o.Status == "delivered" || o.Status == "confirmed" || o.Status == "dispatched"))
            .SumAsync(o => o.TotalAmount);

        // Recent transactions (orders as debit transactions)
        var recentOrders = await db.Orders
            .Where(o => o.ShopId == shop.Id && o.Status != "draft" && o.Status != "cancelled")
            .OrderByDescending(o => o.CreatedAt)
            .Take(20)
            .Include(o => o.Supplier)
            .Select(o => new {
                Id = o.Id,
                Type = "debit",
                Description = $"Order {o.OrderNumber} - {o.Supplier.CompanyName}",
                Amount = o.TotalAmount,
                Status = o.Status,
                Date = o.CreatedAt,
                OrderNumber = o.OrderNumber,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new {
            Balance = 0.00m, // Starting balance — no top-up system yet
            TotalSpent = totalSpent,
            TotalOrders = recentOrders.Count,
            Currency = "ZAR",
            Transactions = recentOrders,
        }));
    }

    /// <summary>Get transaction history with pagination.</summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop == null) return NotFound(ApiResponse.Fail("Shop not found."));

        var query = db.Orders
            .Where(o => o.ShopId == shop.Id && o.Status != "draft" && o.Status != "cancelled")
            .OrderByDescending(o => o.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(o => o.Supplier)
            .Select(o => new {
                Id = o.Id,
                Type = "debit",
                Description = $"Order {o.OrderNumber} - {o.Supplier.CompanyName}",
                Amount = o.TotalAmount,
                Status = o.Status,
                Date = o.CreatedAt,
                OrderNumber = o.OrderNumber,
                SupplierName = o.Supplier.CompanyName,
                ItemCount = o.Items.Count,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, items }));
    }
}
