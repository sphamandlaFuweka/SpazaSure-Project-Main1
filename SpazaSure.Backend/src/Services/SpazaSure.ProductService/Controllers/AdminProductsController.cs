using SpazaSure.ProductService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.ProductService.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "admin")]
public class AdminProductsController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? categoryId = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        var query = db.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsQueryable();

        var term = search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term) || p.Supplier.CompanyName.ToLower().Contains(term));
        query = ProductCategoryFilter.Apply(query, categoryId);

        if (status == "active") query = query.Where(p => p.IsAvailable && p.IsApproved);
        else if (status == "pending_approval") query = query.Where(p => !p.IsApproved);
        else if (status == "archived") query = query.Where(p => !p.IsAvailable && p.IsApproved);

        var total = await query.CountAsync();
        var apiBase = PublicApi.Base(Request);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Name, p.Sku, p.Price, p.StockQty, p.Unit,
                p.IsAvailable, p.IsApproved, Images = ProductImages.ForList(apiBase, p.Id, p.Images.Length <= ProductImages.InlineLimit ? p.Images : null, p.Images.Length, p.Images.Length - p.Images.Replace("\"", "").Length), p.Description,
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
            Images = ProductImages.WithLinks(PublicApi.Base(Request), product.Id, product.Images), product.Description, product.MinOrderQty,
            product.IsFoodItem, product.CategoryId, product.Barcode,
            Category = product.Category?.Name,
            SupplierId = product.SupplierId,
            SupplierName = product.Supplier.CompanyName,
            product.CreatedAt,
            Info = ProductInfo.Project(product)
        }));
    }

    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var product = await db.Products.Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));

        product.IsApproved = true;
        await db.SaveChangesAsync();
        events.PublishNotification(product.Supplier.UserId.ToString(), "product", "Product approved",
            $"'{product.Name}' is approved and visible to shops.", referenceId: product.Id.ToString(), routingKey: "product.approved");
        return Ok(ApiResponse<object>.Ok(new { product.Id, product.IsApproved }));
    }

    [HttpPatch("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProductRequest? req)
    {
        var product = await db.Products.Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound(ApiResponse.Fail("Product not found."));

        product.IsApproved = false;
        product.IsAvailable = false;
        await db.SaveChangesAsync();
        events.PublishNotification(product.Supplier.UserId.ToString(), "product", "Product not approved",
            $"'{product.Name}' was not approved" + (string.IsNullOrWhiteSpace(req?.Reason) ? "." : $": {req!.Reason!.Trim()}"),
            priority: "high", referenceId: product.Id.ToString(), routingKey: "product.rejected");
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
