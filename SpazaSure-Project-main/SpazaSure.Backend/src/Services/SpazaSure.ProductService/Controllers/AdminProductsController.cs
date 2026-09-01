using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.ProductService.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "admin")]
public class AdminProductsController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        var query = db.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search) || p.Supplier.CompanyName.Contains(search));

        if (status == "active") query = query.Where(p => p.IsAvailable && p.IsApproved);
        else if (status == "pending_approval") query = query.Where(p => !p.IsApproved);
        else if (status == "archived") query = query.Where(p => !p.IsAvailable && p.IsApproved);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Name, p.Sku, p.Price, p.StockQty, p.Unit,
                p.IsAvailable, p.IsApproved, p.Images, p.Description,
                p.MinOrderQty, p.IsFoodItem, p.CategoryId, p.Barcode,
                Category = p.Category == null ? null : p.Category.Name,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier.CompanyName,
                p.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, items }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));

        return Ok(ApiResponse<object>.Ok(new
        {
            product.Id, product.Name, product.Sku, product.Price,
            product.StockQty, product.Unit, product.IsAvailable, product.IsApproved,
            product.Images, product.Description, product.MinOrderQty,
            product.IsFoodItem, product.CategoryId, product.Barcode,
            Category = product.Category?.Name,
            SupplierId = product.SupplierId,
            SupplierName = product.Supplier.CompanyName,
            product.CreatedAt
        }));
    }

    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));

        product.IsApproved = true;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { product.Id, product.IsApproved }));
    }

    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProductRequest? req)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));

        product.IsApproved = false;
        product.IsAvailable = false;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { product.Id, product.IsApproved, reason = req?.Reason }));
    }

    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));

        product.IsAvailable = !product.IsAvailable;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { product.Id, product.IsAvailable }));
    }
}

public record RejectProductRequest(string? Reason);
