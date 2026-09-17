using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
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

        var credits = await db.ShopWalletTransactions
            .Where(t => t.ShopId == shop.Id && t.Type == "top_up" && t.Status == "approved")
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var debits = await db.Orders
            .Where(o => o.ShopId == shop.Id && (o.Status == "delivered" || o.Status == "confirmed" || o.Status == "dispatched"))
            .SumAsync(o => o.TotalAmount);
        var totalSpent = debits;

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
            Balance = credits - debits,
            TotalSpent = totalSpent,
            TotalOrders = recentOrders.Count,
            Currency = "ZAR",
            Transactions = recentOrders,
        }));
    }

    [HttpPost("top-ups")]
    public async Task<IActionResult> RequestTopUp([FromBody] WalletTopUpRequest req)
    {
        if (req.Amount <= 0) return BadRequest(ApiResponse.Fail("Top-up amount must be greater than zero."));
        var allowed = new[] { "eft", "cash", "kazang", "shop2shop", "stripe" };
        if (!allowed.Contains(req.Method.ToLowerInvariant())) return BadRequest(ApiResponse.Fail("Unsupported top-up method."));
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var transaction = new ShopWalletTransaction {
            ShopId = shop.Id, Amount = req.Amount, Method = req.Method.ToLowerInvariant(),
            Reference = req.Reference, Notes = req.Notes, Status = "pending", Type = "top_up"
        };
        db.ShopWalletTransactions.Add(transaction);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { transaction.Id, transaction.Status, transaction.Method, transaction.Amount }, "Top-up request submitted for review."));
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

public record WalletTopUpRequest(decimal Amount, string Method, string? Reference, string? Notes);
