using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using SpazaSure.UserService.Services;
using System.Text.Json;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController(SpazaSureDbContext db, AddressGeocodingSignal geocoding, EventPublisher events) : ControllerBase
{
    //  SUPPLIERS 

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.Suppliers
            .Include(s => s.Documents)
            .Include(s => s.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s =>
                s.CompanyName.ToLower().Contains(term) ||
                s.Email.ToLower().Contains(term) ||
                s.ContactPerson.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "verified")
                query = query.Where(s => s.Status == "verified");
            else if (status == "pending")
                query = query.Where(s => s.Status != "verified" && s.Status != "suspended");
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.CompanyName,
                s.ContactPerson,
                s.Email,
                s.Phone,
                s.Tier,
                IsVerified = s.Status == "verified",
                s.Status,
                IdentityCheckStatus = s.IdentityCheckStatus,
                JoinedAt = s.CreatedAt,
                Documents = s.Documents.Select(d => new
                {
                    d.Id,
                    d.DocType,
                    DocUrl = SpazaSure.Shared.Helpers.PrivateFiles.Link(d.DocUrl),
                    d.Status,
                    d.ExpiryDate,
                    d.RejectionNote
                })
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    [HttpPatch("suppliers/{id}/verify")]
    public async Task<IActionResult> VerifySupplier(Guid id)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        if (IdentityChecks.AdminBlockReason(supplier) is { } supplierBlock)
            return Conflict(ApiResponse.Fail(supplierBlock));
        supplier.Status = "verified";
        await db.SaveChangesAsync();
        events.PublishNotification(supplier.UserId.ToString(), "account", "Your account is verified",
            "SpazaSure verified your business. Your products are now visible to shops.",
            priority: "high", referenceId: supplier.Id.ToString(), routingKey: "account.verified");
        return Ok(ApiResponse.Ok("Supplier verified successfully."));
    }

    /// <summary>Signs a suspended account out by revoking its refresh tokens.</summary>
    private async Task RevokeSessionsAsync(Guid userId)
    {
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync();
        foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;
    }

    [HttpPatch("suppliers/{id}/suspend")]
    public async Task<IActionResult> SuspendSupplier(Guid id)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        supplier.Status = "suspended";
        await RevokeSessionsAsync(supplier.UserId);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Supplier suspended."));
    }

    //  SPAZA OWNERS 

    [HttpGet("spaza-owners")]
    public async Task<IActionResult> GetSpazaOwners(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.SpazaShops
            .Include(s => s.User)
            .Include(s => s.Documents)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s =>
                s.ShopName.ToLower().Contains(term) ||
                (s.ShopCode != null && s.ShopCode.ToLower().Contains(term)) ||
                s.OwnerName.ToLower().Contains(term) ||
                (s.Email != null && s.Email.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "verified")
                query = query.Where(s => s.Status == "verified");
            else if (status == "pending")
                query = query.Where(s => s.Status != "verified" && s.Status != "suspended");
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var missingLocations = items
            .Where(s =>
                (s.Latitude is null || s.Longitude is null) &&
                (!string.IsNullOrWhiteSpace(s.Address) ||
                 !string.IsNullOrWhiteSpace(s.City) ||
                 !string.IsNullOrWhiteSpace(s.Province) ||
                 !string.IsNullOrWhiteSpace(s.PostalCode)))
            .ToList();
        // Shops without map coordinates are looked up by the background geocoding worker
        if (missingLocations.Count > 0) geocoding.RequestRun();

        var result = items.Select(s => new
        {
            s.Id,
            ShopNumber = s.ShopCode,
            s.ShopName,
            s.OwnerName,
            Email = s.Email != null ? s.Email : (s.User != null ? s.User.Email ?? "" : ""),
            s.Phone,
            Address = s.Address ?? "",
            City = s.City ?? "",
            Province = s.Province ?? "",
            s.Latitude,
            s.Longitude,
            IsVerified = s.Status == "verified",
            s.Status,
            IdentityCheckStatus = s.IdentityCheckStatus,
            s.ComplianceStatus,
            RegistrationDetails = s.RegistrationDetails is null
                ? (JsonElement?)null
                : JsonSerializer.Deserialize<JsonElement>(s.RegistrationDetails),
            JoinedAt = s.CreatedAt,
            Documents = s.Documents.Select(d => new
            {
                d.Id,
                d.DocType,
                DocUrl = SpazaSure.Shared.Helpers.PrivateFiles.Link(d.DocUrl),
                d.Status,
                d.ExpiryDate,
                d.RejectionNote
            })
        });

        return Ok(ApiResponse<object>.Ok(new { items = result, total, page, pageSize }));
    }

    [HttpPatch("spaza-owners/{id}/verify")]
    public async Task<IActionResult> VerifySpazaOwner(Guid id)
    {
        var shop = await db.SpazaShops.FindAsync(id);
        if (shop is null) return NotFound(ApiResponse.Fail("Spaza shop not found."));
        if (IdentityChecks.AdminBlockReason(shop) is { } shopBlock)
            return Conflict(ApiResponse.Fail(shopBlock));
        shop.Status = "verified";
        await db.SaveChangesAsync();
        events.PublishNotification(shop.UserId.ToString(), "account", "Your shop is verified",
            $"SpazaSure verified {shop.ShopName}. Your shop now shows as verified to suppliers and customers.",
            referenceId: shop.Id.ToString(), routingKey: "account.verified");
        return Ok(ApiResponse.Ok("Spaza owner verified successfully."));
    }

    [HttpPatch("spaza-owners/{id}/suspend")]
    public async Task<IActionResult> SuspendSpazaOwner(Guid id)
    {
        var shop = await db.SpazaShops.FindAsync(id);
        if (shop is null) return NotFound(ApiResponse.Fail("Spaza shop not found."));
        shop.Status = "suspended";
        await RevokeSessionsAsync(shop.UserId);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Spaza owner suspended."));
    }

    [HttpGet("wallet-top-ups")]
    public async Task<IActionResult> GetWalletTopUps([FromQuery] string status = "pending")
    {
        var query = db.ShopWalletTransactions.Include(t => t.Shop).Where(t => t.Type == "top_up");
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        var items = await query.OrderByDescending(t => t.CreatedAt).Take(100).Select(t => new {
            t.Id, t.Amount, t.Method, t.Status, t.Reference, t.Notes,
            t.CreatedAt, t.ApprovedAt, ShopId = t.ShopId, ShopName = t.Shop.ShopName,
            OwnerName = t.Shop.OwnerName, ShopPhone = t.Shop.Phone
        }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpPatch("wallet-top-ups/{id:guid}/approve")]
    public async Task<IActionResult> ApproveWalletTopUp(Guid id)
    {
        var transaction = await db.ShopWalletTransactions.FirstOrDefaultAsync(t => t.Id == id && t.Type == "top_up");
        if (transaction is null) return NotFound(ApiResponse.Fail("Top-up request not found."));
        if (transaction.Status != "pending") return BadRequest(ApiResponse.Fail("Top-up request is no longer pending."));
        if (transaction.Method == "paystack")
            return BadRequest(ApiResponse.Fail("Paystack top-ups are confirmed automatically by Paystack."));
        transaction.Status = "approved";
        transaction.ApprovedAt = DateTime.UtcNow;
        await ShopWallet.SyncTopUpAsync(db, transaction);
        await db.SaveChangesAsync();
        await NotifyShopAsync(transaction.ShopId, "Wallet topped up",
            $"Your deposit of R{transaction.Amount:F2}{RefSuffix(transaction.Reference)} has been confirmed and added to your wallet.");
        return Ok(ApiResponse.Ok("Wallet top-up approved."));
    }

    [HttpPatch("wallet-top-ups/{id:guid}/reject")]
    public async Task<IActionResult> RejectWalletTopUp(Guid id, [FromBody] WalletTopUpRejectRequest req)
    {
        var transaction = await db.ShopWalletTransactions.FirstOrDefaultAsync(t => t.Id == id && t.Type == "top_up");
        if (transaction is null) return NotFound(ApiResponse.Fail("Top-up request not found."));
        if (transaction.Status != "pending") return BadRequest(ApiResponse.Fail("Top-up request is no longer pending."));
        if (transaction.Method == "paystack")
            return BadRequest(ApiResponse.Fail("Paystack top-ups are confirmed automatically by Paystack."));
        transaction.Status = "rejected";
        transaction.Notes = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        await db.SaveChangesAsync();
        await NotifyShopAsync(transaction.ShopId, "Top-up not confirmed",
            $"We couldn't confirm your deposit of R{transaction.Amount:F2}{RefSuffix(transaction.Reference)}."
            + (transaction.Notes == null ? " Please contact SpazaSure support." : $" Reason: {transaction.Notes}"));
        return Ok(ApiResponse.Ok("Wallet top-up rejected."));
    }

    /// <summary>Shop withdrawal requests: money the admin must pay into the shop's bank account.</summary>
    [HttpGet("wallet-withdrawals")]
    public async Task<IActionResult> GetWalletWithdrawals([FromQuery] string status = "pending")
    {
        var query = db.ShopWalletTransactions.Include(t => t.Shop).Where(t => t.Type == ShopWallet.Withdrawal);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        var items = await query.OrderByDescending(t => t.CreatedAt).Take(100).Select(t => new {
            t.Id, t.Amount, t.Status, t.Reference, t.Notes, t.CreatedAt, PaidAt = t.ApprovedAt,
            t.BankName, t.AccountHolder, t.AccountNumber, t.BranchCode,
            ShopId = t.ShopId, ShopName = t.Shop.ShopName, OwnerName = t.Shop.OwnerName, ShopPhone = t.Shop.Phone
        }).ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    /// <summary>Record that the withdrawal was paid into the shop's bank account. The wallet was already debited when it was requested.</summary>
    [HttpPatch("wallet-withdrawals/{id:guid}/paid")]
    public async Task<IActionResult> MarkWalletWithdrawalPaid(Guid id, [FromBody] WalletWithdrawalPaidRequest req)
    {
        var withdrawal = await db.ShopWalletTransactions.FirstOrDefaultAsync(t => t.Id == id && t.Type == ShopWallet.Withdrawal);
        if (withdrawal is null) return NotFound(ApiResponse.Fail("Withdrawal not found."));
        if (withdrawal.Status != "pending") return BadRequest(ApiResponse.Fail("Withdrawal is no longer pending."));
        var bankReference = req.PaymentReference?.Trim();
        if (string.IsNullOrEmpty(bankReference))
            return BadRequest(ApiResponse.Fail("Enter the bank payment reference so the payment can be traced."));

        withdrawal.Status = "paid";
        withdrawal.ApprovedAt = DateTime.UtcNow;
        withdrawal.Notes = $"Paid to bank account. Bank reference: {bankReference}";
        await ShopWallet.SyncWithdrawalAsync(db, withdrawal);
        await db.SaveChangesAsync();
        await NotifyShopAsync(withdrawal.ShopId, "Withdrawal paid",
            $"Your withdrawal of R{withdrawal.Amount:F2}{RefSuffix(withdrawal.Reference)} has been paid into your bank account.");
        return Ok(ApiResponse.Ok("Withdrawal marked as paid."));
    }

    /// <summary>Decline a withdrawal; the money goes back into the shop's wallet.</summary>
    [HttpPatch("wallet-withdrawals/{id:guid}/reject")]
    public async Task<IActionResult> RejectWalletWithdrawal(Guid id, [FromBody] WalletTopUpRejectRequest req)
    {
        var withdrawal = await db.ShopWalletTransactions.FirstOrDefaultAsync(t => t.Id == id && t.Type == ShopWallet.Withdrawal);
        if (withdrawal is null) return NotFound(ApiResponse.Fail("Withdrawal not found."));
        if (withdrawal.Status != "pending") return BadRequest(ApiResponse.Fail("Withdrawal is no longer pending."));

        withdrawal.Status = "rejected";
        withdrawal.Notes = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        await ShopWallet.SyncWithdrawalAsync(db, withdrawal);
        await db.SaveChangesAsync();
        await NotifyShopAsync(withdrawal.ShopId, "Withdrawal declined",
            $"We couldn't pay your withdrawal of R{withdrawal.Amount:F2}{RefSuffix(withdrawal.Reference)}; the money is back in your wallet."
            + (withdrawal.Notes == null ? "" : $" Reason: {withdrawal.Notes}"));
        return Ok(ApiResponse.Ok("Withdrawal declined and the money returned to the wallet."));
    }

    private static string RefSuffix(string? reference) => reference == null ? "" : $" ({reference})";

    private async Task NotifyShopAsync(Guid shopId, string title, string message)
    {
        var userId = await db.SpazaShops.Where(s => s.Id == shopId).Select(s => (Guid?)s.UserId).FirstOrDefaultAsync();
        if (userId is null) return;
        events.PublishNotification(supplierId: userId.Value.ToString(), type: "wallet", title: title,
            message: message, referenceId: shopId.ToString(), routingKey: "notification.general");
    }

    //  REPORTS / REGULATORY ESCALATIONS

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports(
        [FromQuery] string? status,
        [FromQuery] string? escalatedTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.Reports
            .Include(r => r.Product)
            .Include(r => r.Reporter)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(escalatedTo))
            query = query.Where(r => r.EscalatedTo != null && r.EscalatedTo.Contains(escalatedTo));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.ReportType,
                r.Barcode,
                ProductName = r.Product != null ? r.Product.Name : null,
                ReporterName = r.Reporter != null && r.Reporter.Phone != null ? r.Reporter.Phone : null,
                r.ShopName,
                r.IsAnonymous,
                r.Description,
                PhotoUrl = SpazaSure.Shared.Helpers.PrivateFiles.Link(r.PhotoUrl),
                r.BatchNumber,
                r.SerialCode,
                r.ShopAddress,
                r.PurchaseLocation,
                r.PurchasePrice,
                r.ScratchPanelIntact,
                r.SealTampered,
                r.Latitude,
                r.Longitude,
                r.ReporterIp,
                r.DeviceId,
                r.Status,
                r.EscalatedTo,
                r.EscalatedAt,
                r.ResolutionNote,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    public record EscalateReportRequest(string EscalatedTo, string Status, string? ResolutionNote);

    [HttpPatch("reports/{id:guid}/escalate")]
    public async Task<IActionResult> EscalateReport(Guid id, [FromBody] EscalateReportRequest req)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null)
            return NotFound(ApiResponse.Fail("Report not found."));

        if (string.IsNullOrWhiteSpace(req.EscalatedTo))
            return BadRequest(ApiResponse.Fail("Escalation destination is required."));

        report.Status = string.IsNullOrWhiteSpace(req.Status) ? "escalated" : req.Status;
        report.EscalatedTo = req.EscalatedTo;
        report.EscalatedAt = DateTime.UtcNow;
        report.ResolutionNote = req.ResolutionNote;

        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            report.Id,
            report.Status,
            report.EscalatedTo,
            report.EscalatedAt,
            report.ResolutionNote
        }, "Report escalated to the required authority."));
    }
}

public record WalletTopUpRejectRequest(string? Reason);
public record WalletWithdrawalPaidRequest(string? PaymentReference);
