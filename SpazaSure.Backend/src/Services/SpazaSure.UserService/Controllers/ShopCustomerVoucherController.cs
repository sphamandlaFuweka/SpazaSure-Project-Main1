using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/shop/customer-vouchers")]
[Authorize(Roles = "spaza_owner")]
public class ShopCustomerVoucherController(SpazaSureDbContext db) : ControllerBase
{
    private Guid ShopUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] RedeemCustomerVoucherRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(ApiResponse.Fail("Voucher code is required."));
        if (!await db.SpazaShops.AnyAsync(s => s.UserId == ShopUserId && s.Status == "active"))
            return Forbid();

        var voucher = await db.CustomerVouchers.FirstOrDefaultAsync(v => v.Code == request.Code.Trim().ToUpperInvariant());
        if (voucher is null) return NotFound(ApiResponse.Fail("Voucher not found."));
        if (voucher.Status != "active") return BadRequest(ApiResponse.Fail("This voucher has already been used or is no longer active."));

        voucher.Status = "redeemed";
        voucher.RedeemedAt = DateTime.UtcNow;
        voucher.RedeemedByUserId = ShopUserId;
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { voucher.Code, voucher.Amount, status = voucher.Status }, "Voucher accepted. Apply the R20 discount to the customer's purchase."));
    }
}

public record RedeemCustomerVoucherRequest(string Code);
