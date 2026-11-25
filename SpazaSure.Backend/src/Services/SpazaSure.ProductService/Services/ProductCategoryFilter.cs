using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.ProductService.Services;

/// <summary>Filters by category, including its direct subcategories, or "none" for uncategorised products.</summary>
public static class ProductCategoryFilter
{
    public static IQueryable<Product> Apply(IQueryable<Product> query, string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId) || categoryId == "all") return query;
        if (categoryId == "none") return query.Where(p => p.CategoryId == null);
        return Guid.TryParse(categoryId, out var id)
            ? query.Where(p => p.CategoryId == id || (p.Category != null && p.Category.ParentId == id))
            : query.Where(_ => false);
    }
}
