using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// Dashboard staff roles. Every staff member signs in with the "admin" role; what they can reach
/// inside the admin console is decided per area by the master admin:
///   master  - everything, plus registering staff and changing their access
///   view    - read-only access to an area (GET requests)
///   manage  - read and change things in an area
/// The grant is stored on the user as "master" or e.g. "trust:manage,finance:view".
/// </summary>
public static class AdminAccess
{
    public const string Master = "master";
    public const string View = "view";
    public const string Manage = "manage";

    public const string Trust = "trust";
    public const string Marketplace = "marketplace";
    public const string Finance = "finance";
    public const string Projects = "projects";
    public const string Settings = "settings";

    /// <summary>The areas a master admin can hand out, in sidebar order.</summary>
    public static readonly IReadOnlyList<(string Key, string Label, string Description)> Areas =
    [
        (Trust, "Trust & safety", "Suppliers and shops, documents, customer reports, shops map"),
        (Marketplace, "Marketplace", "Products, categories, orders, group buys and analytics"),
        (Finance, "Finance", "Supplier payouts, wallet deposits and withdrawals, ledger, finance reports"),
        (Projects, "Projects & impact", "Project management, MEL and reporting workspace"),
        (Settings, "Platform settings", "Platform rules, fees, maintenance mode and FAQs"),
    ];

    private static readonly HashSet<string> AreaKeys = Areas.Select(a => a.Key).ToHashSet();

    /// <summary>Area that protects a request path. "*" means any admin may call it; null means master only.</summary>
    private static readonly (string Prefix, string? Area)[] Routes =
    [
        ("/api/admin/me", "*"),
        ("/api/admin/team", null),
        ("/api/admin/suppliers", Trust),
        ("/api/admin/spaza-owners", Trust),
        ("/api/admin/reports", Trust),
        ("/api/compliance/documents", Trust),
        ("/api/admin/products", Marketplace),
        ("/api/admin/orders", Marketplace),
        ("/api/admin/group-buy", Marketplace), // also covers /api/admin/group-buy-banner
        ("/api/admin/analytics", Marketplace),
        ("/api/admin/categories", Marketplace),
        ("/api/admin/payouts", Finance),
        ("/api/admin/wallet-top-ups", Finance),
        ("/api/admin/wallet-withdrawals", Finance),
        ("/api/admin/finance", Finance),
        ("/api/admin/projects", Projects),
        ("/api/admin/settings", Settings),
        ("/api/admin/faqs", Settings),
    ];

    /// <summary>Whether a path is part of the admin console and so subject to staff access.</summary>
    public static bool IsAdminPath(string path) =>
        path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/compliance/documents", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The area guarding a path: "*" for any admin, an area key, or null for master only.
    /// Unknown admin paths are master only, so a new admin endpoint is closed until it is mapped.
    /// </summary>
    public static string? AreaFor(string path)
    {
        foreach (var (prefix, area) in Routes)
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return area;
        return null;
    }

    /// <summary>Parses a stored grant. Null (an admin that predates staff roles) means master.</summary>
    public static Grant Parse(string? stored)
    {
        if (stored is null) return Grant.MasterGrant;
        var parts = stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Any(p => string.Equals(p, Master, StringComparison.OrdinalIgnoreCase))) return Grant.MasterGrant;

