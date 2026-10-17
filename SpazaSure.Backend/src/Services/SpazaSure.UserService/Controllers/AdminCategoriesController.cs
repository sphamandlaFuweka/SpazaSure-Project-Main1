using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

public record CategoryRequest(string? Name, short? SortOrder, bool? IsActive);

/// <summary>
/// Product categories that suppliers file products under and shops browse and filter by.
/// Part of the Marketplace admin area.
/// </summary>
[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = "admin")]
public partial class AdminCategoriesController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var items = await db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Name, c.Slug, c.SortOrder, c.IsActive,
                ProductCount = c.Products.Count(),
                LiveProductCount = c.Products.Count(p => p.IsAvailable && p.IsApproved)
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryRequest req)
    {
        var name = req.Name?.Trim() ?? "";
        if (name.Length is < 2 or > 60) return BadRequest(ApiResponse.Fail("Give the category a name (2 to 60 characters)."));
        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower()))
            return BadRequest(ApiResponse.Fail("A category with that name already exists."));

        var baseSlug = Slugify(name);
        var slug = baseSlug;
        for (var i = 2; await db.Categories.AnyAsync(c => c.Slug == slug); i++) slug = $"{baseSlug}-{i}";

        var sort = req.SortOrder ?? (short)((await db.Categories.MaxAsync(c => (short?)c.SortOrder) ?? 0) + 1);
        var category = new Category { Name = name, Slug = slug, SortOrder = sort, IsActive = req.IsActive ?? true };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { category.Id, category.Name, category.Slug, category.SortOrder, category.IsActive }, "Category added."));
    }

    /// <summary>Renames, reorders or hides a category. The slug stays the same so app icons keep working.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryRequest req)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound(ApiResponse.Fail("Category not found."));

        if (req.Name is not null)
        {
            var name = req.Name.Trim();
            if (name.Length is < 2 or > 60) return BadRequest(ApiResponse.Fail("Give the category a name (2 to 60 characters)."));
            if (await db.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
                return BadRequest(ApiResponse.Fail("A category with that name already exists."));
            category.Name = name;
        }
        if (req.SortOrder is { } sort) category.SortOrder = sort;
        if (req.IsActive is { } active) category.IsActive = active;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { category.Id, category.Name, category.Slug, category.SortOrder, category.IsActive }, "Category updated."));
    }

    /// <summary>Only empty categories can be deleted; hide one that still has products instead.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound(ApiResponse.Fail("Category not found."));
        var count = await db.Products.CountAsync(p => p.CategoryId == id);
        if (count > 0)
            return BadRequest(ApiResponse.Fail($"{count} product{(count == 1 ? " uses" : "s use")} this category. Hide it instead, or move the products first."));
        if (await db.Categories.AnyAsync(c => c.ParentId == id))
            return BadRequest(ApiResponse.Fail("This category has sub-categories. Remove those first."));
        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Category deleted."));
    }

    private static string Slugify(string name)
    {
        var slug = NonSlug().Replace(name.ToLowerInvariant().Replace("&", "and"), "-").Trim('-');
        return slug.Length == 0 ? "category" : slug[..Math.Min(slug.Length, 60)];
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}
