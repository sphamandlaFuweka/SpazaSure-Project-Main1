using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>Master admin, staff roles and per-area admin access.</summary>
public class AdminStaffAccessTests
{
    // ── Access rules ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NullAccess_IsMaster_SoExistingAdminsKeepEverything()
    {
        AdminAccess.Parse(null).IsMaster.Should().BeTrue();
        AdminAccess.Parse("master").IsMaster.Should().BeTrue();
    }

    [Fact]
    public void Parse_EmptyAccess_HasNoAreas()
    {
        var grant = AdminAccess.Parse("");
        grant.IsMaster.Should().BeFalse();
        grant.ToAreaMap().Values.Should().OnlyContain(level => level == "none");
    }

    [Theory]
    [InlineData("/api/admin/wallet-top-ups", "GET", true)]
    [InlineData("/api/admin/wallet-top-ups/1/approve", "PATCH", false)] // view only
    [InlineData("/api/admin/suppliers", "GET", true)]
    [InlineData("/api/admin/suppliers/1/verify", "PATCH", true)]          // manage
    [InlineData("/api/compliance/documents/1/approve", "PATCH", true)]
    [InlineData("/api/admin/products", "GET", false)]                     // no marketplace access
    [InlineData("/api/admin/group-buy", "POST", false)]
    [InlineData("/api/admin/team", "GET", false)]                         // master only
    [InlineData("/api/admin/something-new", "GET", false)]                // unmapped: master only
    [InlineData("/api/admin/me", "GET", true)]                            // anyone
    [InlineData("/api/admin/settings/platform", "GET", true)]             // reading rules is open
    [InlineData("/api/admin/settings/platform", "PUT", false)]
    public void Grant_Allows_ByAreaAndLevel(string path, string method, bool allowed)
    {
        var grant = AdminAccess.Parse("trust:manage,finance:view");
        grant.Allows(path, method).Should().Be(allowed);
    }

    [Fact]
    public void Master_AllowsEverything()
    {
        var grant = AdminAccess.Parse("master");
        grant.Allows("/api/admin/team", "POST").Should().BeTrue();
        grant.Allows("/api/admin/finance/summary", "GET").Should().BeTrue();
    }

    [Fact]
    public void Serialize_DropsNone_AndOrdersAreas()
    {
        var (stored, error) = AdminAccess.Serialize(false, new Dictionary<string, string>
        {
            ["finance"] = "view", ["trust"] = "manage", ["projects"] = "none"
        });
        error.Should().BeNull();
        stored.Should().Be("trust:manage,finance:view");
    }

    [Fact]
    public void Serialize_RejectsUnknownAreaOrLevel()
    {
        AdminAccess.Serialize(false, new Dictionary<string, string> { ["payroll"] = "view" }).Error.Should().NotBeNull();
        AdminAccess.Serialize(false, new Dictionary<string, string> { ["trust"] = "owner" }).Error.Should().NotBeNull();
    }

    // ── Middleware ──────────────────────────────────────────────────────────────

    private sealed class FakeStore(string? access, bool active = true) : IAdminAccessStore
    {
        public Task<(string? Access, bool Active)?> FindAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<(string? Access, bool Active)?>((access, active));
    }

