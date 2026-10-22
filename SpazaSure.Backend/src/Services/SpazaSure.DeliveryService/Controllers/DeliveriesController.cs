using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.DeliveryService.Controllers;

/// <summary>Admins allocate drivers to orders that still have to be delivered.</summary>
[ApiController]
[Route("api/admin/deliveries")]
[Authorize(Roles = "admin")]
public class DeliveriesController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    /// <summary>
    /// Orders waiting for or on their way to a shop. "unassigned" lists the ones without a driver,
    /// "assigned" the ones with one; anything else lists both.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string filter = "open")
    {
        var query = db.Orders.AsNoTracking()
            .Include(o => o.Shop).Include(o => o.Supplier).Include(o => o.SupplierBranch).Include(o => o.Driver)
            .Where(o => DriversController.OpenStatuses.Contains(o.Status) && o.Status != "pending" && o.DeliveryType != "pickup");
        query = filter switch
        {
            "unassigned" => query.Where(o => o.DriverId == null),
            "assigned" => query.Where(o => o.DriverId != null),
            _ => query,
        };
        var orders = await query.OrderBy(o => o.DriverId != null).ThenBy(o => o.CreatedAt).Take(300).ToListAsync();
        return Ok(ApiResponse<object>.Ok(orders.Select(Project).ToList()));
    }

    /// <summary>Allocates a driver to the order, or takes the driver off it when DriverId is null.</summary>
    [HttpPut("{orderId:guid}/driver")]
    public async Task<IActionResult> Assign(Guid orderId, [FromBody] AssignDriverRequest req)
    {
        var order = await db.Orders.Include(o => o.Shop).Include(o => o.Supplier).Include(o => o.SupplierBranch)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));
        if (!DriversController.OpenStatuses.Contains(order.Status))
            return BadRequest(ApiResponse.Fail("Drivers can only be allocated to orders that haven't been delivered or cancelled."));

        Driver? driver = null;
        if (req.DriverId is { } driverId)
        {
            driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId);
            if (driver is null) return NotFound(ApiResponse.Fail("Driver not found."));
            if (!driver.IsActive) return BadRequest(ApiResponse.Fail($"{driver.FullName} is switched off. Switch them on first."));
        }

        var changed = order.DriverId != driver?.Id;
        order.DriverId = driver?.Id;
        order.Driver = driver;
        order.DriverAssignedAt = driver is null ? null : DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (changed && driver is not null)
            events.PublishNotification(
                supplierId: order.Shop.UserId.ToString(),
                type: "delivery",
                title: "Driver allocated",
                message: $"{driver.FullName} ({driver.VehicleType}, {driver.VehicleRegistration}) will deliver order {order.OrderNumber}. " +
                         $"Their badge code is {driver.BadgeCode}. Follow them on the map once the order is on its way.",
                priority: "normal",
                referenceId: order.Id.ToString(),
                routingKey: "order.driver_assigned");

        return Ok(ApiResponse<object>.Ok(Project(order),
            driver is null ? "Driver removed from the order." : $"{driver.FullName} is delivering this order."));
    }

    private static object Project(Order o) => new
    {
        o.Id, o.OrderNumber, o.Status, o.PaymentStatus, o.DeliveryType, o.CreatedAt,
        ShopName = o.Shop.ShopName, ShopPhone = o.Shop.Phone, o.DeliveryAddress, o.DeliveryLatitude, o.DeliveryLongitude,
        SupplierName = o.Supplier.CompanyName,
        Pickup = Tracking.Pickup(o),
        o.DeliveryDistanceKm,
        Driver = o.Driver is null ? null : new { o.Driver.Id, o.Driver.FullName, o.Driver.Phone, o.Driver.VehicleRegistration, o.Driver.LastSeenAt },
        o.DriverAssignedAt,
    };
}

public record AssignDriverRequest(Guid? DriverId);

/// <summary>Where an order is: for the shop that ordered it, its supplier and admins.</summary>
[ApiController]
[Route("api/delivery/orders/{orderId:guid}/tracking")]
[Authorize]
public class TrackingController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid orderId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Shop).Include(o => o.Supplier).Include(o => o.SupplierBranch).Include(o => o.Driver)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));
        var allowed = User.IsInRole("admin") || order.Shop.UserId == userId || order.Supplier.UserId == userId;
        if (!allowed) return NotFound(ApiResponse.Fail("Order not found."));
        return Ok(ApiResponse<object>.Ok(Tracking.Project(order)));
    }
}

internal static class Tracking
{
    /// <summary>A driver position older than this isn't shown; the driver probably stopped sharing.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(30);

    public static object Pickup(Order o) => o.SupplierBranch is { } b
        ? new { Name = $"{o.Supplier.CompanyName} - {b.Name}", b.Address, Latitude = (double?)b.Latitude, Longitude = (double?)b.Longitude }
        : new { Name = o.Supplier.CompanyName, Address = o.Supplier.Address ?? "", o.Supplier.Latitude, o.Supplier.Longitude };

    public static object Project(Order o)
    {
        var d = o.Driver;
        // Only while the goods are on the road, and only a recent position
        var live = d is not null && o.Status == "dispatched" &&
                   d.LastLatitude is not null && d.LastSeenAt is { } seen && DateTime.UtcNow - seen < FreshFor;
        return new
        {
            OrderId = o.Id, o.OrderNumber, o.Status,
            Pickup = Pickup(o),
            Destination = new { o.DeliveryAddress, Latitude = o.DeliveryLatitude ?? o.Shop.Latitude, Longitude = o.DeliveryLongitude ?? o.Shop.Longitude },
            Driver = d is null ? null : new { d.FullName, d.Phone, d.VehicleType, d.VehicleRegistration, d.BadgeCode },
            DriverLocation = live ? new { Latitude = d!.LastLatitude, Longitude = d.LastLongitude, UpdatedAt = d.LastSeenAt } : null,
        };
    }
}
