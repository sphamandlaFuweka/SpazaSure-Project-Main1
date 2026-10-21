using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.ComplianceService.Controllers;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>
/// Didit can leave a check "In Review" (e.g. the ID number on the document differs from the
/// profile). An admin then decides, so verification can always be completed.
/// </summary>
public class AdminIdentityDecisionTests
{
    private static (SpazaSureDbContext Db, IdentityChecksController Admin, SpazaShop Shop) Setup(string? status = "In Review")
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var adminRole = db.Roles.Single(r => r.Name == "admin");
        var shopRole = db.Roles.Single(r => r.Name == "spaza_owner");
        var admin = new User { Email = "lindiwe@spazasure.co.za", DisplayName = "Lindiwe", PasswordHash = "h", RoleId = adminRole.Id, Phone = "0820000001" };
        var owner = new User { Phone = "0820000002", PasswordHash = "h", RoleId = shopRole.Id };
        var shop = new SpazaShop
        {
            UserId = owner.Id, ShopName = "Mama's", OwnerName = "Thandi", Phone = "0820000002", Address = "1 St",
            IdentityCheckSessionId = "didit-session-1", IdentityCheckStatus = status,
            IdentityCheckNotes = "ID number doesn't match the one provided",
            IdentityCheckUpdatedAt = DateTime.UtcNow.AddHours(-1),
        };
        db.AddRange(admin, owner, shop);
        db.SaveChanges();
        var controller = new IdentityChecksController(db, null!, new EventPublisher("__nonexistent__", "x", "x"), null!,
            NullLogger<IdentityChecksController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Role, "admin")], "Test")),
                },
            },
        };
        return (db, controller, shop);
    }

    [Fact]
    public async Task AdminApprovesACheckInReview_WithAReason_AndItIsRecorded()
    {
        var (db, admin, shop) = Setup();

        var result = await admin.DecideForAdmin("shop", shop.Id, new AdminDecisionBody(true, "Checked the original ID in person"), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.SpazaShops.SingleAsync();
        saved.IdentityCheckStatus.Should().Be("Approved");
        saved.IdentityCheckNotes.Should().StartWith("Approved by Lindiwe on").And.Contain("Checked the original ID in person")
            .And.Contain("ID number doesn't match"); // Didit's finding is kept
        saved.IdentityCheckSessionId.Should().StartWith("admin:");  // a late result from the old session is ignored
        IdentityChecks.HasPassed(saved).Should().BeTrue();         // documents can now be approved
        db.AuthAuditLogs.Should().Contain(a => a.Event.StartsWith("identity_decision_by_admin:Approved"));
    }

    [Fact]
    public async Task AdminDeclines_AndTheShopIsNotVerified()
    {
        var (db, admin, shop) = Setup();

        await admin.DecideForAdmin("shop", shop.Id, new AdminDecisionBody(false, "ID photo belongs to someone else"), CancellationToken.None);

        (await db.SpazaShops.SingleAsync()).IdentityCheckStatus.Should().Be("Declined");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ok")]
    public async Task ADecisionWithoutAReason_IsRefused(string? reason)
    {
        var (db, admin, shop) = Setup();

        var result = await admin.DecideForAdmin("shop", shop.Id, new AdminDecisionBody(true, reason), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.SpazaShops.SingleAsync()).IdentityCheckStatus.Should().Be("In Review");
    }

    [Fact]
    public async Task WhileACheckIsStillRunning_TheAdminWaits()
    {
        var (db, admin, shop) = Setup("In Progress");
        shop.IdentityCheckUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await admin.DecideForAdmin("shop", shop.Id, new AdminDecisionBody(true, "Looks fine to me"), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }
}
