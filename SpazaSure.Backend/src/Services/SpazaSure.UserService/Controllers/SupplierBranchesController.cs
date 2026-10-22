using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;
using System.Security.Claims;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// The supplier's warehouses and depots, e.g. Durban, Johannesburg and Cape Town. Shops near any
/// active branch can buy from the supplier, and group buys run from one branch to the shops around it.
/// </summary>
[ApiController]
[Route("api/supplier/profile/branches")]
[Authorize(Roles = "supplier")]
public class SupplierBranchesController(SpazaSureDbContext db) : ControllerBase
{
    internal const int MaxBranches = 50;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Task<Guid> SupplierIdAsync() =>
        db.Suppliers.Where(s => s.UserId == UserId).Select(s => s.Id).FirstOrDefaultAsync();

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var supplierId = await SupplierIdAsync();
        if (supplierId == Guid.Empty) return NotFound(ApiResponse.Fail("Supplier not found."));

        var branches = await db.SupplierBranches.AsNoTracking()
            .Where(b => b.SupplierId == supplierId)
            .OrderByDescending(b => b.IsActive).ThenBy(b => b.Name)
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(branches.Select(Project).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SupplierBranchRequest req)
    {
        var supplierId = await SupplierIdAsync();
        if (supplierId == Guid.Empty) return NotFound(ApiResponse.Fail("Supplier not found."));
        if (await db.SupplierBranches.CountAsync(b => b.SupplierId == supplierId) >= MaxBranches)
            return BadRequest(ApiResponse.Fail($"You can add up to {MaxBranches} branches."));

        var error = Validate(req);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));
        if (await NameTakenAsync(db, supplierId, req.Name!, null))
            return Conflict(ApiResponse.Fail("You already have a branch with this name."));

        var branch = new SupplierBranch { SupplierId = supplierId };
        Apply(branch, req);
        db.SupplierBranches.Add(branch);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(Project(branch), "Branch added."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SupplierBranchRequest req)
    {
        var supplierId = await SupplierIdAsync();
        var branch = await db.SupplierBranches.FirstOrDefaultAsync(b => b.Id == id && b.SupplierId == supplierId);
        if (branch is null) return NotFound(ApiResponse.Fail("Branch not found."));

        var error = Validate(req);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));
        if (await NameTakenAsync(db, supplierId, req.Name!, id))
            return Conflict(ApiResponse.Fail("You already have a branch with this name."));

        Apply(branch, req);
        branch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(Project(branch), "Branch updated."));
    }

    /// <summary>Running group buys keep the area they were created with, so removing a branch doesn't move them.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var supplierId = await SupplierIdAsync();
        var branch = await db.SupplierBranches.FirstOrDefaultAsync(b => b.Id == id && b.SupplierId == supplierId);
        if (branch is null) return NotFound(ApiResponse.Fail("Branch not found."));

        db.SupplierBranches.Remove(branch);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Branch removed."));
    }

    internal static Task<bool> NameTakenAsync(SpazaSureDbContext db, Guid supplierId, string name, Guid? exceptId)
    {
        var lowered = name.Trim().ToLower();
        return db.SupplierBranches.AnyAsync(b =>
            b.SupplierId == supplierId && b.Id != exceptId && b.Name.ToLower() == lowered);
    }

    internal static string? Validate(SupplierBranchRequest req)
    {
        if ((req.Name?.Trim().Length ?? 0) is < 2 or > 80) return "Give the branch a name (2 to 80 characters), e.g. Durban warehouse.";
        if (string.IsNullOrWhiteSpace(req.Address) || req.Address.Trim().Length > 300) return "Enter the branch address.";
        if (string.IsNullOrWhiteSpace(req.City) || req.City.Trim().Length > 100) return "Enter the city the branch is in.";
        if (string.IsNullOrWhiteSpace(req.Province) || req.Province.Trim().Length > 100) return "Enter the province the branch is in.";
        if (req.PostalCode?.Trim().Length > 10) return "The postal code is too long.";
        // Shops are matched to branches by distance, so every branch needs its spot on the map
        if (req.Latitude is null || req.Longitude is null) return "Pick the branch address from the suggestions or pin it on the map.";
        return AddressValidation.ValidateCoordinates(req.Latitude, req.Longitude);
    }

    internal static void Apply(SupplierBranch branch, SupplierBranchRequest req)
    {
        branch.Name = req.Name!.Trim();
        branch.Address = req.Address!.Trim();
        branch.City = req.City!.Trim();
        branch.Province = req.Province!.Trim();
        branch.PostalCode = string.IsNullOrWhiteSpace(req.PostalCode) ? null : req.PostalCode.Trim();
        branch.Latitude = req.Latitude!.Value;
        branch.Longitude = req.Longitude!.Value;
        branch.IsActive = req.IsActive ?? branch.IsActive;
    }

    internal static object Project(SupplierBranch b) => new
    {
        b.Id, b.Name, b.Address, b.City, b.Province, b.PostalCode, b.Latitude, b.Longitude, b.IsActive, b.CreatedAt,
    };
}

public record SupplierBranchRequest(
    string? Name,
    string? Address,
    string? City,
    string? Province,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    bool? IsActive = null);
