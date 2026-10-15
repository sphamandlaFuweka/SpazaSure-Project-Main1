using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// Anyone can check a shop's sticker: scan its QR code or type its shop number. Only
/// public information is returned (shop name, area and whether it is verified).
/// </summary>
[ApiController]
[Route("api/public/shops")]
[AllowAnonymous]
public class PublicShopVerificationController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Verify(string code)
    {
        var shopCode = ShopVerification.Normalize(code);
        if (shopCode is null)
            return BadRequest(ApiResponse.Fail("Shop numbers look like SS482913. Check the number and try again."));

        var shop = await db.SpazaShops.AsNoTracking()
            .Include(s => s.Documents)
            .FirstOrDefaultAsync(s => s.ShopCode == shopCode);
        if (shop is null)
            return NotFound(ApiResponse.Fail($"There is no SpazaSure shop with number {shopCode}."));

        var result = ShopVerification.Evaluate(shop, shop.Documents, DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(ApiResponse<object>.Ok(new
        {
            shopNumber = shopCode,
            shopName = shop.ShopName,
            area = string.Join(", ", new[] { shop.City, shop.Province }.Where(x => !string.IsNullOrWhiteSpace(x))),
            verified = result.Verified,
            message = result.Summary,
            validUntil = result.ValidUntil,
            ratingAvg = shop.RatingCount > 0 ? shop.RatingAvg : (decimal?)null,
            checkedAt = DateTime.UtcNow,
        }));
    }
}

/// <summary>The signed-in shop's own number, QR link and what it still needs for the green sticker.</summary>
[ApiController]
[Route("api/shop/profile/verification")]
[Authorize(Roles = "spaza_owner")]
public class ShopVerificationController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var shop = await db.SpazaShops.Include(s => s.Documents).FirstOrDefaultAsync(s => s.UserId == UserId);
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));

        if (string.IsNullOrEmpty(shop.ShopCode))
        {
            await ShopVerification.EnsureCodeAsync(db, shop);
            await db.SaveChangesAsync();
        }

        var result = ShopVerification.Evaluate(shop, shop.Documents, DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(ApiResponse<object>.Ok(new
        {
            shopNumber = shop.ShopCode,
            shopName = shop.ShopName,
            verifyUrl = ShopVerification.VerifyUrl(shop.ShopCode!),
            verified = result.Verified,
            message = result.Verified
                ? "Your shop is SpazaSure Verified. Display your green sticker so customers can check it."
                : "Complete the items below to get your green SpazaSure Verified sticker.",
            outstanding = result.Outstanding,
            validUntil = result.ValidUntil,
        }));
    }
}
