using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.OrderService.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "admin")]
public class AdminOrdersController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var query = db.Orders
            .Include(o => o.Shop)
            .Include(o => o.Supplier)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && status != "all")
            query = query.Where(o => o.Status == status);

        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.Shop.ShopName.ToLower().Contains(term) ||
                o.Supplier.CompanyName.ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.TotalAmount,
                o.Subtotal,
                OriginalSubtotal = o.Items.Sum(i => i.OriginalUnitPrice * i.Quantity),
                TotalDiscount = o.Items.Sum(i => (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity),
                o.DeliveryFee,
                o.PlatformCommission,
                o.DeliveryType,
                o.DeliveryAddress,
                o.DeliveryLatitude,
                o.DeliveryLongitude,
                o.DeliveryDistanceKm,
                o.PaymentMethod,
                o.PaymentStatus,
                o.GroupBuyId,
                IsGroupBuy = o.GroupBuyId.HasValue,
                o.RejectionReason,
                o.CreatedAt,
                o.UpdatedAt,
                ShopId = o.ShopId,
                ShopName = o.Shop.ShopName,
                ShopAddress = o.Shop.Address ?? "",
                SupplierId = o.SupplierId,
                SupplierName = o.Supplier.CompanyName,
                Items = o.Items.Select(i => new
                {
                    i.ProductId,
                    ProductName = i.Product.Name,
                    i.Quantity,
                    i.OriginalUnitPrice,
                    i.UnitPrice,
                    i.DiscountPct,
                    DiscountAmount = i.OriginalUnitPrice - i.UnitPrice,
                    OriginalLineTotal = i.OriginalUnitPrice * i.Quantity,
                    DiscountLineAmount = (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity,
                    i.LineTotal
                })
            })
            .ToListAsync();

        // Summary stats
        var allOrders = await db.Orders.ToListAsync();
        var summary = new
        {
            totalOrders = allOrders.Count,
            pendingCount = allOrders.Count(o => o.Status == "pending"),
            deliveredRevenue = allOrders.Where(o => o.Status == "delivered").Sum(o => o.TotalAmount),
            totalPlatformFees = allOrders.Sum(o => o.PlatformCommission)
        };

        return Ok(ApiResponse<object>.Ok(new { total, page, pageSize, items, summary }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var order = await db.Orders
            .Include(o => o.Shop)
            .Include(o => o.Supplier)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        return Ok(ApiResponse<object>.Ok(new
        {
            order.Id,
            order.OrderNumber,
            order.Status,
            order.TotalAmount,
            order.Subtotal,
            OriginalSubtotal = order.Items.Sum(i => i.OriginalUnitPrice * i.Quantity),
            TotalDiscount = order.Items.Sum(i => (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity),
            order.DeliveryFee,
            order.PlatformCommission,
            order.DeliveryType,
            order.DeliveryAddress,
            order.DeliveryLatitude,
            order.DeliveryLongitude,
            order.DeliveryDistanceKm,
            order.PaymentMethod,
            order.PaymentStatus,
            order.GroupBuyId,
            IsGroupBuy = order.GroupBuyId.HasValue,
            order.RejectionReason,
            order.CreatedAt,
            order.UpdatedAt,
            ShopId = order.ShopId,
            ShopName = order.Shop.ShopName,
            ShopPhone = order.Shop.Phone,
            ShopAddress = order.Shop.Address ?? "",
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier.CompanyName,
            SupplierPhone = order.Supplier.Phone,
            Items = order.Items.Select(i => new
            {
                i.ProductId,
                ProductName = i.Product.Name,
                i.Quantity,
                i.OriginalUnitPrice,
                i.UnitPrice,
                i.DiscountPct,
                DiscountAmount = i.OriginalUnitPrice - i.UnitPrice,
                OriginalLineTotal = i.OriginalUnitPrice * i.Quantity,
                DiscountLineAmount = (i.OriginalUnitPrice - i.UnitPrice) * i.Quantity,
                i.LineTotal
            })
        }));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound(ApiResponse.Fail("Order not found."));

        var validStatuses = new[] { "pending", "processing", "dispatched", "delivered", "cancelled" };
        if (!validStatuses.Contains(req.Status))
            return BadRequest(ApiResponse.Fail("Invalid status."));

        order.Status = req.Status;
        if (req.Status == "cancelled") order.RejectionReason = req.Reason;
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { order.Id, order.Status }));
    }
}
