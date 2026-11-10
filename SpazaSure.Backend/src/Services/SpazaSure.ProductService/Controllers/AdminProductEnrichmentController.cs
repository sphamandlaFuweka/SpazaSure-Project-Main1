using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.ProductService.Services;
using SpazaSure.Shared.Models;

namespace SpazaSure.ProductService.Controllers;

/// <summary>
/// Fills every product's empty details, ingredients, nutrition and "spot a fake" checks from the
/// open product databases, by barcode. Never overwrites what a supplier entered.
/// </summary>
[ApiController]
[Route("api/admin/products/enrich")]
[Authorize(Roles = "admin")]
public class AdminProductEnrichmentController(ProductEnrichmentJob job) : ControllerBase
{
    /// <summary>Starts a run over all products (or one supplier's). dryRun=true only reports.</summary>
    [HttpPost]
    public IActionResult Start([FromQuery] bool dryRun = true, [FromQuery] Guid? supplierId = null)
    {
        if (!job.TryStart(dryRun, supplierId))
            return Conflict(ApiResponse.Fail("A product information run is already in progress."));
        return Accepted(ApiResponse<object>.Ok(job.Progress,
            dryRun ? "Checking every product. Nothing will be saved." : "Filling in product information."));
    }

    /// <summary>Progress and per-product results of the current or last run.</summary>
    [HttpGet]
    public IActionResult Status() => Ok(ApiResponse<object>.Ok(job.Progress));
}
