using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>Shops pay the once-off onboarding fee before they can buy in groups.</summary>
public class GroupBuyOnboardingFeeTests
{
    private static (ShopGroupBuyController Controller, SpazaSureDbContext Db, SpazaShop Shop) Create(bool feePaid)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var user = new User { Email = "gb@test.com", Phone = "0820007000", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "spaza_owner").Id };
        var shop = new SpazaShop { UserId = user.Id, ShopName = "Group Spaza", OwnerName = "Lerato", Phone = "0820007000", Address = "9 Test St", OnboardingFeePaid = feePaid };
        db.AddRange(user, shop);
        db.SaveChanges();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test")),
        };
        return (new ShopGroupBuyController(db, NullLogger<ShopGroupBuyController>.Instance)
            { ControllerContext = new ControllerContext { HttpContext = http } }, db, shop);
    }

    private static JoinGroupBuyRequest Request() =>
        new() { Items = [new JoinGroupBuyItemRequest { GroupBuyProductId = Guid.NewGuid(), Quantity = 10 }] };

    /// <summary>Joins past the fee check; the in-memory test database then can't open the join's transaction.</summary>
    private static async Task<IActionResult?> JoinPastTheFee(ShopGroupBuyController controller)
    {
        try { return await controller.Join(Guid.NewGuid(), Request()); }
        catch (InvalidOperationException) { return null; }
    }

    [Fact]
    public async Task UnpaidShop_IsAskedForTheFee()
    {
        var (controller, _, _) = Create(feePaid: false);

        var result = await controller.Join(Guid.NewGuid(), Request());

        var refused = result.Should().BeOfType<ObjectResult>().Subject;
        refused.StatusCode.Should().Be(StatusCodes.Status402PaymentRequired);
        System.Text.Json.JsonSerializer.Serialize(refused.Value).Should().Contain("R150.00");
    }

    [Fact]
    public async Task FeeSetByAdmin_IsTheAmountAsked()
    {
        var (controller, db, _) = Create(feePaid: false);
        db.PlatformSettings.Add(new PlatformSetting { Key = OnboardingFee.Key, Value = "200" });
        db.SaveChanges();

        var result = (ObjectResult)await controller.Join(Guid.NewGuid(), Request());
        System.Text.Json.JsonSerializer.Serialize(result.Value).Should().Contain("R200.00");
    }

    [Fact]
    public async Task NoFee_WhenAdminSetsItToZero()
    {
        var (controller, db, _) = Create(feePaid: false);
        db.PlatformSettings.Add(new PlatformSetting { Key = OnboardingFee.Key, Value = "0" });
        db.SaveChanges();

        (await JoinPastTheFee(controller) is ObjectResult { StatusCode: 402 }).Should().BeFalse();
    }

    [Fact]
    public async Task PaidShop_GoesOnToJoin()
    {
        var (controller, _, _) = Create(feePaid: true);

        (await JoinPastTheFee(controller) is ObjectResult { StatusCode: 402 }).Should().BeFalse();
    }
}
