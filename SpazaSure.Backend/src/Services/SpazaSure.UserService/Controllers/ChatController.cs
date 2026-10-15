using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

public record StartChatRequest(Guid ShopId, string? Message);
public record SendChatMessageRequest(string? Body);

/// <summary>Rules shared by both sides of a customer-shop chat.</summary>
internal static class Chat
{
    public const int MaxLength = 1000;

    public static string? Clean(string? body)
    {
        var text = body?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > MaxLength ? text[..MaxLength] : text;
    }

    public static string Preview(string text) => text.Length > 120 ? text[..117] + "..." : text;

    public static object ToDto(ShopMessage m) => new { m.Id, m.SenderRole, m.Body, SentAt = m.CreatedAt };

    /// <summary>Messages, oldest first. Pass the last id you have to get only newer ones.</summary>
    public static async Task<List<object>> MessagesAsync(SpazaSureDbContext db, Guid conversationId, DateTime? after)
    {
        var query = db.ShopMessages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (after is not null) query = query.Where(m => m.CreatedAt > after);
        var items = await query.OrderByDescending(m => m.CreatedAt).Take(100).ToListAsync();
        items.Reverse();
        return items.Select(ToDto).ToList();
    }

    public static ShopMessage Add(SpazaSureDbContext db, ShopConversation conversation, Guid senderId, string role, string text)
    {
        var message = new ShopMessage { ConversationId = conversation.Id, SenderUserId = senderId, SenderRole = role, Body = text };
        db.ShopMessages.Add(message);
        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessagePreview = Preview(text);
        if (role == "customer") conversation.ShopUnread++;
        else conversation.CustomerUnread++;
        return message;
    }
}

/// <summary>Customers chatting with spaza shops in the app.</summary>
[ApiController]
[Route("api/customer/chats")]
[Authorize]
public class CustomerChatController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var items = await db.ShopConversations.AsNoTracking()
            .Where(c => c.CustomerUserId == UserId)
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new
            {
                c.Id, c.ShopId, ShopName = c.Shop.ShopName, ShopNumber = c.Shop.ShopCode,
                ShopVerified = c.Shop.Status == "verified", c.Shop.City,
                c.LastMessageAt, c.LastMessagePreview, Unread = c.CustomerUnread,
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount() =>
        Ok(ApiResponse<object>.Ok(new
        {
            count = await db.ShopConversations.Where(c => c.CustomerUserId == UserId).SumAsync(c => (int?)c.CustomerUnread) ?? 0,
        }));

    /// <summary>Opens the chat with a shop (creating it the first time), optionally with a first message.</summary>
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartChatRequest req)
    {
        var shop = await db.SpazaShops.FirstOrDefaultAsync(s => s.Id == req.ShopId);
        if (shop is null || shop.Status is "suspended" or "rejected" or "deleted")
            return NotFound(ApiResponse.Fail("This shop can't receive messages right now."));
        if (shop.UserId == UserId) return BadRequest(ApiResponse.Fail("You can't message your own shop."));

        var conversation = await db.ShopConversations.FirstOrDefaultAsync(c => c.ShopId == shop.Id && c.CustomerUserId == UserId);
        if (conversation is null)
        {
            conversation = new ShopConversation { ShopId = shop.Id, CustomerUserId = UserId };
            db.ShopConversations.Add(conversation);
        }
        var text = Chat.Clean(req.Message);
        if (text is not null) Chat.Add(db, conversation, UserId, "customer", text);
        await db.SaveChangesAsync();
        if (text is not null) await NotifyShopAsync(shop, text, conversation.Id);

        return Ok(ApiResponse<object>.Ok(new { conversation.Id, shop.ShopName, ShopId = shop.Id }));
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, [FromQuery] DateTime? after)
    {
        var conversation = await db.ShopConversations.Include(c => c.Shop)
            .FirstOrDefaultAsync(c => c.Id == id && c.CustomerUserId == UserId);
        if (conversation is null) return NotFound(ApiResponse.Fail("Chat not found."));
        if (conversation.CustomerUnread > 0)
        {
            conversation.CustomerUnread = 0;
            await db.SaveChangesAsync();
        }
        return Ok(ApiResponse<object>.Ok(new
        {
            conversation.Id, conversation.ShopId, conversation.Shop.ShopName,
            ShopVerified = conversation.Shop.Status == "verified",
            Messages = await Chat.MessagesAsync(db, id, after),
        }));
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> Send(Guid id, [FromBody] SendChatMessageRequest req)
    {
        var text = Chat.Clean(req.Body);
        if (text is null) return BadRequest(ApiResponse.Fail("Type a message first."));
        var conversation = await db.ShopConversations.Include(c => c.Shop)
            .FirstOrDefaultAsync(c => c.Id == id && c.CustomerUserId == UserId);
        if (conversation is null) return NotFound(ApiResponse.Fail("Chat not found."));
        if (conversation.Shop.Status is "suspended" or "rejected" or "deleted")
            return BadRequest(ApiResponse.Fail("This shop can't receive messages right now."));

        var message = Chat.Add(db, conversation, UserId, "customer", text);
        await db.SaveChangesAsync();
        await NotifyShopAsync(conversation.Shop, text, conversation.Id);
        return Ok(ApiResponse<object>.Ok(Chat.ToDto(message)));
    }

    private async Task NotifyShopAsync(SpazaShop shop, string text, Guid conversationId)
    {
        var customer = await db.CustomerProfiles.Where(p => p.UserId == UserId)
            .Select(p => p.PreferredName ?? p.FirstName).FirstOrDefaultAsync();
        events.PublishNotification(shop.UserId.ToString(), "chat",
            $"Message from {(string.IsNullOrWhiteSpace(customer) ? "a customer" : customer)}",
            Chat.Preview(text), referenceId: conversationId.ToString(), routingKey: "notification.chat");
    }
}

