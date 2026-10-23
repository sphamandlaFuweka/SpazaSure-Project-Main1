using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>The admin Customers tab: listing customers, and suspending or reinstating them.</summary>
public class AdminCustomersTests
{
    private const string Phone = "+27820000077";
    private const string Password = "CustomerPassword123";

    private static async Task<(SpazaSure.Infrastructure.Data.SpazaSureDbContext Db, CustomerAuthService Auth, AdminCustomersController Admin, Guid UserId)> CreateAsync()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        db.Roles.Add(new Role { Name = "customer", IsActive = true });
        await db.SaveChangesAsync();
        var auth = new CustomerAuthService(db, ConfigHelper.Build());
        var registered = await auth.RegisterAsync(
            new CustomerRegisterRequest(Phone, "Zanele", "Mthembu", Password, 30, ["Peanuts"],
                City: "Durban", Province: "KwaZulu-Natal", AcceptedPrivacyNotice: true),
            "127.0.0.1");
        registered.Success.Should().BeTrue();
        var userId = db.Users.Single(u => u.Phone == Phone).Id;
        db.CustomerScanEvents.Add(new CustomerScanEvent { CustomerUserId = userId, PointsAwarded = 5 });
        db.CustomerRewardTransactions.Add(new CustomerRewardTransaction { CustomerUserId = userId, Points = 5 });
        await db.SaveChangesAsync();
        return (db, auth, new AdminCustomersController(db, new EventPublisher("__nonexistent__", "x", "x")), userId);
    }

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((ObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("data");

    [Fact]
    public async Task List_ShowsCustomersWithTheirAreaAndActivity_AndSearches()
    {
        var (_, _, admin, _) = await CreateAsync();

        var data = Data(await admin.List(null, null));
        data.GetProperty("total").GetInt32().Should().Be(1);
        var customer = data.GetProperty("items")[0];
        customer.GetProperty("fullName").GetString().Should().Be("Zanele Mthembu");
        customer.GetProperty("city").GetString().Should().Be("Durban");
        customer.GetProperty("scanCount").GetInt32().Should().Be(1);
        customer.GetProperty("rewardPoints").GetInt32().Should().Be(5);
        customer.GetProperty("plan").GetString().Should().Be("free");
        customer.GetProperty("allergies")[0].GetString().Should().Be("Peanuts");

        Data(await admin.List("zanele", null)).GetProperty("total").GetInt32().Should().Be(1);
        Data(await admin.List("durban", null)).GetProperty("total").GetInt32().Should().Be(1);
        Data(await admin.List("nobody", null)).GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task SuspendedCustomer_CannotSignIn_UntilReinstated()
    {
        var (db, auth, admin, userId) = await CreateAsync();

        (await admin.Suspend(userId)).Should().BeOfType<OkObjectResult>();
        var refused = await auth.LoginAsync(new CustomerLoginRequest(Phone, Password), "127.0.0.1");
        refused.Success.Should().BeFalse();
        refused.Error.Should().Be(AuthenticationService.SuspendedMessage);
        Data(await admin.List(null, "suspended")).GetProperty("total").GetInt32().Should().Be(1);

        (await admin.Reinstate(userId)).Should().BeOfType<OkObjectResult>();
        (await auth.LoginAsync(new CustomerLoginRequest(Phone, Password), "127.0.0.1")).Success.Should().BeTrue();
        db.Users.Single(u => u.Id == userId).Status.Should().Be("active");
    }
}
