using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Auth;

public class MobileOtpAuthTests
{
    [Fact]
    public async Task Shop_login_requires_a_delivered_otp_and_consumes_it_once()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.Single(r => r.Name == "spaza_owner");
        var user = new User { Phone = "+27820000001", RoleId = role.Id, Status = "active" };
        var shop = new SpazaShop
        {
            UserId = user.Id,
            ShopName = "Test Shop",
            OwnerName = "Test Owner",
            Phone = user.Phone!
        };
        db.Users.Add(user);
        db.SpazaShops.Add(shop);
        await db.SaveChangesAsync();

        var sms = new RecordingSmsService();
        var service = new ShopAuthService(
            db, ConfigHelper.Build(), NullLogger<ShopAuthService>.Instance, sms);

        var sent = await service.SendOtpAsync(user.Phone!, "login");
        Assert.True(sent.Success);
        Assert.NotNull(sms.LastOtp);

        var resend = await service.SendOtpAsync(user.Phone!, "login");
        Assert.False(resend.Success);

        var wrongCode = await service.LoginAsync(
            new ShopLoginRequest(user.Phone!, "000000"), "127.0.0.1");
        Assert.False(wrongCode.Success);

        var login = await service.LoginAsync(
            new ShopLoginRequest(user.Phone!, sms.LastOtp!), "127.0.0.1");
        Assert.True(login.Success);
        Assert.NotNull(login.Data);

        var reusedCode = await service.LoginAsync(
            new ShopLoginRequest(user.Phone!, sms.LastOtp!), "127.0.0.1");
        Assert.False(reusedCode.Success);
    }

    [Fact]
    public async Task Customer_login_requires_a_delivered_otp()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var role = new Role { Name = "customer", IsActive = true };
        db.Roles.Add(role);
        var user = new User { Phone = "+27820000002", Role = role, Status = "active" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sms = new RecordingSmsService();
        var service = new CustomerAuthService(
            db, ConfigHelper.Build(), NullLogger<CustomerAuthService>.Instance, sms);

        var sent = await service.SendOtpAsync(user.Phone!, "login");
        Assert.True(sent.Success);
        Assert.NotNull(sms.LastOtp);

        var login = await service.LoginAsync(
            new CustomerLoginRequest(user.Phone!, sms.LastOtp!), "127.0.0.1");
        Assert.True(login.Success);
        Assert.NotNull(login.Data);
    }

    [Fact]
    public async Task Failed_sms_delivery_does_not_leave_a_verifiable_otp()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var sms = new RecordingSmsService { ShouldSend = false };
        var service = new ShopAuthService(
            db, ConfigHelper.Build(), NullLogger<ShopAuthService>.Instance, sms);

        var sent = await service.SendOtpAsync("+27820000003", "login");

        Assert.False(sent.Success);
        Assert.Empty(db.OtpCodes);
    }

    private sealed class RecordingSmsService : ISmsService
    {
        public bool ShouldSend { get; init; } = true;
        public string? LastOtp { get; private set; }

        public Task<bool> SendOtpAsync(string phone, string otp)
        {
            LastOtp = otp;
            return Task.FromResult(ShouldSend);
        }
    }
}
