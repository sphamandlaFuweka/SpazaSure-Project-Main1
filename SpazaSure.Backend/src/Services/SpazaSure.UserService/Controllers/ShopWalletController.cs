using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;
using System.Security.Claims;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/shop/wallet")]
[Authorize(Roles = "spaza_owner")]
public class ShopWalletController(SpazaSureDbContext db, PaystackPaymentService paystack) : ControllerBase
{
    private const decimal MinimumPaystackTopUp = 50m;
    private const decimal MaximumPaystackTopUp = 2_000m;
    private const decimal MinimumDepositTopUp = 50m;
    private const decimal MaximumDepositTopUp = 100_000m;
    private const int MaximumPendingDeposits = 5;
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get wallet summary: balance and recent transactions.</summary>
    [HttpGet]
    public async Task<IActionResult> GetWallet()
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop == null) return NotFound(ApiResponse.Fail("Shop not found."));

        var balance = await ShopWallet.BalanceAsync(db, shop.Id);
        var totalSpent = await db.Orders
            .Where(o => o.ShopId == shop.Id && (o.Status == "delivered" || o.Status == "confirmed" || o.Status == "dispatched"))
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var pendingDeposits = await db.ShopWalletTransactions
            .Where(t => t.ShopId == shop.Id && t.Type == "top_up" && t.Method == ShopWallet.DirectDeposit && t.Status == "pending")
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var rules = await PlatformSettingsStore.LoadAsync(db);

        var recentTopUps = await db.ShopWalletTransactions
            .Where(t => t.ShopId == shop.Id && t.Type == "top_up")
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .Select(t => new WalletActivity(
                t.Id,
                "credit",
                t.Method == ShopWallet.DirectDeposit
                    ? "Direct deposit" + (t.Reference == null ? "" : " · " + t.Reference)
                    : t.Method == "paystack" ? "Wallet top-up (Paystack)" : "Wallet top-up (" + t.Method + ")",
                t.Amount,
                t.Status,
                t.CreatedAt,
                null,
                null,
                null,
                t.Method,
                t.Reference))
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
                null,
                o.PaymentMethod,
                null))
            .ToListAsync();
        var orderCount = await db.Orders.CountAsync(o =>
            o.ShopId == shop.Id && o.Status != "draft" && o.Status != "cancelled");

        return Ok(ApiResponse<object>.Ok(new {
            Balance = balance,
            PendingDeposits = pendingDeposits,
            TotalSpent = totalSpent,
            DepositAccount = DepositAccount(rules),
            TotalOrders = orderCount,
            Currency = "ZAR",
            Transactions = recentOrders.Concat(recentTopUps)
                .OrderByDescending(t => t.Date)
                .Take(20),
        }));
    }

    /// <summary>
    /// Request a top-up paid outside the app. For a direct deposit ("eft") SpazaSure generates
    /// the reference the shop must use, and returns the bank account to pay into. An admin
    /// approves the request once the money reflects in SpazaSure's account.
    /// </summary>
    [HttpPost("top-ups")]
    public async Task<IActionResult> RequestTopUp([FromBody] WalletTopUpRequest req)
    {
        var method = (req.Method ?? "").Trim().ToLowerInvariant();
        var allowed = new[] { ShopWallet.DirectDeposit, "cash", "kazang", "shop2shop" };
        if (!allowed.Contains(method)) return BadRequest(ApiResponse.Fail("Unsupported top-up method."));
        if (req.Amount < MinimumDepositTopUp || req.Amount > MaximumDepositTopUp || decimal.Round(req.Amount, 2) != req.Amount)
            return BadRequest(ApiResponse.Fail(
                $"Top-ups must be between R{MinimumDepositTopUp:N0} and R{MaximumDepositTopUp:N0} with at most two decimal places."));
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        var pending = await db.ShopWalletTransactions.CountAsync(t =>
            t.ShopId == shop.Id && t.Type == "top_up" && t.Status == "pending" && t.Method != "paystack");
        if (pending >= MaximumPendingDeposits)
            return BadRequest(ApiResponse.Fail("You already have several top-ups waiting for approval. Please wait for those to be confirmed first."));

        var transaction = new ShopWalletTransaction {
            ShopId = shop.Id, Amount = req.Amount, Method = method,
            Reference = string.IsNullOrWhiteSpace(req.Reference) ? null : req.Reference.Trim(),
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            Status = "pending", Type = "top_up"
        };
        if (method == ShopWallet.DirectDeposit) transaction.Reference = ShopWallet.DepositReference(transaction.Id);
        db.ShopWalletTransactions.Add(transaction);
        await db.SaveChangesAsync();

        var rules = await PlatformSettingsStore.LoadAsync(db);
        return Ok(ApiResponse<object>.Ok(new
        {
            transaction.Id, transaction.Status, transaction.Method, transaction.Amount, transaction.Reference,
            DepositAccount = method == ShopWallet.DirectDeposit ? DepositAccount(rules) : null,
        }, method == ShopWallet.DirectDeposit
            ? "Deposit the amount using your reference. Your wallet is credited once SpazaSure confirms the payment."
            : "Top-up request submitted for review."));
    }

    /// <summary>Cancel a top-up that hasn't been approved yet (e.g. the shop changed its mind).</summary>
    [HttpDelete("top-ups/{id:guid}")]
    public async Task<IActionResult> CancelTopUp(Guid id)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var transaction = await db.ShopWalletTransactions.FirstOrDefaultAsync(t => t.Id == id && t.ShopId == shop.Id && t.Type == "top_up");
        if (transaction is null) return NotFound(ApiResponse.Fail("Top-up not found."));
        if (transaction.Status != "pending" || transaction.Method == "paystack")
            return BadRequest(ApiResponse.Fail("Only top-ups waiting for approval can be cancelled."));
        transaction.Status = "cancelled";
        transaction.Notes = "Cancelled by the shop.";
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Top-up cancelled."));
    }

    private static object DepositAccount(PlatformRules rules) => new
    {
        bankName = rules.DepositBankName,
        accountHolder = rules.DepositAccountHolder,
        accountNumber = rules.DepositAccountNumber,
        branchCode = rules.DepositBranchCode,
    };

    [HttpPost("top-ups/paystack/checkout-session")]
    public async Task<IActionResult> CreatePaystackTopUpCheckout(
        [FromBody] PaystackWalletTopUpRequest req,
        CancellationToken cancellationToken)
    {
        if (req.Amount < MinimumPaystackTopUp || req.Amount > MaximumPaystackTopUp ||
            decimal.Round(req.Amount, 2) != req.Amount)
            return BadRequest(ApiResponse.Fail(
                $"Paystack top-ups must be between R{MinimumPaystackTopUp:N0} and R{MaximumPaystackTopUp:N0} with at most two decimal places."));

        var shop = await db.SpazaShops
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == UserId, cancellationToken);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var email = shop.Email ?? shop.User.Email;
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse.Fail("Add an email address to your shop profile before topping up."));

        var transaction = new ShopWalletTransaction
        {
            ShopId = shop.Id,
            Amount = decimal.Round(req.Amount, 2, MidpointRounding.AwayFromZero),
            Method = "paystack",
            Status = "pending",
            Type = "top_up",
            Notes = "Awaiting Paystack checkout payment."
        };
        db.ShopWalletTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var reference = $"WAL-{transaction.Id:N}";
            var checkout = await paystack.CreateOnboardingCheckoutAsync(
                transaction.Amount,
                email,
                reference,
                paystack.GetCallbackUrl("ShopCheckout"),
                cancellationToken);
            transaction.Reference = checkout.Reference;
            await db.SaveChangesAsync(cancellationToken);

            return Ok(ApiResponse<object>.Ok(new
            {
                transactionId = transaction.Id,
                checkoutUrl = checkout.AuthorizationUrl,
                amount = transaction.Amount,
                currency = "ZAR"
            }));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or ArgumentException)
        {
            transaction.Status = "failed";
            transaction.Notes = "Paystack checkout could not be created.";
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
                o.Items.Count,
                o.PaymentMethod,
                null))
            .ToListAsync();
        var topUps = await topUpQuery
            .Take(take)
            .Select(t => new WalletActivity(
                t.Id,
                "credit",
                t.Method == ShopWallet.DirectDeposit
                    ? "Direct deposit" + (t.Reference == null ? "" : " · " + t.Reference)
                    : t.Method == "paystack" ? "Wallet top-up (Paystack)" : "Wallet top-up (" + t.Method + ")",
                t.Amount,
                t.Status,
                t.CreatedAt,
                null,
                null,
                null,
                t.Method,
                t.Reference))
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

public record WalletTopUpRequest(decimal Amount, string? Method, string? Reference, string? Notes);
public record PaystackWalletTopUpRequest(decimal Amount);
public record WalletActivity(
    Guid Id,
    string Type,
    string Description,
    decimal Amount,
    string Status,
    DateTime Date,
    string? OrderNumber = null,
    string? SupplierName = null,
    int? ItemCount = null,
    string? Method = null,
    string? Reference = null);
