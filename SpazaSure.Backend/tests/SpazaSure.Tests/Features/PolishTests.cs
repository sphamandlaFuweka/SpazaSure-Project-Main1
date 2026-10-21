using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.ComplianceService.Controllers;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class PolishTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");
    private static readonly Guid Reporter = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ReportsController Reports() =>
        new(null!, null!, Events, ConfigHelper.Build(new() { ["Storage:Local:UrlPrefix"] = "/compliance-uploads" }));

    [Theory]
    [InlineData("/compliance-uploads/report-photos/11111111-1111-1111-1111-111111111111/20261021093000.jpg", true)]
    [InlineData("/compliance-uploads/report-photos/11111111-1111-1111-1111-111111111111/20261021093000.png?exp=1&sig=ab", true)]
    [InlineData("/compliance-uploads/report-photos/22222222-2222-2222-2222-222222222222/20261021093000.jpg", false)] // someone else's
    [InlineData("/uploads/shop-docs/2b9f/id_document_20261021093000.pdf", false)]                                    // an ID document
    [InlineData("/compliance-uploads/report-photos/11111111-1111-1111-1111-111111111111/../../shop-docs/x.pdf", false)]
    [InlineData("/compliance-uploads/report-photos/11111111-1111-1111-1111-111111111111/20261021093000.html", false)]
    [InlineData("https://evil.example/report-photos/11111111-1111-1111-1111-111111111111/20261021093000.jpg", false)]
    public void ReportPhoto_MustBeTheReportersOwnUpload(string url, bool accepted)
    {
        var result = Reports().OwnPhotoUrl(url, Reporter);

        if (accepted) result.Should().Be(url.Split('?')[0]);
        else result.Should().BeNull();
    }

    [Fact]
    public async Task ExistingUser_IsAskedToAcceptTheCurrentNotice_Once()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var role = await db.Roles.SingleAsync(r => r.Name == "supplier");
        var user = new User { Phone = "+27820000088", Email = "s@example.co.za", PasswordHash = "hash", RoleId = role.Id, Status = "active" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = new AccountPrivacyController(db, Events)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, "supplier")], "Test")),
                },
            },
        };

        async Task<bool> Accepted() =>
            JsonSerializer.Serialize(((OkObjectResult)await controller.GetConsent(CancellationToken.None)).Value)
                .Contains("\"accepted\":true");

        (await Accepted()).Should().BeFalse();
        await controller.AcceptConsent(CancellationToken.None);
        await controller.AcceptConsent(CancellationToken.None); // a second click doesn't record twice
        (await Accepted()).Should().BeTrue();
        db.AuthAuditLogs.Count(a => a.UserId == user.Id && a.Event.StartsWith("consent_privacy_notice")).Should().Be(1);
    }
}
