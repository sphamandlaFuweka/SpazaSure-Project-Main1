using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.ProductService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SpazaSure.ProductService.Controllers;

[ApiController]
[Route("api/supplier/products")]
[Authorize]
public class ProductsController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    private Guid SupplierId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] string? categoryId = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return Ok(ApiResponse<object>.Ok(new { total = 0, page, pageSize, items = Array.Empty<object>() }));

        var query = db.Products.Include(p => p.Category).Where(p => p.SupplierId == supplier.Id);
        var term = search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(term)) query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        query = ProductCategoryFilter.Apply(query, categoryId);
        if (status == "active") query = query.Where(p => p.IsAvailable && p.IsApproved);
        else if (status == "pending_approval") query = query.Where(p => !p.IsApproved);
        else if (status == "archived") query = query.Where(p => !p.IsAvailable && p.IsApproved);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new {
                p.Id, p.Name, p.Sku, p.Barcode, p.Price, p.StockQty, p.Unit,
                p.IsAvailable, p.IsApproved, p.Images, p.Description,
                p.MinOrderQty, p.IsFoodItem, p.CategoryId,
                Category = p.Category == null ? null : p.Category.Name,
                HasQrCode = p.QrCodes.Any(q => q.OrderItemId == null),
                // Stock per branch; empty when the product isn't tracked per branch
                BranchStock = p.BranchStock.Select(s => new { s.SupplierBranchId, BranchName = s.SupplierBranch.Name, s.StockQty }).ToList(),
                p.CreatedAt
            }).ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, items }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await ProductDetail(id, supplier.Id);
        return product is null
            ? NotFound(ApiResponse.Fail("Product not found."))
            : Ok(ApiResponse<object>.Ok(product));
    }

    // Same shape as a List item. Returning the Product entity itself fails to serialize: the tracked
    // supplier links back to its products (Product.Supplier.Products.Supplier...).
    private async Task<object?> ProductDetail(Guid id, Guid supplierId) =>
        await db.Products.AsNoTracking().Where(p => p.Id == id && p.SupplierId == supplierId)
            .Select(p => new {
                p.Id, p.Name, p.Sku, p.Barcode, p.Price, p.StockQty, p.Unit,
                p.IsAvailable, p.IsApproved, p.Images, p.Description,
                p.MinOrderQty, p.IsFoodItem, p.CategoryId,
                Category = p.Category == null ? null : p.Category.Name,
                HasQrCode = p.QrCodes.Any(q => q.OrderItemId == null),
                // Stock per branch; empty when the product isn't tracked per branch
                BranchStock = p.BranchStock.Select(s => new { s.SupplierBranchId, BranchName = s.SupplierBranch.Name, s.StockQty }).ToList(),
                p.CreatedAt
            }).FirstOrDefaultAsync();

    [HttpPost]
    public async Task<IActionResult> Create(ProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Images) || req.Images.Trim() is "[]" or "null")
            return BadRequest(ApiResponse.Fail("A product image is required."));

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        if (await ValidateRequestAsync(req, supplier.Id, null) is { } invalid) return BadRequest(ApiResponse.Fail(invalid));

        var product = new Product {
            SupplierId = supplier.Id, CategoryId = req.CategoryId, Name = req.Name.Trim(),
            Description = req.Description, Sku = req.Sku.Trim(), Barcode = req.Barcode,
            Price = req.Price, MinOrderQty = req.MinOrderQty, StockQty = req.StockQty,
            Unit = req.Unit, Images = req.Images ?? "[]", IsAvailable = req.IsAvailable,
            IsFoodItem = req.IsFoodItem,
            // Admin setting: verified suppliers' products can skip manual review
            IsApproved = supplier.Status == "verified"
                && (await PlatformSettingsStore.LoadAsync(db)).AutoApproveVerifiedSupplierProducts
        };
        if (req.Info is not null && ProductInfo.Apply(product, req.Info) is { } infoError)
            return BadRequest(ApiResponse.Fail(infoError));
        db.Products.Add(product);
        await db.SaveChangesAsync();

        if (string.IsNullOrWhiteSpace(product.Barcode))
            product.Barcode = BarcodeService.GenerateEan13(product.Id);
        var qrCode = new ProductQrCode { ProductId = product.Id, QrCode = QrCodeService.GenerateToken() };
        db.ProductQrCodes.Add(qrCode);
        await db.SaveChangesAsync();

        if (!product.IsApproved)
            await AdminAlerts.NotifyAdminsAsync(db, events, "product", "Product waiting for review",
                $"{supplier.CompanyName} added '{product.Name}' (R{product.Price:F2}). Review it so shops can order it.",
                product.Id.ToString());

        return Ok(ApiResponse<object>.Ok(new {
            product.Id, product.Name, product.Sku, product.Barcode, product.Price,
            product.StockQty, product.IsAvailable, product.IsApproved, HasQrCode = true
        }));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ProductRequest req)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await db.Products.Include(p => p.BranchStock)
            .FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplier.Id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));
        // The total can't drop below what sits at the branches; the head office holds the rest
        var atBranches = product.BranchStock.Sum(s => s.StockQty);
        if (req.StockQty < atBranches)
            return BadRequest(ApiResponse.Fail(
                $"{atBranches} units of this product are at your branches. Lower the branch stock first, or enter at least {atBranches}."));
        // A product can keep a category an admin later hid; only a change must pick an active one.
        if (await ValidateRequestAsync(req, supplier.Id, product) is { } invalid) return BadRequest(ApiResponse.Fail(invalid));

        product.CategoryId = req.CategoryId;
        product.Name = req.Name.Trim();
        product.Description = req.Description;
        product.Sku = req.Sku.Trim();
        if (req.Barcode is not null) product.Barcode = req.Barcode;
        product.Price = req.Price;
        product.MinOrderQty = req.MinOrderQty;
        product.StockQty = req.StockQty;
        product.Unit = req.Unit;
        product.Images = req.Images ?? product.Images;
        product.IsAvailable = req.IsAvailable;
        product.IsFoodItem = req.IsFoodItem;
        // Older clients don't send product information; leave it as it is.
        if (req.Info is not null && ProductInfo.Apply(product, req.Info) is { } infoError)
            return BadRequest(ApiResponse.Fail(infoError));
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok((await ProductDetail(product.Id, supplier.Id))!));
    }

    /// <summary>
    /// How the product's stock is spread over the head office and each branch. When the product
    /// isn't tracked per branch, every location can ship the full total.
    /// </summary>
    [HttpGet("{id:guid}/branch-stock")]
    public async Task<IActionResult> GetBranchStock(Guid id)
    {
        var supplier = await db.Suppliers.Include(s => s.Branches).FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await db.Products.AsNoTracking().Include(p => p.BranchStock)
            .FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplier.Id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));
        return Ok(ApiResponse<object>.Ok(BranchStockView(product, supplier)));
    }

    /// <summary>Sets the quantity at the head office and at each branch; the product's total becomes their sum.</summary>
    [HttpPut("{id:guid}/branch-stock")]
    public async Task<IActionResult> SetBranchStock(Guid id, [FromBody] BranchStockRequest req)
    {
        var supplier = await db.Suppliers.Include(s => s.Branches).FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await db.Products.Include(p => p.BranchStock)
            .FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplier.Id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));

        if (!req.TrackPerBranch)
        {
            // Back to one shared total that every location can ship from
            db.ProductBranchStock.RemoveRange(product.BranchStock);
            await db.SaveChangesAsync();
            product.BranchStock.Clear();
            return Ok(ApiResponse<object>.Ok(BranchStockView(product, supplier), "Stock is shared by all your locations again."));
        }

        var rows = req.Branches ?? [];
        if (req.HeadOfficeQty < 0 || rows.Any(r => r.StockQty < 0))
            return BadRequest(ApiResponse.Fail("Stock can't be negative."));
        if (rows.Select(r => r.SupplierBranchId).Distinct().Count() != rows.Count)
            return BadRequest(ApiResponse.Fail("Each branch can only be listed once."));
        if (rows.Any(r => supplier.Branches.All(b => b.Id != r.SupplierBranchId)))
            return BadRequest(ApiResponse.Fail("One of those branches isn't yours. Refresh and try again."));
        if (supplier.Branches.Count == 0)
            return BadRequest(ApiResponse.Fail("Add a branch on your profile first."));

        foreach (var row in product.BranchStock.Where(s => rows.All(r => r.SupplierBranchId != s.SupplierBranchId)).ToList())
        {
            db.ProductBranchStock.Remove(row);
            product.BranchStock.Remove(row);
        }
        foreach (var input in rows)
        {
            var row = product.BranchStock.FirstOrDefault(s => s.SupplierBranchId == input.SupplierBranchId);
            if (row is null)
            {
                row = new ProductBranchStock { ProductId = product.Id, SupplierBranchId = input.SupplierBranchId };
                db.ProductBranchStock.Add(row);
                product.BranchStock.Add(row);
            }
            row.StockQty = input.StockQty;
            row.UpdatedAt = DateTime.UtcNow;
        }
        product.StockQty = req.HeadOfficeQty + rows.Sum(r => r.StockQty);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(BranchStockView(product, supplier), "Stock per branch saved."));
    }

    private static object BranchStockView(Product product, Supplier supplier) => new
    {
        product.Id,
        TotalStock = product.StockQty,
        TrackPerBranch = BranchStock.IsTracked(product),
        HeadOfficeQty = BranchStock.HeadOfficeQty(product),
        Branches = supplier.Branches.OrderBy(b => b.Name).Select(b => new
        {
            SupplierBranchId = b.Id,
            b.Name,
            b.City,
            b.IsActive,
            StockQty = product.BranchStock.FirstOrDefault(s => s.SupplierBranchId == b.Id)?.StockQty ?? 0,
        }).ToList(),
    };

    /// <summary>The product information block, for editing in the supplier portal.</summary>
    [HttpGet("{id:guid}/info")]
    public async Task<IActionResult> GetInfo(Guid id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplier.Id);
        return product is null
            ? NotFound(ApiResponse.Fail("Product not found."))
            : Ok(ApiResponse<object>.Ok(ProductInfo.Project(product)));
    }

    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplier.Id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));
        product.IsAvailable = !product.IsAvailable;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { product.Id, product.IsAvailable }));
    }

    [HttpGet("{id:guid}/barcode")]
    public async Task<IActionResult> GetBarcode(Guid id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplier.Id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));
        if (string.IsNullOrWhiteSpace(product.Barcode)) {
            product.Barcode = BarcodeService.GenerateEan13(product.Id);
            await db.SaveChangesAsync();
        }
        return Ok(ApiResponse<object>.Ok(new {
            productId = product.Id, productName = product.Name, sku = product.Sku,
            barcode = product.Barcode, barcodeSvg = BarcodeService.RenderSvg(product.Barcode)
        }));
    }

    [HttpGet("{id:guid}/qr-code")]
    public async Task<IActionResult> GetQrCode(Guid id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await db.Products.Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplier.Id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));

        if (string.IsNullOrWhiteSpace(product.Barcode))
            product.Barcode = BarcodeService.GenerateEan13(product.Id);
        var qr = await GetOrCreateDefaultQrCode(product.Id);
        await db.SaveChangesAsync();
        var supplierVerified = product.Supplier.Status is "verified" or "active";

        return Ok(ApiResponse<object>.Ok(new {
            productId = product.Id, productName = product.Name, product.Sku,
            supplierName = product.Supplier.CompanyName,
            supplierStatus = product.Supplier.Status, supplierVerified,
            productApproved = product.IsApproved,
            isVerified = product.IsApproved && supplierVerified && !qr.IsRecalled &&
                (!qr.ExpiryDate.HasValue || qr.ExpiryDate.Value >= DateOnly.FromDateTime(DateTime.UtcNow)),
            qrToken = qr.QrCode, qrSvg = QrCodeService.RenderSvg(qr.QrCode),
            qr.BatchNumber, qr.ExpiryDate, qr.IsRecalled,
            barcode = product.Barcode, barcodeSvg = BarcodeService.RenderSvg(product.Barcode)
        }));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.UserId == SupplierId);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.SupplierId == supplier.Id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));
        if (await db.OrderItems.AnyAsync(oi => oi.ProductId == id)) {
            product.IsAvailable = false;
            await db.SaveChangesAsync();
            return Ok(ApiResponse.Ok("Product removed from listing."));
        }
        var qrCodes = await db.ProductQrCodes.Where(q => q.ProductId == id).ToListAsync();
        if (qrCodes.Count > 0) db.ProductQrCodes.RemoveRange(qrCodes);
        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Product deleted."));
    }

    /// <summary>Checks the request makes business sense before it reaches the database.</summary>
    private async Task<string?> ValidateRequestAsync(ProductRequest req, Guid supplierId, Product? existing)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Trim().Length > 200) return "Give the product a name (up to 200 characters).";
        if (string.IsNullOrWhiteSpace(req.Sku) || req.Sku.Trim().Length > 60) return "Give the product a SKU (up to 60 characters).";
        if (req.Price <= 0 || req.Price > 1_000_000) return "The price must be more than R0.";
        if (req.StockQty < 0) return "Stock can't be negative.";
        if (req.MinOrderQty < 1) return "The minimum order must be at least 1.";
        var sku = req.Sku.Trim();
        if (await db.Products.AnyAsync(p => p.SupplierId == supplierId && p.Sku == sku && (existing == null || p.Id != existing.Id)))
            return $"You already have a product with SKU {sku}.";
        if (req.CategoryId is { } categoryId && categoryId != existing?.CategoryId &&
            !await db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive))
            return "Choose one of the listed categories.";
        return null;
    }

    private async Task<ProductQrCode> GetOrCreateDefaultQrCode(Guid productId)
    {
        var existing = await db.ProductQrCodes.FirstOrDefaultAsync(q => q.ProductId == productId && q.OrderItemId == null);
        if (existing is not null) return existing;
        var created = new ProductQrCode { ProductId = productId, QrCode = QrCodeService.GenerateToken() };
        db.ProductQrCodes.Add(created);
        try {
            await db.SaveChangesAsync();
            return created;
        } catch (DbUpdateException) {
            db.Entry(created).State = EntityState.Detached;
            return await db.ProductQrCodes.FirstAsync(q => q.ProductId == productId && q.OrderItemId == null);
        }
    }
}

public record ProductRequest(
    [Required] string Name,
    [Required] string Sku,
    [Required] decimal Price,
    string? Description,
    string? Barcode,
    Guid? CategoryId,
    int MinOrderQty = 1,
    int StockQty = 0,
    string Unit = "unit",
    string? Images = null,
    bool IsAvailable = true,
    bool IsFoodItem = false,
    ProductInfoRequest? Info = null
);

public record BranchStockRequest(bool TrackPerBranch, int HeadOfficeQty, List<BranchStockRow>? Branches);
public record BranchStockRow(Guid SupplierBranchId, int StockQty);
