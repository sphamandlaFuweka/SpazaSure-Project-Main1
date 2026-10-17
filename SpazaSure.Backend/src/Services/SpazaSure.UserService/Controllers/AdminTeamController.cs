using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

public record StaffAccessRequest(bool IsMaster, Dictionary<string, string>? Areas);

public record CreateStaffRequest(
    string? Email,
    string? DisplayName,
    string? Title,
    string? Password,
    StaffAccessRequest? Access);

public record UpdateStaffRequest(string? DisplayName, string? Title, StaffAccessRequest? Access);

public record StaffStatusRequest(bool Active);

/// <summary>Who the signed-in staff member is and what they can reach. Any admin can call it.</summary>
[ApiController]
[Route("api/admin/me")]
[Authorize(Roles = "admin")]
public class AdminMeController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId);
        if (user is null) return NotFound(ApiResponse.Fail("Account not found."));
        var grant = AdminAccess.Parse(user.AdminAccess);
        return Ok(ApiResponse<object>.Ok(new
        {
            user.Id,
            user.Email,
            DisplayName = user.DisplayName ?? "",
            Title = user.AdminTitle ?? (grant.IsMaster ? "Master admin" : "Staff"),
            grant.IsMaster,
            Access = grant.ToAreaMap()
        }));
    }
}

