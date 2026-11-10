using FluentAssertions;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Auth;

public class AuthenticationServiceTests
{
    private static AuthenticationService CreateService(
        out SpazaSure.Infrastructure.Data.SpazaSureDbContext db,
        Dictionary<string, string?>? configOverrides = null)
    {
        db = DbContextHelper.CreateInMemoryDb();
        var config = ConfigHelper.Build(configOverrides);
        return new AuthenticationService(db, config);
    }

    [Fact]
    public async Task Register_WithValidSupplierData_ReturnsSuccessAndTokens()
    {
        var svc = CreateService(out _);
        var req = new RegisterRequest(
            Role: "supplier", Email: "supplier@test.com", Phone: "0821234567",
            Password: "Password123!", CompanyName: "Test Co", ContactPerson: "Jane Doe",
            ShopName: null, OwnerName: null, Address: null, AcceptedPrivacyNotice: true);

        var (success, error, data) = await svc.RegisterAsync(req, "127.0.0.1");

        success.Should().BeTrue();
        error.Should().BeNull();
        data!.AccessToken.Should().NotBeNullOrEmpty();
        data.Role.Should().Be("supplier");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsDuplicateError()
    {
        var svc = CreateService(out _);
        var req = new RegisterRequest("supplier", "dup@test.com", "0821111111", "Password123!",
            "Co A", "Jane", null, null, null, AcceptedPrivacyNotice: true);
        await svc.RegisterAsync(req, "127.0.0.1");

        var (success, error, _) = await svc.RegisterAsync(req with { Phone = "0822222222" }, "127.0.0.1");

        success.Should().BeFalse();
        error.Should().Contain("already registered");
    }

    [Fact]
    public async Task Register_WithInvalidRole_ReturnsRoleError()
    {
        var svc = CreateService(out _);
        var req = new RegisterRequest("bad_role", "x@test.com", "0829999999",
            "Password123!", null, null, null, null, null, AcceptedPrivacyNotice: true);

        var (success, error, _) = await svc.RegisterAsync(req, "127.0.0.1");

        success.Should().BeFalse();
        error.Should().Contain("can't be registered");
    }

    [Fact]
    public async Task Register_SpazaOwner_CreatesShopRecord()
    {
        var svc = CreateService(out var db);
        var req = new RegisterRequest("spaza_owner", "owner@test.com", "0823334444",
            "Password123!", null, null, "Dudu Shop", "Dudu Nkosi", "123 Main St", AcceptedPrivacyNotice: true);

        await svc.RegisterAsync(req, "127.0.0.1");

        db.SpazaShops.Should().Contain(s => s.ShopName == "Dudu Shop");
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        var svc = CreateService(out _);
        await svc.RegisterAsync(new RegisterRequest("supplier", "login@test.com",
            "0820000001", "Password123!", "X Co", "Bob", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");

        var (success, error, data) = await svc.LoginAsync(
            new LoginRequest("login@test.com", null, "Password123!"), "127.0.0.1");

        success.Should().BeTrue();
        error.Should().BeNull();
        data!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_AdminAccount_IsRefusedByTheSupplierPortal()
    {
        var svc = CreateService(out var db);
        var adminRole = db.Roles.First(r => r.Name == "admin");
        db.Users.Add(new SpazaSure.Infrastructure.Entities.User
        {
            Email = "boss@test.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            RoleId = adminRole.Id, Status = "active"
        });
        await db.SaveChangesAsync();

        var viaSupplier = await svc.LoginAsync(new LoginRequest("boss@test.com", null, "Password123!", "supplier"), "127.0.0.1");
        var viaAdmin = await svc.LoginAsync(new LoginRequest("boss@test.com", null, "Password123!", "admin"), "127.0.0.1");

        viaSupplier.Success.Should().BeFalse();
        viaSupplier.Error.Should().Be("Invalid credentials.");
        viaAdmin.Success.Should().BeTrue();
        viaAdmin.Data!.Role.Should().Be("admin");
    }

    [Fact]
    public async Task Login_SupplierAccount_IsRefusedByTheAdminConsole()
    {
        var svc = CreateService(out _);
        await svc.RegisterAsync(new RegisterRequest("supplier", "sup@test.com",
            "0820000003", "Password123!", "Z Co", "Sam", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");

        var viaAdmin = await svc.LoginAsync(new LoginRequest("sup@test.com", null, "Password123!", "admin"), "127.0.0.1");
        var viaSupplier = await svc.LoginAsync(new LoginRequest("sup@test.com", null, "Password123!", "supplier"), "127.0.0.1");

        viaAdmin.Success.Should().BeFalse();
        viaAdmin.Error.Should().Be("Invalid credentials.");
        viaSupplier.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsInvalidCredentials()
    {
        var svc = CreateService(out _);
        await svc.RegisterAsync(new RegisterRequest("supplier", "wrong@test.com",
            "0820000002", "Password123!", "Y Co", "Alice", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");

        var (success, error, _) = await svc.LoginAsync(
            new LoginRequest("wrong@test.com", null, "WrongPass!"), "127.0.0.1");

        success.Should().BeFalse();
        error.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ReturnsInvalidCredentials()
    {
        var svc = CreateService(out _);

        var (success, _, _) = await svc.LoginAsync(
            new LoginRequest("ghost@test.com", null, "Password123!"), "127.0.0.1");

        success.Should().BeFalse();
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_AccountIsLocked()
    {
        var svc = CreateService(out _);
        await svc.RegisterAsync(new RegisterRequest("supplier", "lockme@test.com",
            "0820000003", "Password123!", "Lock Co", "Bob", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");

        for (int i = 0; i < 5; i++)
            await svc.LoginAsync(new LoginRequest("lockme@test.com", null, "BadPass!"), "127.0.0.1");

        var (success, error, _) = await svc.LoginAsync(
            new LoginRequest("lockme@test.com", null, "Password123!"), "127.0.0.1");

        success.Should().BeFalse();
        error.Should().Contain("locked");
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokenPair()
    {
        var svc = CreateService(out _);
        await svc.RegisterAsync(new RegisterRequest("supplier", "refresh@test.com",
            "0820000004", "Password123!", "R Co", "Carol", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");

        var (_, _, loginData) = await svc.LoginAsync(
            new LoginRequest("refresh@test.com", null, "Password123!"), "127.0.0.1");

        var (success, _, newData) = await svc.RefreshAsync(loginData!.RefreshToken, "127.0.0.1");

        success.Should().BeTrue();
        newData!.AccessToken.Should().NotBe(loginData.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReplayingRotatedToken_RevokesEverySession()
    {
        var svc = CreateService(out var db);
        await svc.RegisterAsync(new RegisterRequest("supplier", "reuse@test.com",
            "0820000014", "Password123!", "R Co", "Carol", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");
        var (_, _, loginData) = await svc.LoginAsync(
            new LoginRequest("reuse@test.com", null, "Password123!"), "127.0.0.1");
        var (_, _, rotated) = await svc.RefreshAsync(loginData!.RefreshToken, "127.0.0.1");

        // Pretend the old token was rotated out a while ago, then gets replayed by a thief
        var oldHash = SpazaSure.Shared.Helpers.JwtHelper.HashToken(loginData.RefreshToken);
        db.RefreshTokens.Single(r => r.TokenHash == oldHash).RevokedAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var (replayed, _, _) = await svc.RefreshAsync(loginData.RefreshToken, "10.0.0.9");
        var (newerStillWorks, _, _) = await svc.RefreshAsync(rotated!.RefreshToken, "127.0.0.1");

        replayed.Should().BeFalse();
        newerStillWorks.Should().BeFalse();
        db.AuthAuditLogs.Should().Contain(l => l.Event == "refresh_token_reuse");
    }

    [Fact]
    public async Task Refresh_ConcurrentDoubleRefresh_DoesNotRevokeSessions()
    {
        var svc = CreateService(out _);
        await svc.RegisterAsync(new RegisterRequest("supplier", "double@test.com",
            "0820000015", "Password123!", "R Co", "Carol", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");
        var (_, _, loginData) = await svc.LoginAsync(
            new LoginRequest("double@test.com", null, "Password123!"), "127.0.0.1");

        var (_, _, first) = await svc.RefreshAsync(loginData!.RefreshToken, "127.0.0.1");
        var (second, _, _) = await svc.RefreshAsync(loginData.RefreshToken, "127.0.0.1");
        var (firstStillWorks, _, _) = await svc.RefreshAsync(first!.RefreshToken, "127.0.0.1");

        second.Should().BeFalse();
        firstStillWorks.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsError()
    {
        var svc = CreateService(out _);

        var (success, error, _) = await svc.RefreshAsync("invalid-token-xyz", "127.0.0.1");

        success.Should().BeFalse();
        error.Should().Contain("Invalid");
    }

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ReturnsTrue()
    {
        var svc = CreateService(out _);
        await svc.RegisterAsync(new RegisterRequest("supplier", "reset@test.com",
            "0820000005", "Password123!", "R2 Co", "Dave", null, null, null, AcceptedPrivacyNotice: true), "127.0.0.1");

        var result = await svc.ForgotPasswordAsync("reset@test.com");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_StillReturnsTrue()
    {
        var svc = CreateService(out _);

        var result = await svc.ForgotPasswordAsync("nobody@test.com");

        result.Should().BeTrue();
    }
}