    private static async Task<(int Status, bool Reached, string Body)> RunMiddleware(
        IAdminAccessStore store, string path, string method, string role = "admin")
    {
        var reached = false;
        var middleware = new AdminAccessMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(store).BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)], "Test"))
        };
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, reached, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task Middleware_RefusesAreaWithoutAccess_WithNoAccessFlag()
    {
        var (status, reached, body) = await RunMiddleware(new FakeStore("finance:view"), "/api/admin/products", "GET");

        status.Should().Be(403);
        reached.Should().BeFalse();
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("noAccess").GetBoolean().Should().BeTrue();
        json.GetProperty("message").GetString().Should().Contain("Marketplace");
    }

    [Fact]
    public async Task Middleware_LetsGrantedRequestThrough()
    {
        var (_, reached, _) = await RunMiddleware(new FakeStore("marketplace:manage"), "/api/admin/products/1/approve", "PATCH");
        reached.Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_RefusesSuspendedStaffImmediately()
    {
        var (status, reached, _) = await RunMiddleware(new FakeStore("master", active: false), "/api/admin/orders", "GET");
        status.Should().Be(403);
        reached.Should().BeFalse();
    }

    [Fact]
    public async Task Middleware_IgnoresNonAdminCallers()
    {
        // Role checks on the controller handle these; the middleware only narrows admin access.
        var (_, reached, _) = await RunMiddleware(new FakeStore(""), "/api/admin/orders", "GET", role: "supplier");
        reached.Should().BeTrue();
    }

    [Fact]
    public async Task AdminAccessStore_ReportsSuspendedStaffAsInactive()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var staff = AddStaff(db, "s@test.com", "trust:view", status: "suspended");

        var found = await new AdminAccessStore(db).FindAsync(staff.Id, default);

        found.Should().NotBeNull();
        found!.Value.Active.Should().BeFalse();
    }

    // ── Team management ─────────────────────────────────────────────────────────

    private static User AddStaff(SpazaSureDbContext db, string email, string? access, string status = "active")
    {
        var role = db.Roles.First(r => r.Name == "admin");
        var user = new User { Email = email, PasswordHash = "h", RoleId = role.Id, Status = status, AdminAccess = access, DisplayName = email };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static AdminTeamController Team(SpazaSureDbContext db, Guid callerId) => new(db)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, callerId.ToString()), new Claim(ClaimTypes.Role, "admin")], "Test"))
            }
        }
    };

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    [Fact]
    public async Task Master_RegistersStaff_WithLimitedAccess_AndGetsTemporaryPassword()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var master = AddStaff(db, "boss@test.com", "master");

        var result = await Team(db, master.Id).Create(new CreateStaffRequest(
            "finance@test.com", "Thandi", "Finance officer", null,
            new StaffAccessRequest(false, new Dictionary<string, string> { ["finance"] = "manage", ["marketplace"] = "view" })));

        var data = Data(result);
        var temp = data.GetProperty("temporaryPassword").GetString();
        temp.Should().HaveLength(12);
        data.GetProperty("member").GetProperty("isMaster").GetBoolean().Should().BeFalse();

        var saved = db.Users.Single(u => u.Email == "finance@test.com");
        saved.AdminAccess.Should().Be("marketplace:view,finance:manage");
        saved.AdminTitle.Should().Be("Finance officer");
        saved.Status.Should().Be("active");
        BCrypt.Net.BCrypt.Verify(temp, saved.PasswordHash).Should().BeTrue();
        db.Roles.Single(r => r.Id == saved.RoleId).Name.Should().Be("admin");
    }

    [Fact]
    public async Task NonMaster_CannotManageTeam()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var staff = AddStaff(db, "ops@test.com", "trust:manage,marketplace:manage,finance:manage,projects:manage,settings:manage");

        var result = await Team(db, staff.Id).Create(new CreateStaffRequest("x@test.com", "X", null, null, new StaffAccessRequest(true, null)));

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        db.Users.Any(u => u.Email == "x@test.com").Should().BeFalse();
    }

    [Fact]
    public async Task Create_RejectsDuplicateEmail()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var master = AddStaff(db, "boss@test.com", "master");

        var result = await Team(db, master.Id).Create(new CreateStaffRequest("BOSS@test.com", "Again", null, "Password123", null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task LastMaster_CannotBeDemotedOrSuspended()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var master = AddStaff(db, "boss@test.com", "master");
        var other = AddStaff(db, "other@test.com", "master");
        var team = Team(db, master.Id);

        // With two masters, one can be demoted...
        (await team.Update(other.Id, new UpdateStaffRequest(null, null, new StaffAccessRequest(false, null))))
            .Should().BeOfType<OkObjectResult>();
        // ...but you can't demote yourself, and you can't suspend yourself.
        (await team.Update(master.Id, new UpdateStaffRequest(null, null, new StaffAccessRequest(false, null))))
            .Should().BeOfType<BadRequestObjectResult>();
        (await team.SetStatus(master.Id, new StaffStatusRequest(false)))
            .Should().BeOfType<BadRequestObjectResult>();
        AdminAccess.Parse(db.Users.Single(u => u.Id == master.Id).AdminAccess).IsMaster.Should().BeTrue();
    }

    [Fact]
    public async Task Suspend_RevokesSessions()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var master = AddStaff(db, "boss@test.com", "master");
        var staff = AddStaff(db, "ops@test.com", "trust:view");
        db.RefreshTokens.Add(new RefreshToken { UserId = staff.Id, TokenHash = "t", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        db.SaveChanges();

        (await Team(db, master.Id).SetStatus(staff.Id, new StaffStatusRequest(false))).Should().BeOfType<OkObjectResult>();

        db.Users.Single(u => u.Id == staff.Id).Status.Should().Be("suspended");
        db.RefreshTokens.Single(t => t.UserId == staff.Id).RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Me_ReturnsAreaMapForStaff()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var staff = AddStaff(db, "ops@test.com", "projects:manage");
        var ctrl = new AdminMeController(db) { ControllerContext = Team(db, staff.Id).ControllerContext };

        var data = Data(await ctrl.Get());

        data.GetProperty("isMaster").GetBoolean().Should().BeFalse();
        data.GetProperty("access").GetProperty("projects").GetString().Should().Be("manage");
        data.GetProperty("access").GetProperty("finance").GetString().Should().Be("none");
    }

    // ── Public sign-up ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PublicRegistration_CannotCreateAdmin()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var svc = new AuthenticationService(db, ConfigHelper.Build());

        var (success, error, _) = await svc.RegisterAsync(new RegisterRequest(
            "admin", "sneaky@test.com", "0820000001", "Password123!", null, null, null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");

        success.Should().BeFalse();
        error.Should().Contain("can't be registered");
        db.Users.Any(u => u.Email == "sneaky@test.com").Should().BeFalse();
    }
}
