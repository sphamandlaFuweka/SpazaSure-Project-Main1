using SpazaSure.ProductService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.ProductService.Controllers;

[ApiController]
[Route("api/shop/marketplace")]
[Authorize]
public class ShopMarketplaceController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Suppliers the shop can buy from (same province, within the admin-set distance), or null
    /// when the caller has no shop and so isn't limited.
    /// </summary>
    private async Task<HashSet<Guid>?> ReachableSuppliersAsync(SpazaSure.Infrastructure.Entities.SpazaShop? shop)
    {
        if (shop is null) return null;
        var rules = await PlatformSettingsStore.LoadAsync(db);
        return await SupplierReach.ReachableSupplierIdsAsync(db, shop, rules.SupplierReachKm);
    }

    private Task<SpazaSure.Infrastructure.Entities.SpazaShop?> CurrentShopAsync() =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId)
            : Task.FromResult<SpazaSure.Infrastructure.Entities.SpazaShop?>(null);

    [HttpGet("products")]
    public async Task<IActionResult> Products(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? supplierId = null, [FromQuery] string sort = "popular")
    {
        // Get the shop's location for proximity sorting
        var shop = await CurrentShopAsync();
        var shopCity = shop?.City?.ToLower() ?? "";
        var shopProvince = shop?.Province?.ToLower() ?? "";
        var reachable = await ReachableSuppliersAsync(shop);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Products
            .Include(p => p.Category).Include(p => p.Supplier)
            .Where(p => p.IsAvailable && p.IsApproved && p.StockQty > 0)
            .Where(p => p.Supplier.Status == "active" || p.Supplier.Status == "verified");
        if (reachable is not null)
            query = query.Where(p => reachable.Contains(p.SupplierId));

        // Case-insensitive, so "maize" finds "Maize Meal"
        var term = search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(p => p.Name.ToLower().Contains(term)
                || p.Supplier.CompanyName.ToLower().Contains(term)
                || (p.Barcode != null && p.Barcode == term) || p.Sku.ToLower() == term);
        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);
        if (supplierId.HasValue)
            query = query.Where(p => p.SupplierId == supplierId);

        // Sort: "nearby" prioritizes suppliers in same city/province.
        // Every order ends with Name then Id: without a unique tie-breaker the database may
        // return tied products in a different order on each request, so products jumped
        // between pages and seemed to disappear.
        var ordered = sort switch {
            "price_low" => query.OrderBy(p => p.Price),
            "price_high" => query.OrderByDescending(p => p.Price),
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            "name" => query.OrderBy(p => p.Name),
            "nearby" => query
                .OrderByDescending(p => p.Supplier.City != null && p.Supplier.City.ToLower() == shopCity)
                .ThenByDescending(p => p.Supplier.Province != null && p.Supplier.Province.ToLower() == shopProvince)
                .ThenByDescending(p => p.StockQty),
            _ => // "popular" — default: nearby suppliers first, then by stock
                query
                .OrderByDescending(p => p.Supplier.City != null && p.Supplier.City.ToLower() == shopCity)
                .ThenByDescending(p => p.Supplier.Province != null && p.Supplier.Province.ToLower() == shopProvince)
                .ThenByDescending(p => p.StockQty)
        };
        var stable = ordered.ThenBy(p => p.Name).ThenBy(p => p.Id);

        var total = await query.CountAsync();
        var items = await stable.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new {
                p.Id, p.Name, p.Description, p.Sku, p.Price, p.MinOrderQty,
                p.StockQty, p.Unit, p.Images, p.IsFoodItem,
                CategoryId = p.CategoryId, CategoryName = p.Category != null ? p.Category.Name : null,
                SupplierId = p.SupplierId, SupplierName = p.Supplier.CompanyName,
                SupplierTier = p.Supplier.Tier, SupplierLogo = p.Supplier.LogoUrl,
                SupplierCommissionRate = p.Supplier.CommissionRate,
                SupplierCity = p.Supplier.City, SupplierProvince = p.Supplier.Province,
                IsNearby = (p.Supplier.City != null && p.Supplier.City.ToLower() == shopCity) ||
                           (p.Supplier.Province != null && p.Supplier.Province.ToLower() == shopProvince),
            }).ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, items }));
    }

    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> ProductDetail(Guid id)
    {
        var p = await db.Products.Include(x => x.Category).Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsAvailable && x.IsApproved
                && (x.Supplier.Status == "active" || x.Supplier.Status == "verified"));
        if (p is null) return NotFound(ApiResponse.Fail("Product not found."));
        if (await ReachableSuppliersAsync(await CurrentShopAsync()) is { } reachable && !reachable.Contains(p.SupplierId))
            return NotFound(ApiResponse.Fail("This supplier doesn't deliver to your area."));
        return Ok(ApiResponse<object>.Ok(new {
            p.Id, p.Name, p.Description, p.Sku, p.Price, p.MinOrderQty,
            p.StockQty, p.Unit, p.Images, CategoryName = p.Category?.Name,
            SupplierId = p.SupplierId, SupplierName = p.Supplier.CompanyName,
            SupplierTier = p.Supplier.Tier,
            p.IsFoodItem, Info = ProductInfo.Project(p),
            SupplierCommissionRate = p.Supplier.CommissionRate,
            SupplierCity = p.Supplier.City, SupplierProvince = p.Supplier.Province,
        }));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories()
    {
        var reachable = await ReachableSuppliersAsync(await CurrentShopAsync());
        var limited = reachable is not null;
        var supplierIds = reachable ?? [];
        var items = await db.Set<SpazaSure.Infrastructure.Entities.Category>()
            .Where(c => c.IsActive).OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.Name, c.Slug, c.ParentId, c.IconUrl,
                ProductCount = c.Products.Count(p => p.IsAvailable && p.IsApproved && p.StockQty > 0
                    && (p.Supplier.Status == "active" || p.Supplier.Status == "verified")
                    && (!limited || supplierIds.Contains(p.SupplierId))) })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers()
    {
        // Get shop location for nearby indicator
        var shop = await CurrentShopAsync();
        var shopCity = shop?.City?.ToLower() ?? "";
        var shopProvince = shop?.Province?.ToLower() ?? "";
        var reachable = await ReachableSuppliersAsync(shop);

        var suppliers = db.Suppliers.Where(s => s.Status == "active" || s.Status == "verified");
        if (reachable is not null) suppliers = suppliers.Where(s => reachable.Contains(s.Id));
        var items = await suppliers
            .OrderByDescending(s => s.City != null && s.City.ToLower() == shopCity)
            .ThenByDescending(s => s.Province != null && s.Province.ToLower() == shopProvince)
            .ThenBy(s => s.CompanyName)
            .Select(s => new {
                s.Id, s.CompanyName, s.Tier, s.LogoUrl, s.City, s.Province,
                ProductCount = s.Products.Count(p => p.IsAvailable && p.IsApproved && p.StockQty > 0),
                IsNearby = (s.City != null && s.City.ToLower() == shopCity) ||
                           (s.Province != null && s.Province.ToLower() == shopProvince),
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    /// <summary>Lookup a product by a SpazaSure QR token, EAN barcode, or SKU.</summary>
    [HttpGet("scan/{code}")]
    public async Task<IActionResult> ScanProduct(string code)
    {
        code = code.Trim();
        var qr = await db.ProductQrCodes.AsNoTracking().FirstOrDefaultAsync(q => q.QrCode == code);
        var firstScan = false;
        DateTime? scannedAt = null;

        if (qr is not null)
        {
            var scanTime = DateTime.UtcNow;
            firstScan = await db.ProductQrCodes
                .Where(q => q.Id == qr.Id && !q.IsScanned)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(q => q.IsScanned, true)
                    .SetProperty(q => q.ScannedAt, scanTime)
                    .SetProperty(q => q.UpdatedAt, scanTime)) == 1;
            scannedAt = firstScan
                ? scanTime
                : await db.ProductQrCodes.Where(q => q.Id == qr.Id).Select(q => q.ScannedAt).SingleAsync();
        }

        var productId = qr?.ProductId;
        var p = await db.Products.Include(x => x.Category).Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => productId.HasValue
                ? x.Id == productId.Value
                : (x.Barcode != null && x.Barcode == code) || x.Sku == code);

        if (p is null)
            return NotFound(ApiResponse.Fail("Product not found. This QR code, barcode, or SKU is not registered."));

        var supplierVerified = p.Supplier.Status is "verified" or "active";
        var expired = qr?.ExpiryDate is DateOnly expiry && expiry < DateOnly.FromDateTime(DateTime.UtcNow);
        var recalled = qr?.IsRecalled == true;
        var isVerified = p.IsApproved && supplierVerified && !expired && !recalled;

        return Ok(ApiResponse<object>.Ok(new {
            ScanType = qr is null ? (p.Barcode == code ? "barcode" : "sku") : "qr",
            QrToken = qr?.QrCode,
            p.Id, p.Name, p.Description, p.Sku, p.Barcode, p.Price, p.MinOrderQty,
            p.StockQty, p.Unit, p.Images, p.IsAvailable, p.IsApproved,
            CategoryName = p.Category?.Name,
            SupplierId = p.SupplierId, SupplierName = p.Supplier.CompanyName,
            SupplierTier = p.Supplier.Tier, SupplierCity = p.Supplier.City,
            SupplierProvince = p.Supplier.Province, SupplierStatus = p.Supplier.Status,
            SupplierVerified = supplierVerified, ProductApproved = p.IsApproved, IsVerified = isVerified,
            BatchNumber = qr?.BatchNumber, ExpiryDate = qr?.ExpiryDate,
            IsExpired = expired, IsRecalled = recalled,
            FirstScan = qr is not null && firstScan,
            RepeatScan = qr is not null && !firstScan,
            ScannedAt = scannedAt,
            // Shops check deliveries against the supplier's own description of the genuine product.
            Info = ProductInfo.Project(p)
        }));
    }
}
