using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController(SpazaSureDbContext db) : ControllerBase
{
    //  SUPPLIERS 

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.Suppliers
            .Include(s => s.Documents)
            .Include(s => s.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s =>
                s.CompanyName.ToLower().Contains(term) ||
                s.Email.ToLower().Contains(term) ||
                s.ContactPerson.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "verified")
                query = query.Where(s => s.Status == "verified");
            else if (status == "pending")
                query = query.Where(s => s.Status != "verified");
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.CompanyName,
                s.ContactPerson,
                s.Email,
                s.Phone,
                s.Tier,
                IsVerified = s.Status == "verified",
                s.Status,
                JoinedAt = s.CreatedAt,
                Documents = s.Documents.Select(d => new
                {
                    d.Id,
                    d.DocType,
                    d.DocUrl,
                    d.Status,
                    d.ExpiryDate,
                    d.RejectionNote
                })
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    [HttpPatch("suppliers/{id}/verify")]
    public async Task<IActionResult> VerifySupplier(Guid id)
    {
        var supplier = await db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound(ApiResponse.Fail("Supplier not found."));
        supplier.Status = "verified";
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Supplier verified successfully."));
    }

    //  SPAZA OWNERS 

    [HttpGet("spaza-owners")]
    public async Task<IActionResult> GetSpazaOwners(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.SpazaShops.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s =>
                s.ShopName.ToLower().Contains(term) ||
                s.OwnerName.ToLower().Contains(term) ||
                (s.Email != null && s.Email.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "verified")
                query = query.Where(s => s.Status == "verified");
            else if (status == "pending")
                query = query.Where(s => s.Status != "verified");
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.ShopName,
                s.OwnerName,
                Email = s.Email != null ? s.Email : (s.User != null ? s.User.Email ?? "" : ""),
                s.Phone,
                Address = s.Address ?? "",
                City = s.City ?? "",
                Province = s.Province ?? "",
                IsVerified = s.Status == "verified",
                s.Status,
                s.ComplianceStatus,
                JoinedAt = s.CreatedAt,
                Documents = s.Documents.Select(d => new
                {
                    d.Id,
                    d.DocType,
                    d.DocUrl,
                    d.Status,
                    d.ExpiryDate,
                    d.RejectionNote
                })
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    [HttpPatch("spaza-owners/{id}/verify")]
    public async Task<IActionResult> VerifySpazaOwner(Guid id)
    {
        var shop = await db.SpazaShops.FindAsync(id);
        if (shop is null) return NotFound(ApiResponse.Fail("Spaza shop not found."));
        shop.Status = "verified";
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Spaza owner verified successfully."));
    }
}
