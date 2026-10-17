using Microsoft.EntityFrameworkCore;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>Reads a staff member's current admin access straight from the users table.</summary>
public sealed class AdminAccessStore(SpazaSureDbContext db) : IAdminAccessStore
{
    public async Task<(string? Access, bool Active)?> FindAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.Role.Name == "admin")
            .Select(u => new { u.AdminAccess, u.Status })
            .FirstOrDefaultAsync(ct);
        return user is null ? null : (user.AdminAccess, user.Status != "suspended");
    }
}