        var areas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            var bits = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (bits.Length != 2 || !AreaKeys.Contains(bits[0])) continue;
            var level = bits[1].ToLowerInvariant();
            if (level is not (View or Manage)) continue;
            // Keep the stronger level if an area appears twice.
            if (!areas.TryGetValue(bits[0], out var existing) || existing == View)
                areas[bits[0].ToLowerInvariant()] = level;
        }
        return new Grant(false, areas);
    }

    /// <summary>
    /// Builds the stored string from a request. Returns an error for unknown areas or levels.
    /// "none" levels are dropped.
    /// </summary>
    public static (string? Stored, string? Error) Serialize(bool isMaster, IDictionary<string, string>? areas)
    {
        if (isMaster) return (Master, null);
        var parts = new List<string>();
        foreach (var (key, rawLevel) in areas ?? new Dictionary<string, string>())
        {
            var area = key.Trim().ToLowerInvariant();
            var level = (rawLevel ?? "").Trim().ToLowerInvariant();
            if (!AreaKeys.Contains(area)) return (null, $"Unknown area '{key}'.");
            if (level is "" or "none") continue;
            if (level is not (View or Manage)) return (null, $"Access for '{key}' must be none, view or manage.");
            parts.Add($"{area}:{level}");
        }
        // Stored in a fixed order so the same grant always reads the same.
        var ordered = Areas.Select(a => parts.FirstOrDefault(p => p.StartsWith(a.Key + ":"))).Where(p => p is not null);
        return (string.Join(',', ordered), null);
    }

    /// <summary>A staff member's parsed access.</summary>
    public sealed record Grant(bool IsMaster, IReadOnlyDictionary<string, string> Areas)
    {
        public static readonly Grant MasterGrant = new(true, new Dictionary<string, string>());

        /// <summary>The level for an area: manage, view or none.</summary>
        public string LevelFor(string area) =>
            IsMaster ? Manage : Areas.TryGetValue(area, out var level) ? level : "none";

        /// <summary>Whether this grant allows the request: reads need view, anything else needs manage.</summary>
        public bool Allows(string path, string method)
        {
            if (IsMaster) return true;
            var area = AreaFor(path);
            if (area == "*") return true;
            if (area is null) return false;
            var level = LevelFor(area);
            var isRead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
            // Platform rules are read by the payouts and deposits pages too; reading them is harmless.
            if (isRead && area == Settings && path.StartsWith("/api/admin/settings", StringComparison.OrdinalIgnoreCase))
                return true;
            return level == Manage || (isRead && level == View);
        }

        /// <summary>The access map sent to the admin console, covering every area.</summary>
        public Dictionary<string, string> ToAreaMap() =>
            AdminAccess.Areas.ToDictionary(a => a.Key, a => LevelFor(a.Key));
    }
}

/// <summary>Looks up a staff member's current access. Implemented against the database by each service.</summary>
public interface IAdminAccessStore
{
    /// <summary>Returns the stored grant and whether the account is active, or null when the user does not exist.</summary>
    Task<(string? Access, bool Active)?> FindAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// Runs after authorization. For signed-in admins calling an admin endpoint, checks the staff member's
/// current access in the database (so a change by the master admin applies on the next request) and
/// refuses with 403 when the area is not granted.
/// </summary>
public sealed class AdminAccessMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var user = context.User;
        if (!AdminAccess.IsAdminPath(path) || user.Identity?.IsAuthenticated != true || !user.IsInRole("admin"))
        {
            await next(context);
            return;
        }

        var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        var store = context.RequestServices.GetService<IAdminAccessStore>();
        if (store is null || !Guid.TryParse(idValue, out var userId))
        {
            await Deny(context, "Your admin access could not be checked.");
            return;
        }

        var found = await store.FindAsync(userId, context.RequestAborted);
        if (found is not { Active: true } account)
        {
            await Deny(context, "This admin account is no longer active.");
            return;
        }

        if (!AdminAccess.Parse(account.Access).Allows(path, context.Request.Method))
        {
            var area = AdminAccess.AreaFor(path);
            var label = AdminAccess.Areas.FirstOrDefault(a => a.Key == area).Label;
            await Deny(context, label is null
                ? "Only a master admin can do this."
                : $"You don't have access to {label}. Ask a master admin if you need it.");
            return;
        }

        await next(context);
    }

    private static Task Deny(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        // noAccess lets the admin console stay quiet for background reads it is not allowed to make.
        return context.Response.WriteAsync(JsonSerializer.Serialize(
            new { success = false, message, noAccess = true }, Json));
    }
}

public static class AdminAccessExtensions
{
    /// <summary>Enforces dashboard staff access on admin endpoints. Call after UseAuthorization.</summary>
    public static IApplicationBuilder UseAdminAccess(this IApplicationBuilder app) =>
        app.UseMiddleware<AdminAccessMiddleware>();
}
