using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// Admins manage a supplier's branches for them, e.g. while onboarding a supplier who isn't
/// comfortable with the portal. Same rules as the supplier's own Branches tab.
/// </summary>
[ApiController]
[Route("api/admin/suppliers/{supplierId:guid}/branches")]
[Authorize(Roles = "admin")]
public class AdminSupplierBranchesController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid supplierId)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == supplierId)) return NotFound(ApiResponse.Fail("Supplier not found."));
        var branches = await db.SupplierBranches.AsNoTracking()
            .Where(b => b.SupplierId == supplierId)
            .OrderByDescending(b => b.IsActive).ThenBy(b => b.Name)
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(branches.Select(SupplierBranchesController.Project).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid supplierId, [FromBody] SupplierBranchRequest req)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == supplierId)) return NotFound(ApiResponse.Fail("Supplier not found."));
        if (await db.SupplierBranches.CountAsync(b => b.SupplierId == supplierId) >= SupplierBranchesController.MaxBranches)
            return BadRequest(ApiResponse.Fail($"A supplier can have up to {SupplierBranchesController.MaxBranches} branches."));
        if (SupplierBranchesController.Validate(req) is { } error) return BadRequest(ApiResponse.Fail(error));
        if (await SupplierBranchesController.NameTakenAsync(db, supplierId, req.Name!, null))
            return Conflict(ApiResponse.Fail("This supplier already has a branch with this name."));

        var branch = new SupplierBranch { SupplierId = supplierId };
        SupplierBranchesController.Apply(branch, req);
        db.SupplierBranches.Add(branch);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(SupplierBranchesController.Project(branch), "Branch added."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid supplierId, Guid id, [FromBody] SupplierBranchRequest req)
    {
        var branch = await db.SupplierBranches.FirstOrDefaultAsync(b => b.Id == id && b.SupplierId == supplierId);
        if (branch is null) return NotFound(ApiResponse.Fail("Branch not found."));
        if (SupplierBranchesController.Validate(req) is { } error) return BadRequest(ApiResponse.Fail(error));
        if (await SupplierBranchesController.NameTakenAsync(db, supplierId, req.Name!, id))
            return Conflict(ApiResponse.Fail("This supplier already has a branch with this name."));

        SupplierBranchesController.Apply(branch, req);
        branch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(SupplierBranchesController.Project(branch), "Branch updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid supplierId, Guid id)
    {
        var branch = await db.SupplierBranches.FirstOrDefaultAsync(b => b.Id == id && b.SupplierId == supplierId);
        if (branch is null) return NotFound(ApiResponse.Fail("Branch not found."));
        db.SupplierBranches.Remove(branch);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Branch removed."));
    }
}
