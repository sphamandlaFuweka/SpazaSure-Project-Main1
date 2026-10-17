using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.ProductService.Services;

/// <summary>Filters a product list by category: a category id, or "none" for uncategorised products.</summary>
public static class ProductCategoryFilter
{
    public static IQueryable<Product> Apply(IQueryable<Product> query, string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId) || categoryId == "all") return query;
        if (categoryId == "none") return query.Where(p => p.CategoryId == null);
        return Guid.TryParse(categoryId, out var id) ? query.Where(p => p.CategoryId == id) : query.Where(_ => false);
    }
}
