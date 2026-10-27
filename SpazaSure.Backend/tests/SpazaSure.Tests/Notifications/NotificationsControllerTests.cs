using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.NotificationService.Controllers;
using SpazaSure.NotificationService.Data;
using Xunit;
using Notification = SpazaSure.NotificationService.Models.Notification;

namespace SpazaSure.Tests.Notifications;

public class NotificationsControllerTests
{
    private const string Me = "user-me";
    private const string SomeoneElse = "user-other";

    private static NotificationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ControllerContext SignedInAs(string? userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                userId is null ? [] : [new Claim(ClaimTypes.NameIdentifier, userId)], "Test")),
        },
    };

    private static JsonElement Body(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static Notification Add(NotificationDbContext db, string userId, bool isRead = false, int minutesAgo = 0)
    {
        var n = new Notification
        {
            SupplierId = userId, Type = "order", Title = "Order update", Message = "Your order moved",
            IsRead = isRead, CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo),
        };
        db.Notifications.Add(n);
        db.SaveChanges();
        return n;
    }

    [Fact]
    public async Task Supplier_GetAll_ShowsOnlyMyNotifications_NewestFirst()
    {
        var db = CreateDb();
        var older = Add(db, Me, minutesAgo: 10);
        var newer = Add(db, Me, minutesAgo: 1);
        Add(db, SomeoneElse);
        var ctrl = new NotificationsController(db) { ControllerContext = SignedInAs(Me) };

        var body = Body(await ctrl.GetAll());

        body.GetProperty("total").GetInt32().Should().Be(2);
        body.GetProperty("data").EnumerateArray().Select(n => n.GetProperty("id").GetGuid())
            .Should().Equal(newer.Id, older.Id);
    }

    [Fact]
    public async Task GetAll_CapsThePageSize()
    {
        var db = CreateDb();
        for (var i = 0; i < 120; i++) Add(db, Me, minutesAgo: i);
        var ctrl = new ShopNotificationsController(db) { ControllerContext = SignedInAs(Me) };

        var body = Body(await ctrl.GetAll(page: 1, pageSize: 1000));

        body.GetProperty("data").GetArrayLength().Should().Be(100);
        body.GetProperty("total").GetInt32().Should().Be(120);
    }

    [Fact]
    public async Task GetAll_RejectsATokenWithoutAUserId()
    {
        var ctrl = new ShopNotificationsController(CreateDb()) { ControllerContext = SignedInAs(null) };

        (await ctrl.GetAll()).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Shop_UnreadCount_CountsOnlyMyUnread()
    {
        var db = CreateDb();
        Add(db, Me);
        Add(db, Me);
        Add(db, Me, isRead: true);
        Add(db, SomeoneElse);
        var ctrl = new ShopNotificationsController(db) { ControllerContext = SignedInAs(Me) };

        var body = Body(await ctrl.GetUnreadCount());

        body.GetProperty("data").GetProperty("count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task MarkAsRead_UpdatesMyNotification()
    {
        var db = CreateDb();
        var mine = Add(db, Me);
        var ctrl = new ShopNotificationsController(db) { ControllerContext = SignedInAs(Me) };

        (await ctrl.MarkAsRead(mine.Id)).Should().BeOfType<OkObjectResult>();

        (await db.Notifications.FindAsync(mine.Id))!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsRead_CannotTouchSomeoneElsesNotification()
    {
        var db = CreateDb();
        var theirs = Add(db, SomeoneElse);
        var ctrl = new NotificationsController(db) { ControllerContext = SignedInAs(Me) };

        (await ctrl.MarkAsRead(theirs.Id)).Should().BeOfType<NotFoundResult>();

        (await db.Notifications.FindAsync(theirs.Id))!.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterDevice_RejectsInvalidTokens()
    {
        var ctrl = new ShopNotificationsController(CreateDb()) { ControllerContext = SignedInAs(Me) };

        (await ctrl.RegisterDevice(new RegisterDeviceRequest("short", "android"))).Should().BeOfType<BadRequestObjectResult>();
        (await ctrl.RegisterDevice(new RegisterDeviceRequest(new string('x', 513), "ios"))).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RegisterDevice_MovesAPhoneToWhoeverSignedInLast()
    {
        var db = CreateDb();
        var token = new string('t', 40);
        await new ShopNotificationsController(db) { ControllerContext = SignedInAs(SomeoneElse) }
            .RegisterDevice(new RegisterDeviceRequest(token, "IOS"));

        await new ShopNotificationsController(db) { ControllerContext = SignedInAs(Me) }
            .RegisterDevice(new RegisterDeviceRequest($"  {token}  ", "android"));

        var device = await db.DeviceTokens.SingleAsync();
        device.UserId.Should().Be(Me);
        device.Token.Should().Be(token);
        device.Platform.Should().Be("android");
    }

    [Fact]
    public async Task RegisterDevice_TreatsUnknownPlatformsAsAndroid()
    {
        var db = CreateDb();
        await new ShopNotificationsController(db) { ControllerContext = SignedInAs(Me) }
            .RegisterDevice(new RegisterDeviceRequest(new string('a', 40), "windows-phone"));

        (await db.DeviceTokens.SingleAsync()).Platform.Should().Be("android");
    }
}
