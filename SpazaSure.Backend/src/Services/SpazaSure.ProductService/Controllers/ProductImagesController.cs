using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;

namespace SpazaSure.ProductService.Controllers;

/// <summary>
/// Serves the product photos and supplier logos that lists link to (see ProductImages). Public, like
/// the photos themselves, because image tags can't send a login. Links carry a version, so the
/// browser and app cache each photo for good.
/// </summary>
[ApiController]
[Route("api/shop/marketplace/images")]
[AllowAnonymous]
public class ProductImagesController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet("{productId:guid}/{index:int}")]
    public async Task<IActionResult> Product(Guid productId, int index)
    {
        var images = await db.Products.AsNoTracking().Where(p => p.Id == productId)
            .Select(p => p.Images).FirstOrDefaultAsync();
        var list = ProductImages.Parse(images);
        return index >= 0 && index < list.Count ? Serve(list[index]) : NotFound();
    }

    [HttpGet("logos/{supplierId:guid}")]
    public async Task<IActionResult> Logo(Guid supplierId) =>
        Serve(await db.Suppliers.AsNoTracking().Where(s => s.Id == supplierId)
            .Select(s => s.LogoUrl).FirstOrDefaultAsync());

    private IActionResult Serve(string? entry)
    {
        if (ProductImages.Decode(entry) is { } image)
        {
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(image.Bytes, image.ContentType);
        }
        // A photo stored as a web address: send the viewer there (never back to these links)
        if (Uri.TryCreate(entry, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            && !uri.AbsolutePath.StartsWith(ProductImages.Route, StringComparison.OrdinalIgnoreCase))
            return Redirect(uri.ToString());
        return NotFound();
    }
}
