using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// Settings the apps need before checkout (so displayed totals match what is charged)
/// and that the gateway uses for maintenance mode. Contains nothing sensitive.
/// </summary>
[ApiController]
[Route("api/settings/public")]
[AllowAnonymous]
public class PublicSettingsController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var rules = await PlatformSettingsStore.LoadAsync(db);
        return Ok(ApiResponse<object>.Ok(new
        {
            rules.StandardDeliveryFee,
            rules.ExpressDeliveryFee,
            pickupDeliveryFee = 0m,
            rules.ShopServiceFeePercent,
            rules.CashOnDeliveryEnabled,
            rules.CashOnDeliveryMaxOrderTotal,
            rules.MaintenanceMode,
            rules.MaintenanceMessage,
        }));
    }
}
