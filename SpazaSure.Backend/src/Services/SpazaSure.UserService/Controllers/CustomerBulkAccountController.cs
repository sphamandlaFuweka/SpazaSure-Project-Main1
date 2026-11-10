using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// A Bulk Buyer shopper's buyer account: the address their bulk orders are delivered to (or
/// collected near). Saving it the first time creates the account the marketplace and checkout use.
/// </summary>
[ApiController]
[Route("api/customer/bulk/account")]
[Authorize(Roles = "customer")]
public sealed class CustomerBulkAccountController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var rules = await PlatformSettingsStore.LoadAsync(db, ct);
        var account = await BulkBuying.BuyerAccountAsync(db, UserId, tracked: false, ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            isBulkActive = await BulkBuying.IsActiveAsync(db, UserId, ct),
            monthlyPrice = BulkBuying.MonthlyPrice,
            minOrderAmount = rules.BulkBuyerMinOrderAmount,
            address = account is null ? null : ToDto(account),
        }));
    }

    [HttpPut]
    public async Task<IActionResult> SaveAddress([FromBody] BulkBuyerAddressRequest req, CancellationToken ct)
    {
        if (!await BulkBuying.IsActiveAsync(db, UserId, ct))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(BulkBuying.RequiredMessage));
        if (string.IsNullOrWhiteSpace(req.Address) || req.Address.Trim().Length > 300)
            return BadRequest(ApiResponse.Fail("Enter the street address (up to 300 characters)."));
        if (string.IsNullOrWhiteSpace(req.Province) || req.Province.Trim().Length > 80)
            return BadRequest(ApiResponse.Fail("Choose the province."));
        if (req.Latitude is not (>= -35.5 and <= -21.5) || req.Longitude is not (>= 16 and <= 33.5))
            return BadRequest(ApiResponse.Fail("Pick the address from the suggestions or use your location, so we know where to deliver."));

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == UserId, ct);
        if (user is null) return NotFound(ApiResponse.Fail("Account not found."));
        var profile = await db.CustomerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == UserId, ct);
        var name = new[] { profile?.FirstName, profile?.LastName }.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() is { Count: > 0 } parts
            ? string.Join(" ", parts)
            : user.DisplayName ?? "Bulk buyer";

        // A spaza owner's login is never a customer, but never turn a real shop into a buyer account
        if (await db.SpazaShops.AnyAsync(s => s.UserId == UserId && s.AccountType != ShopAccountTypes.BulkBuyer, ct))
            return Conflict(ApiResponse.Fail("This account already has a spaza shop."));

        var account = await BulkBuying.BuyerAccountAsync(db, UserId, tracked: true, ct);
        if (account is null)
        {
            account = new SpazaShop { UserId = UserId, AccountType = ShopAccountTypes.BulkBuyer, Status = "active" };
            db.SpazaShops.Add(account);
        }
        account.ShopName = name;
        account.OwnerName = name;
        account.Phone = req.Phone?.Trim() is { Length: > 0 } phone ? phone : user.Phone ?? "";
        account.Email = user.Email;
        account.Address = req.Address.Trim();
        account.City = req.City?.Trim();
        account.Province = req.Province.Trim();
        account.PostalCode = req.PostalCode?.Trim();
        account.Latitude = req.Latitude;
        account.Longitude = req.Longitude;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(ToDto(account), "Delivery address saved."));
    }

    private static object ToDto(SpazaShop s) => new
    {
        s.Address, s.City, s.Province, s.PostalCode, s.Latitude, s.Longitude, s.Phone,
    };
}

public sealed record BulkBuyerAddressRequest(
    string Address, string? City, string Province, string? PostalCode,
    double? Latitude, double? Longitude, string? Phone);
