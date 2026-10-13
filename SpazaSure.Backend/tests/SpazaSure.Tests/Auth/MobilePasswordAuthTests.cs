using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Auth;

public class MobilePasswordAuthTests
{
    [Fact]
    public async Task Shop_registration_hashes_password_and_login_accepts_it()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.Single(r => r.Name == "spaza_owner");
        const string phone = "+27820000001";
        const string password = "StrongPassword123";
        var service = new ShopAuthService(db, ConfigHelper.Build());

        var registration = await service.RegisterAsync(
            new ShopRegisterRequest(
                phone, password, "Test Owner", "Test Shop", "1 Main Street", null),
            "127.0.0.1");

        Assert.True(registration.Success);
        var user = Assert.Single(db.Users);
        Assert.Equal(phone, user.Phone);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
        Assert.Null(user.Email);

        var login = await service.LoginAsync(
            new ShopLoginRequest(phone, password), "127.0.0.1");

        Assert.True(login.Success);
        Assert.NotNull(login.Data);
        Assert.Equal(role.Name, db.Roles.Single(r => r.Id == user.RoleId).Name);
    }

    [Fact]
    public async Task Shop_login_rejects_wrong_password_and_locks_after_five_attempts()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.Single(r => r.Name == "spaza_owner");
        var user = new User
        {
            Phone = "+27820000002",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password"),
            RoleId = role.Id,
            Status = "active"
        };
        db.Users.Add(user);
        db.SpazaShops.Add(new SpazaShop
        {
            UserId = user.Id,
            ShopName = "Test Shop",
            OwnerName = "Test Owner",
            Phone = user.Phone!
        });
        await db.SaveChangesAsync();
        var service = new ShopAuthService(db, ConfigHelper.Build());

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var result = await service.LoginAsync(
                new ShopLoginRequest(user.Phone!, "wrong-password"), "127.0.0.1");
            Assert.False(result.Success);
        }

        Assert.Equal(5, user.FailedAttempts);
        Assert.True(user.LockedUntil > DateTime.UtcNow);
        var lockedLogin = await service.LoginAsync(
            new ShopLoginRequest(user.Phone!, "correct-password"), "127.0.0.1");
        Assert.False(lockedLogin.Success);
    }

    [Fact]
    public async Task Customer_registration_and_login_work_without_an_email_address()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        db.Roles.Add(new Role { Name = "customer", IsActive = true });
        await db.SaveChangesAsync();
        const string phone = "+27820000003";
        const string password = "CustomerPassword123";
        var service = new CustomerAuthService(db, ConfigHelper.Build());

        var registration = await service.RegisterAsync(
            new CustomerRegisterRequest(
                phone, "First", "Last", password, null, []),
            "127.0.0.1");

        Assert.True(registration.Success);
        Assert.Equal(string.Empty, registration.Data?.Email);
        var user = Assert.Single(db.Users);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
        Assert.Null(user.Email);

        var login = await service.LoginAsync(
            new CustomerLoginRequest(phone, password), "127.0.0.1");

        Assert.True(login.Success);
        Assert.Equal(phone, login.Data?.Phone);
    }

    [Fact]
    public async Task Customer_login_rejects_a_wrong_password()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var role = new Role { Name = "customer", IsActive = true };
        db.Roles.Add(role);
        db.Users.Add(new User
        {
            Phone = "+27820000004",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password"),
            Role = role,
            Status = "active"
        });
        await db.SaveChangesAsync();
        var service = new CustomerAuthService(db, ConfigHelper.Build());

        var login = await service.LoginAsync(
            new CustomerLoginRequest("+27820000004", "wrong-password"), "127.0.0.1");

        Assert.False(login.Success);
        Assert.Equal(1, db.Users.Single().FailedAttempts);
    }
}
