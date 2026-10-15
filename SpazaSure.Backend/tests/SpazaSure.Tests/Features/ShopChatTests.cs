using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class ShopChatTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");

    private static ControllerContext As(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")),
        },
    };

    private static JsonElement Data(IActionResult result) =>
        JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("data");

    private static (SpazaSureDbContext Db, SpazaShop Shop, Guid CustomerId, Guid OwnerId) Create()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var ownerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var shop = new SpazaShop { UserId = ownerId, ShopName = "Corner Spaza", OwnerName = "Thandi", Phone = "0820001000", Address = "1 St", Status = "verified" };
        db.SpazaShops.Add(shop);
        db.CustomerProfiles.Add(new CustomerProfile { UserId = customerId, FirstName = "Lerato", LastName = "M" });
        db.SaveChanges();
        return (db, shop, customerId, ownerId);
    }

    [Fact]
    public async Task CustomerAndShop_CanChat_WithUnreadCounts()
    {
        var (db, shop, customerId, ownerId) = Create();
        var customer = new CustomerChatController(db, Events) { ControllerContext = As(customerId) };
        var owner = new ShopChatController(db, Events) { ControllerContext = As(ownerId) };

        var conversationId = Data(await customer.Start(new StartChatRequest(shop.Id, "Do you have bread today?"))).GetProperty("id").GetGuid();

        // The shop sees one unread chat from Lerato
        var shopList = Data(await owner.List());
        shopList.GetArrayLength().Should().Be(1);
        shopList[0].GetProperty("customerName").GetString().Should().Be("Lerato");
        shopList[0].GetProperty("unread").GetInt32().Should().Be(1);

        // Opening it marks it read; the shop replies
        Data(await owner.Messages(conversationId, null)).GetProperty("messages").GetArrayLength().Should().Be(1);
        Data(await owner.UnreadCount()).GetProperty("count").GetInt32().Should().Be(0);
        (await owner.Send(conversationId, new SendChatMessageRequest("Yes, fresh this morning"))).Should().BeOfType<OkObjectResult>();

        // The customer sees the reply, newest last
        Data(await customer.UnreadCount()).GetProperty("count").GetInt32().Should().Be(1);
        var messages = Data(await customer.Messages(conversationId, null)).GetProperty("messages");
        messages.GetArrayLength().Should().Be(2);
        messages[1].GetProperty("senderRole").GetString().Should().Be("shop");
        Data(await customer.List())[0].GetProperty("lastMessagePreview").GetString().Should().Be("Yes, fresh this morning");
    }

    [Fact]
    public async Task StartingTwice_ReusesTheSameChat()
    {
        var (db, shop, customerId, _) = Create();
        var customer = new CustomerChatController(db, Events) { ControllerContext = As(customerId) };

        var first = Data(await customer.Start(new StartChatRequest(shop.Id, null))).GetProperty("id").GetGuid();
        var second = Data(await customer.Start(new StartChatRequest(shop.Id, "Hello"))).GetProperty("id").GetGuid();

        second.Should().Be(first);
        db.ShopConversations.Count().Should().Be(1);
    }

    [Fact]
    public async Task OtherCustomers_CannotReadSomeoneElsesChat()
    {
        var (db, shop, customerId, _) = Create();
        var customer = new CustomerChatController(db, Events) { ControllerContext = As(customerId) };
        var id = Data(await customer.Start(new StartChatRequest(shop.Id, "Private question"))).GetProperty("id").GetGuid();

        var stranger = new CustomerChatController(db, Events) { ControllerContext = As(Guid.NewGuid()) };
        (await stranger.Messages(id, null)).Should().BeOfType<NotFoundObjectResult>();
        (await stranger.Send(id, new SendChatMessageRequest("hi"))).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SuspendedShop_CannotBeMessaged_AndEmptyMessagesAreRejected()
    {
        var (db, shop, customerId, _) = Create();
        var customer = new CustomerChatController(db, Events) { ControllerContext = As(customerId) };
        var id = Data(await customer.Start(new StartChatRequest(shop.Id, null))).GetProperty("id").GetGuid();

        (await customer.Send(id, new SendChatMessageRequest("   "))).Should().BeOfType<BadRequestObjectResult>();

        shop.Status = "suspended";
        await db.SaveChangesAsync();
        (await customer.Send(id, new SendChatMessageRequest("Hello?"))).Should().BeOfType<BadRequestObjectResult>();
    }
}
