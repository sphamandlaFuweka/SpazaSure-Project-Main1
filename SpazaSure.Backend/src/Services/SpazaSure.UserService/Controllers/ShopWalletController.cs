using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using System.Security.Claims;
using Stripe;
using Stripe.Checkout;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/shop/wallet")]
[Authorize(Roles = "spaza_owner")]
public class ShopWalletController(SpazaSureDbContext db, StripePaymentService stripe) : ControllerBase
{
    private const decimal MinimumStripeTopUp = 50m;
    private const decimal MaximumStripeTopUp = 2_000m;
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

        var recentTopUps = await db.ShopWalletTransactions
            .Where(t => t.ShopId == shop.Id && t.Type == "top_up")
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .Select(t => new WalletActivity(
                t.Id,
                "credit",
                $"Wallet top-up ({t.Method})",
                t.Amount,
                t.Status,
                t.CreatedAt,
                null,
                null,
                null))
            .ToListAsync();

        var recentOrders = await db.Orders
            .Where(o => o.ShopId == shop.Id && o.Status != "draft" && o.Status != "cancelled")
            .OrderByDescending(o => o.CreatedAt)
            .Take(20)
            .Include(o => o.Supplier)
            .Select(o => new WalletActivity(
                o.Id,
                "debit",
                $"Order {o.OrderNumber} - {o.Supplier.CompanyName}",
                o.TotalAmount,
                o.Status,
                o.CreatedAt,
                o.OrderNumber,
                null,
                null))
            .ToListAsync();
        var orderCount = await db.Orders.CountAsync(o =>
            o.ShopId == shop.Id && o.Status != "draft" && o.Status != "cancelled");

        return Ok(ApiResponse<object>.Ok(new {
            Balance = credits - debits,
            TotalSpent = totalSpent,
            TotalOrders = orderCount,
            Currency = "ZAR",
            Transactions = recentOrders.Concat(recentTopUps)
                .OrderByDescending(t => t.Date)
                .Take(20),
        }));
    }

    [HttpPost("top-ups")]
    public async Task<IActionResult> RequestTopUp([FromBody] WalletTopUpRequest req)
    {
        if (req.Amount <= 0) return BadRequest(ApiResponse.Fail("Top-up amount must be greater than zero."));
        var allowed = new[] { "eft", "cash", "kazang", "shop2shop" };
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

    [HttpPost("top-ups/stripe/checkout-session")]
    public async Task<IActionResult> CreateStripeTopUpCheckoutSession(
        [FromBody] StripeWalletTopUpRequest req,
        CancellationToken cancellationToken)
    {
        if (req.Amount < MinimumStripeTopUp || req.Amount > MaximumStripeTopUp)
            return BadRequest(ApiResponse.Fail(
                $"Stripe top-ups must be between R{MinimumStripeTopUp:N0} and R{MaximumStripeTopUp:N0}."));

        var shop = await db.SpazaShops
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == UserId, cancellationToken);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var transaction = new ShopWalletTransaction
        {
            ShopId = shop.Id,
            Amount = decimal.Round(req.Amount, 2, MidpointRounding.AwayFromZero),
            Method = "stripe",
            Status = "pending",
            Type = "top_up",
            Notes = "Awaiting Stripe Checkout payment."
        };
        db.ShopWalletTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            Session session = await stripe.CreateWalletTopUpCheckoutSessionAsync(
                transaction.Amount,
                transaction.Id,
                shop.Email ?? shop.User.Email ?? string.Empty,
                cancellationToken);
            transaction.Reference = session.Id;
            await db.SaveChangesAsync(cancellationToken);

            return Ok(ApiResponse<object>.Ok(new
            {
                transactionId = transaction.Id,
                checkoutUrl = session.Url,
                amount = transaction.Amount,
                currency = "ZAR"
            }));
        }
        catch (Exception ex) when (ex is StripeException or InvalidOperationException)
        {
            transaction.Status = "failed";
            transaction.Notes = "Stripe Checkout session could not be created.";
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Get transaction history with pagination.</summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop == null) return NotFound(ApiResponse.Fail("Shop not found."));

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;
        var take = skip + pageSize;

        var query = db.Orders
            .Where(o => o.ShopId == shop.Id && o.Status != "draft" && o.Status != "cancelled")
            .OrderByDescending(o => o.CreatedAt);

        var orderCount = await query.CountAsync();
        var topUpQuery = db.ShopWalletTransactions
            .Where(t => t.ShopId == shop.Id && t.Type == "top_up")
            .OrderByDescending(t => t.CreatedAt);
        var topUpCount = await topUpQuery.CountAsync();

        var orders = await query
            .Take(take)
            .Include(o => o.Supplier)
            .Select(o => new WalletActivity(
                o.Id,
                "debit",
                $"Order {o.OrderNumber} - {o.Supplier.CompanyName}",
                o.TotalAmount,
                o.Status,
                o.CreatedAt,
                o.OrderNumber,
                o.Supplier.CompanyName,
                o.Items.Count))
            .ToListAsync();
        var topUps = await topUpQuery
            .Take(take)
            .Select(t => new WalletActivity(
                t.Id,
                "credit",
                $"Wallet top-up ({t.Method})",
                t.Amount,
                t.Status,
                t.CreatedAt,
                null,
                null,
                null))
            .ToListAsync();
        var items = orders.Concat(topUps)
            .OrderByDescending(t => t.Date)
            .Skip(skip)
            .Take(pageSize);

        return Ok(ApiResponse<object>.Ok(new
        {
            total = orderCount + topUpCount,
            page,
            pageSize,
            items
        }));
    }
}

public record WalletTopUpRequest(decimal Amount, string Method, string? Reference, string? Notes);
public record StripeWalletTopUpRequest(decimal Amount);
public record WalletActivity(
    Guid Id,
    string Type,
    string Description,
    decimal Amount,
    string Status,
    DateTime Date,
    string? OrderNumber = null,
    string? SupplierName = null,
    int? ItemCount = null);