/// <summary>
/// The master admin registers the people who use the admin console and decides, area by area,
/// whether each person can view or manage it. Only master admins reach this controller: the
/// admin access middleware treats /api/admin/team as master only.
/// </summary>
[ApiController]
[Route("api/admin/team")]
[Authorize(Roles = "admin")]
public class AdminTeamController(SpazaSureDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Defence in depth: the middleware already enforces this in the running services.</summary>
    private async Task<bool> CallerIsMasterAsync()
    {
        var access = await db.Users.AsNoTracking()
            .Where(u => u.Id == UserId).Select(u => new { u.AdminAccess }).FirstOrDefaultAsync();
        return access is not null && AdminAccess.Parse(access.AdminAccess).IsMaster;
    }

    private IActionResult NotMaster() =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("Only a master admin can manage the team."));

    private IQueryable<User> Staff => db.Users.Where(u => u.Role.Name == "admin");

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (!await CallerIsMasterAsync()) return NotMaster();

        var staff = await Staff.AsNoTracking().OrderBy(u => u.CreatedAt).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            Areas = AdminAccess.Areas.Select(a => new { a.Key, a.Label, a.Description }),
            Members = staff.Select(Project).ToList()
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStaffRequest req)
    {
        if (!await CallerIsMasterAsync()) return NotMaster();

        var email = req.Email?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > 254 || !MailAddress.TryCreate(email, out _))
            return BadRequest(ApiResponse.Fail("Enter a valid email address."));
        var name = req.DisplayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
            return BadRequest(ApiResponse.Fail("Enter the person's name (up to 120 characters)."));
        var title = Clean(req.Title, 80);
        if (title.Error is not null) return BadRequest(ApiResponse.Fail(title.Error));

        var (access, accessError) = AdminAccess.Serialize(req.Access?.IsMaster ?? false, req.Access?.Areas);
        if (accessError is not null) return BadRequest(ApiResponse.Fail(accessError));

        if (await db.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower()))
            return BadRequest(ApiResponse.Fail("Someone is already registered with that email."));

        var password = req.Password?.Trim();
        var generated = string.IsNullOrEmpty(password);
        if (generated) password = TemporaryPassword();
        else if (password!.Length < 8) return BadRequest(ApiResponse.Fail("The password must be at least 8 characters."));

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "admin" && r.IsActive);
        if (role is null) return StatusCode(500, ApiResponse.Fail("The admin role is missing."));

        var user = new User
        {
            Email = email,
            DisplayName = name,
            AdminTitle = title.Value,
            AdminAccess = access,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RoleId = role.Id,
            Status = "active"
        };
        db.Users.Add(user);
        db.AuthAuditLogs.Add(new AuthAuditLog { UserId = UserId, Event = $"staff_created:{user.Id}" });
        await db.SaveChangesAsync();

        user.Role = role;
        return Ok(ApiResponse<object>.Ok(new
        {
            Member = Project(user),
            // Shown once so the master admin can pass it on. They should change it after signing in.
            TemporaryPassword = generated ? password : null
        }, $"{name} can now sign in to the admin console."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStaffRequest req)
    {
        if (!await CallerIsMasterAsync()) return NotMaster();

        var user = await Staff.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(ApiResponse.Fail("Team member not found."));

        if (req.DisplayName is not null)
        {
            var name = req.DisplayName.Trim();
            if (name.Length is 0 or > 120) return BadRequest(ApiResponse.Fail("Enter the person's name (up to 120 characters)."));
            user.DisplayName = name;
        }
        if (req.Title is not null)
        {
            var title = Clean(req.Title, 80);
            if (title.Error is not null) return BadRequest(ApiResponse.Fail(title.Error));
            user.AdminTitle = title.Value;
        }
        if (req.Access is not null)
        {
            var (access, error) = AdminAccess.Serialize(req.Access.IsMaster, req.Access.Areas);
            if (error is not null) return BadRequest(ApiResponse.Fail(error));

            var losesMaster = AdminAccess.Parse(user.AdminAccess).IsMaster && !req.Access.IsMaster;
            if (losesMaster && id == UserId)
                return BadRequest(ApiResponse.Fail("You can't remove your own master access. Ask another master admin."));
            if (losesMaster && !await AnotherActiveMasterExistsAsync(id))
                return BadRequest(ApiResponse.Fail("There must always be at least one active master admin."));
            user.AdminAccess = access;
        }

        db.AuthAuditLogs.Add(new AuthAuditLog { UserId = UserId, Event = $"staff_updated:{user.Id}" });
        await db.SaveChangesAsync();
        await db.Entry(user).Reference(u => u.Role).LoadAsync();
        return Ok(ApiResponse<object>.Ok(Project(user), "Team member updated."));
    }

    /// <summary>Suspending signs the person out everywhere; their admin requests stop working at once.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] StaffStatusRequest req)
    {
        if (!await CallerIsMasterAsync()) return NotMaster();
        if (id == UserId) return BadRequest(ApiResponse.Fail("You can't suspend your own account."));

        var user = await Staff.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(ApiResponse.Fail("Team member not found."));

        if (!req.Active)
        {
            if (AdminAccess.Parse(user.AdminAccess).IsMaster && !await AnotherActiveMasterExistsAsync(id))
                return BadRequest(ApiResponse.Fail("There must always be at least one active master admin."));
            user.Status = "suspended";
            foreach (var token in user.RefreshTokens.Where(t => t.RevokedAt == null)) token.RevokedAt = DateTime.UtcNow;
        }
        else
        {
            user.Status = "active";
            user.FailedAttempts = 0;
            user.LockedUntil = null;
        }

        db.AuthAuditLogs.Add(new AuthAuditLog { UserId = UserId, Event = $"staff_{(req.Active ? "reactivated" : "suspended")}:{user.Id}" });
        await db.SaveChangesAsync();
        await db.Entry(user).Reference(u => u.Role).LoadAsync();
        return Ok(ApiResponse<object>.Ok(Project(user), req.Active ? "Access restored." : "Access suspended."));
    }

    /// <summary>Issues a new temporary password, shown once, and signs the person out everywhere.</summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        if (!await CallerIsMasterAsync()) return NotMaster();

        var user = await Staff.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(ApiResponse.Fail("Team member not found."));

        var password = TemporaryPassword();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.FailedAttempts = 0;
        user.LockedUntil = null;
        foreach (var token in user.RefreshTokens.Where(t => t.RevokedAt == null)) token.RevokedAt = DateTime.UtcNow;
        db.AuthAuditLogs.Add(new AuthAuditLog { UserId = UserId, Event = $"staff_password_reset:{user.Id}" });
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { TemporaryPassword = password }, "New temporary password created."));
    }

    private async Task<bool> AnotherActiveMasterExistsAsync(Guid exceptId)
    {
        var others = await Staff.AsNoTracking()
            .Where(u => u.Id != exceptId && u.Status != "suspended")
            .Select(u => u.AdminAccess)
            .ToListAsync();
        return others.Any(a => AdminAccess.Parse(a).IsMaster);
    }

    private object Project(User user)
    {
        var grant = AdminAccess.Parse(user.AdminAccess);
        return new
        {
            user.Id,
            user.Email,
            DisplayName = user.DisplayName ?? "",
            Title = user.AdminTitle ?? (grant.IsMaster ? "Master admin" : ""),
            grant.IsMaster,
            Access = grant.ToAreaMap(),
            Active = user.Status != "suspended",
            IsYou = user.Id == UserId,
            user.LastLoginAt,
            user.CreatedAt
        };
    }

    private static (string? Value, string? Error) Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return (null, null);
        return trimmed.Length > max ? (null, $"The job title can be up to {max} characters.") : (trimmed, null);
    }

    /// <summary>12 characters from an alphabet without look-alikes (no 0/O, 1/l/I).</summary>
    private static string TemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        return string.Create(12, alphabet, (span, chars) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        });
    }
}
