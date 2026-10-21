using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.Security.Claims;
using SpazaSure.UserService.Services;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/shop/wallet")]
[Authorize(Roles = "spaza_owner")]
public class ShopWalletController(SpazaSureDbContext db, PaystackPaymentService paystack, EventPublisher events) : ControllerBase
{
    private const decimal MinimumPaystackTopUp = 50m;
    private const decimal MaximumPaystackTopUp = 2_000m;
    private const decimal MinimumDepositTopUp = 50m;
    private const decimal MaximumDepositTopUp = 100_000m;
    private const int MaximumPendingDeposits = 5;
    private const decimal MinimumWithdrawal = 50m;
    private const decimal MaximumWithdrawal = 100_000m;
    private const int MaximumPendingWithdrawals = 3;
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
        var pendingWithdrawals = await db.ShopWalletTransactions
            .Where(t => t.ShopId == shop.Id && t.Type == ShopWallet.Withdrawal && t.Status == "pending")
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var rules = await PlatformSettingsStore.LoadAsync(db);

        var (activity, _) = await LoadActivityAsync(shop.Id, balance, 20);
        var orderCount = await db.Orders.CountAsync(o =>
            o.ShopId == shop.Id && o.Status != "draft" && o.Status != "cancelled");

        return Ok(ApiResponse<object>.Ok(new {
            Balance = balance,
            PendingDeposits = pendingDeposits,
            PendingWithdrawals = pendingWithdrawals,
            MinimumWithdrawal = MinimumWithdrawal,
            TotalSpent = totalSpent,
            DepositAccount = DepositAccount(rules),
            TotalOrders = orderCount,
            Currency = "ZAR",
            Transactions = activity,
        }));
    }

    /// <summary>
    /// The newest wallet activity: every ledger entry (with the balance after it) plus top-ups that
    /// haven't been credited yet, so a shop can see deposits that are still waiting.
    /// </summary>
    private async Task<(List<WalletActivity> Items, int Total)> LoadActivityAsync(Guid shopId, decimal balance, int take)
    {
        var entries = await db.ShopWalletLedgerEntries
            .Where(e => e.ShopId == shopId)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Take(take)
            .ToListAsync();
        var entryCount = await db.ShopWalletLedgerEntries.CountAsync(e => e.ShopId == shopId);

        // Withdrawals show their own status (pending, paid out, declined) rather than "completed"
        var sourceIds = entries.Where(e => e.SourceId != null).Select(e => e.SourceId!.Value).ToList();
        var withdrawalStatus = await db.ShopWalletTransactions
            .Where(t => sourceIds.Contains(t.Id) && t.Type == ShopWallet.Withdrawal)
            .ToDictionaryAsync(t => t.Id, t => t.Status);

        var items = new List<WalletActivity>();
        var running = balance;
        foreach (var e in entries)
        {
            var isWithdrawal = e.SourceId is { } id && withdrawalStatus.ContainsKey(id);
            items.Add(new WalletActivity(
                e.SourceId ?? e.Id,
                e.Amount < 0 ? "debit" : "credit",
                e.Description,
                Math.Abs(e.Amount),
                isWithdrawal ? withdrawalStatus[e.SourceId!.Value] : "completed",
                e.CreatedAt,
                e.Type is ShopWallet.Entry.OrderPayment or ShopWallet.Entry.OrderRefund ? e.Reference : null,
                null,
                null,
                isWithdrawal ? ShopWallet.Withdrawal : e.Type,
                e.Reference,
                running));
            running -= e.Amount;
        }

        var waiting = db.ShopWalletTransactions
            .Where(t => t.ShopId == shopId && t.Type == "top_up" && t.Status != "approved");
        var waitingCount = await waiting.CountAsync();
        var topUps = await waiting
            .OrderByDescending(t => t.CreatedAt)
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
                t.Reference,
                null))
            .ToListAsync();

        return (items.Concat(topUps).OrderByDescending(a => a.Date).ToList(), entryCount + waitingCount);
    }

    /// <summary>
    /// Ask for money in the wallet to be paid out to the shop's bank account. The amount leaves the
    /// wallet straight away (so it can't be spent twice) and is returned if the request is declined or cancelled.
    /// </summary>
    [HttpPost("withdrawals")]
    public async Task<IActionResult> RequestWithdrawal([FromBody] WalletWithdrawalRequest req)
    {
        if (req.Amount < MinimumWithdrawal || req.Amount > MaximumWithdrawal || decimal.Round(req.Amount, 2) != req.Amount)
            return BadRequest(ApiResponse.Fail(
                $"Withdrawals must be between R{MinimumWithdrawal:N0} and R{MaximumWithdrawal:N0} with at most two decimal places."));

        var bankName = req.BankName?.Trim();
        var holder = req.AccountHolder?.Trim();
        var accountNumber = new string((req.AccountNumber ?? "").Where(char.IsDigit).ToArray());
        var branchCode = new string((req.BranchCode ?? "").Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(bankName) || string.IsNullOrEmpty(holder))
            return BadRequest(ApiResponse.Fail("Enter the bank name and the account holder's name."));
        if (accountNumber.Length is < 6 or > 20)
            return BadRequest(ApiResponse.Fail("Enter a valid bank account number."));
        if (branchCode.Length is not (0 or 6))
            return BadRequest(ApiResponse.Fail("The branch code must be 6 digits."));

        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        await using var dbTransaction = await db.Database.BeginTransactionAsync();
        // Hold the wallet so a checkout and a withdrawal can't spend the same money
        await ShopWallet.LockAsync(db, shop.Id);

        var pending = await db.ShopWalletTransactions.CountAsync(t =>
            t.ShopId == shop.Id && t.Type == ShopWallet.Withdrawal && t.Status == "pending");
        if (pending >= MaximumPendingWithdrawals)
            return BadRequest(ApiResponse.Fail("You already have withdrawals waiting to be paid. Please wait for those first."));

        var balance = await ShopWallet.BalanceAsync(db, shop.Id);
        if (req.Amount > balance)
            return BadRequest(ApiResponse.Fail($"Your wallet balance is R{Math.Max(balance, 0):N2}, which is less than R{req.Amount:N2}."));

        var withdrawal = new ShopWalletTransaction
        {
            ShopId = shop.Id, Amount = req.Amount, Type = ShopWallet.Withdrawal, Status = "pending",
            Method = ShopWallet.DirectDeposit,
            BankName = bankName, AccountHolder = holder, AccountNumber = accountNumber,
            BranchCode = branchCode.Length == 0 ? null : branchCode,
        };
        withdrawal.Reference = "WD" + withdrawal.Id.ToString("N")[..8].ToUpperInvariant();
        db.ShopWalletTransactions.Add(withdrawal);
        await ShopWallet.SyncWithdrawalAsync(db, withdrawal);
        await db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            withdrawal.Id, withdrawal.Status, withdrawal.Amount, withdrawal.Reference,
            Balance = balance - req.Amount,
        }, "Withdrawal requested. We'll pay it into your bank account and let you know."));
    }

    /// <summary>Cancel a withdrawal that hasn't been paid yet; the money goes back into the wallet.</summary>
    [HttpDelete("withdrawals/{id:guid}")]
    public async Task<IActionResult> CancelWithdrawal(Guid id)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var withdrawal = await db.ShopWalletTransactions.FirstOrDefaultAsync(t =>
            t.Id == id && t.ShopId == shop.Id && t.Type == ShopWallet.Withdrawal);
        if (withdrawal is null) return NotFound(ApiResponse.Fail("Withdrawal not found."));
        if (withdrawal.Status != "pending")
            return BadRequest(ApiResponse.Fail("Only withdrawals that haven't been paid yet can be cancelled."));

        withdrawal.Status = "cancelled";
        withdrawal.Notes = "Cancelled by the shop.";
        await ShopWallet.SyncWithdrawalAsync(db, withdrawal);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Withdrawal cancelled. The money is back in your wallet."));
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
        await AdminAlerts.NotifyAdminsAsync(db, events, "wallet", "New wallet deposit to confirm",
            $"{shop.ShopName} will deposit R{transaction.Amount:F2}" + (transaction.Reference == null ? "." : $" with reference {transaction.Reference}.")
            + " Confirm it under Wallet Deposits once it reflects in the bank.", transaction.Id.ToString());

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
        // Shops often have no email; Paystack still needs one (see PaymentEmail).
        var email = PaymentEmail.For(shop.Email ?? shop.User.Email, shop.Phone, shop.UserId);

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

        var (all, total) = await LoadActivityAsync(shop.Id, await ShopWallet.BalanceAsync(db, shop.Id), take);
        var items = all.Skip(skip).Take(pageSize);

        return Ok(ApiResponse<object>.Ok(new
        {
            total,
            page,
            pageSize,
            items
        }));
    }
}

public record WalletTopUpRequest(decimal Amount, string? Method, string? Reference, string? Notes);
public record PaystackWalletTopUpRequest(decimal Amount);
public record WalletWithdrawalRequest(decimal Amount, string? BankName, string? AccountHolder, string? AccountNumber, string? BranchCode);
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
    string? Reference = null,
    decimal? BalanceAfter = null);