/// <summary>Spaza shop owners reading and answering customer chats.</summary>
[ApiController]
[Route("api/shop/chats")]
[Authorize(Roles = "spaza_owner")]
public class ShopChatController(SpazaSureDbContext db, EventPublisher events) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Task<SpazaShop?> MyShopAsync() => db.SpazaShops.FirstOrDefaultAsync(s => s.UserId == UserId);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var shop = await MyShopAsync();
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var items = await db.ShopConversations.AsNoTracking()
            .Where(c => c.ShopId == shop.Id)
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new
            {
                c.Id,
                CustomerName = db.CustomerProfiles.Where(p => p.UserId == c.CustomerUserId)
                    .Select(p => p.PreferredName ?? p.FirstName).FirstOrDefault() ?? "Customer",
                c.LastMessageAt, c.LastMessagePreview, Unread = c.ShopUnread,
            })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var shop = await MyShopAsync();
        var count = shop is null ? 0 : await db.ShopConversations.Where(c => c.ShopId == shop.Id).SumAsync(c => (int?)c.ShopUnread) ?? 0;
        return Ok(ApiResponse<object>.Ok(new { count }));
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, [FromQuery] DateTime? after)
    {
        var shop = await MyShopAsync();
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var conversation = await db.ShopConversations.FirstOrDefaultAsync(c => c.Id == id && c.ShopId == shop.Id);
        if (conversation is null) return NotFound(ApiResponse.Fail("Chat not found."));
        if (conversation.ShopUnread > 0)
        {
            conversation.ShopUnread = 0;
            await db.SaveChangesAsync();
        }
        var customer = await db.CustomerProfiles.Where(p => p.UserId == conversation.CustomerUserId)
            .Select(p => p.PreferredName ?? p.FirstName).FirstOrDefaultAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            conversation.Id, CustomerName = customer ?? "Customer",
            Messages = await Chat.MessagesAsync(db, id, after),
        }));
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> Send(Guid id, [FromBody] SendChatMessageRequest req)
    {
        var text = Chat.Clean(req.Body);
        if (text is null) return BadRequest(ApiResponse.Fail("Type a message first."));
        var shop = await MyShopAsync();
        if (shop is null) return NotFound(ApiResponse.Fail("Shop not found."));
        var conversation = await db.ShopConversations.FirstOrDefaultAsync(c => c.Id == id && c.ShopId == shop.Id);
        if (conversation is null) return NotFound(ApiResponse.Fail("Chat not found."));

        var message = Chat.Add(db, conversation, UserId, "shop", text);
        await db.SaveChangesAsync();
        events.PublishNotification(conversation.CustomerUserId.ToString(), "chat", $"Message from {shop.ShopName}",
            Chat.Preview(text), referenceId: conversation.Id.ToString(), routingKey: "notification.chat");
        return Ok(ApiResponse<object>.Ok(Chat.ToDto(message)));
    }
}
