using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Auth;

public class SuspendedAccountTests
{
    private static async Task<(AuthenticationService Svc, SpazaSure.Infrastructure.Data.SpazaSureDbContext Db, AuthResponse Tokens)> RegisterSupplierAsync(string email)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var svc = new AuthenticationService(db, ConfigHelper.Build());
        var (_, _, data) = await svc.RegisterAsync(new RegisterRequest("supplier", email, "0820000077",
            "Password123!", "Suspend Co", "Sipho", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");
        return (svc, db, data!);
    }

    private static async Task SetSupplierStatusAsync(SpazaSure.Infrastructure.Data.SpazaSureDbContext db, string status)
    {
        var supplier = await db.Suppliers.SingleAsync();
        supplier.Status = status;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_SuspendedSupplier_IsRejected()
    {
        var (svc, db, _) = await RegisterSupplierAsync("susp@test.com");
        await SetSupplierStatusAsync(db, "suspended");

        var (success, error, data) = await svc.LoginAsync(new LoginRequest("susp@test.com", null, "Password123!"), "127.0.0.1");

        success.Should().BeFalse();
        error.Should().Be(AuthenticationService.SuspendedMessage);
        data.Should().BeNull();
    }

    [Fact]
    public async Task Login_SuspendedSupplierWithWrongPassword_DoesNotRevealSuspension()
    {
        var (svc, db, _) = await RegisterSupplierAsync("susp2@test.com");
        await SetSupplierStatusAsync(db, "suspended");

        var (_, error, _) = await svc.LoginAsync(new LoginRequest("susp2@test.com", null, "WrongPass!"), "127.0.0.1");

        error.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task Refresh_AfterSuspension_IsRejected()
    {
        var (svc, db, tokens) = await RegisterSupplierAsync("susp3@test.com");
        await SetSupplierStatusAsync(db, "suspended");

        var (success, error, _) = await svc.RefreshAsync(tokens.RefreshToken, "127.0.0.1");

        success.Should().BeFalse();
        error.Should().Be(AuthenticationService.SuspendedMessage);
    }

    [Fact]
    public async Task Login_AfterReinstating_Succeeds()
    {
        var (svc, db, _) = await RegisterSupplierAsync("susp4@test.com");
        await SetSupplierStatusAsync(db, "suspended");
        await SetSupplierStatusAsync(db, "verified");

        var (success, _, _) = await svc.LoginAsync(new LoginRequest("susp4@test.com", null, "Password123!"), "127.0.0.1");

        success.Should().BeTrue();
    }
}
