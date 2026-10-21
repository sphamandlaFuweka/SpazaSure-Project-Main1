using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Storage;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class PrivacyRightsTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");

    private sealed class FakeStorage : IFileStorageService
    {
        public List<string> Deleted { get; } = [];
        public Task<string> SaveAsync(Stream content, string key, string contentType, CancellationToken ct = default) =>
            Task.FromResult("/uploads/" + key);
        public Task DeleteAsync(string urlOrKey, CancellationToken ct = default)
        {
            Deleted.Add(urlOrKey);
            return Task.CompletedTask;
        }
    }

    private static T SignedIn<T>(T controller, Guid userId, string role) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "Test")),
            },
        };
        return controller;
    }

    private static async Task<(User User, SpazaShop Shop)> AddShopAsync(SpazaSureDbContext db)
    {
        var role = await db.Roles.SingleAsync(r => r.Name == "spaza_owner");
        var user = new User { Phone = "+27820000077", Email = "thandi@example.co.za", PasswordHash = "hash", RoleId = role.Id, Status = "active" };
        var shop = new SpazaShop
        {
            UserId = user.Id, ShopName = "Mama Thandi's Spaza", OwnerName = "Thandi Mokoena", OwnerIdNumber = "8001015009087",
            Phone = "+27820000077", Address = "9 Vilakazi St", City = "Soweto", Province = "Gauteng", ShopCode = "SS123456",
            Latitude = -26.2, Longitude = 27.9, Status = "verified", IdentityCheckStatus = "Approved",
        };
        db.Users.Add(user);
        db.SpazaShops.Add(shop);
        db.ShopDocuments.Add(new ShopDocument { ShopId = shop.Id, DocType = "business_permit", DocUrl = "/uploads/shop-docs/x/permit.pdf", Status = "approved" });
        await db.SaveChangesAsync();
        return (user, shop);
    }

    [Fact]
    public async Task Registration_WithoutAcceptingThePrivacyNotice_IsRefused_AndAcceptingIsRecorded()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var service = new ShopAuthService(db, ConfigHelper.Build());

        var refused = await service.RegisterAsync(
            new ShopRegisterRequest("+27820000001", "StrongPassword123", "Owner", "Shop", "1 Main St", "8001015009087"), "127.0.0.1");
        refused.Success.Should().BeFalse();
        refused.Error.Should().Be(PrivacyNotice.NotAcceptedMessage);

        var accepted = await service.RegisterAsync(
            new ShopRegisterRequest("+27820000001", "StrongPassword123", "Owner", "Shop", "1 Main St", "8001015009087",
                AcceptedPrivacyNotice: true), "127.0.0.1");
        accepted.Success.Should().BeTrue();
        db.AuthAuditLogs.Should().ContainSingle(a => a.Event == $"consent_privacy_notice:{PrivacyNotice.Version}" && a.IpAddress == "127.0.0.1");
    }

    [Fact]
    public async Task MyData_ContainsTheShopsPersonalInformation_WithSignedDocumentLinks()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        PrivateFiles.Configure("unit-test-jwt-secret");
        var (user, _) = await AddShopAsync(db);

        var result = await SignedIn(new AccountPrivacyController(db, Events), user.Id, "spaza_owner").MyData(CancellationToken.None);

        var json = JsonSerializer.Serialize(((OkObjectResult)result).Value);
        json.Should().Contain("8001015009087").And.Contain("Thandi Mokoena").And.Contain("9 Vilakazi St")
            .And.Contain("/uploads/shop-docs/x/permit.pdf?exp=");
        db.AuthAuditLogs.Should().Contain(a => a.UserId == user.Id && a.Event == "data_export");
    }

    [Fact]
    public async Task ApprovedDeletion_ErasesPersonalDetailsAndFiles_ButKeepsOrders()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var storage = new FakeStorage();
        var (user, shop) = await AddShopAsync(db);
        var order = new Order { OrderNumber = "ORD-1", ShopId = shop.Id, Status = "delivered", TotalAmount = 250m, DeliveryAddress = "9 Vilakazi St" };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await SignedIn(new AccountPrivacyController(db, Events), user.Id, "spaza_owner")
            .RequestDeletion(new DeletionRequestBody("Closing my shop"), CancellationToken.None);
        var request = await db.AccountDeletionRequests.SingleAsync();
        var admin = SignedIn(new AdminDeletionRequestsController(db, storage, Events), Guid.NewGuid(), "admin");

        (await admin.Approve(request.Id, CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        var erasedUser = await db.Users.SingleAsync(u => u.Id == user.Id);
        erasedUser.Email.Should().BeNull();
        erasedUser.Phone.Should().StartWith("deleted-");
        erasedUser.Status.Should().Be("deleted");
        var erasedShop = await db.SpazaShops.SingleAsync(s => s.Id == shop.Id);
        erasedShop.OwnerIdNumber.Should().BeNull();
        erasedShop.OwnerName.Should().Be(PersonalData.DeletedName);
        erasedShop.Address.Should().BeEmpty();
        erasedShop.ShopCode.Should().BeNull();
        erasedShop.Status.Should().Be("deleted");
        db.ShopDocuments.Should().BeEmpty();
        storage.Deleted.Should().Contain("/uploads/shop-docs/x/permit.pdf");

        var keptOrder = await db.Orders.SingleAsync();
        keptOrder.TotalAmount.Should().Be(250m);
        keptOrder.DeliveryAddress.Should().BeNull();
        (await db.AccountDeletionRequests.SingleAsync()).Status.Should().Be("approved");
    }

    [Fact]
    public async Task Deletion_WithMoneyInTheWallet_IsBlocked_UntilItIsWithdrawn()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var (user, shop) = await AddShopAsync(db);
        db.ShopWalletLedgerEntries.Add(new ShopWalletLedgerEntry { ShopId = shop.Id, Amount = 80m, Type = "top_up" });
        await db.SaveChangesAsync();
        await SignedIn(new AccountPrivacyController(db, Events), user.Id, "spaza_owner")
            .RequestDeletion(new DeletionRequestBody(null), CancellationToken.None);
        var request = await db.AccountDeletionRequests.SingleAsync();
        var admin = SignedIn(new AdminDeletionRequestsController(db, new FakeStorage(), Events), Guid.NewGuid(), "admin");

        var result = await admin.Approve(request.Id, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        (await db.SpazaShops.SingleAsync()).OwnerIdNumber.Should().Be("8001015009087");
        (await db.AccountDeletionRequests.SingleAsync()).Status.Should().Be("pending");
    }

    [Fact]
    public async Task OnlyOnePendingRequest_AndItCanBeCancelled()
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var (user, _) = await AddShopAsync(db);
        var controller = SignedIn(new AccountPrivacyController(db, Events), user.Id, "spaza_owner");

        (await controller.RequestDeletion(new DeletionRequestBody(null), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.RequestDeletion(new DeletionRequestBody(null), CancellationToken.None)).Should().BeOfType<ConflictObjectResult>();
        (await controller.CancelDeletion(CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        (await db.AccountDeletionRequests.SingleAsync()).Status.Should().Be("cancelled");
    }
}
