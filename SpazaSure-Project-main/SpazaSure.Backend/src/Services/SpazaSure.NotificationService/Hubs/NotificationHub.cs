using Microsoft.AspNetCore.SignalR;

namespace SpazaSure.NotificationService.Hubs;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var supplierId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(supplierId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, supplierId);
            _logger.LogInformation("Supplier {SupplierId} connected to notifications hub", supplierId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var supplierId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(supplierId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, supplierId);
            _logger.LogInformation("Supplier {SupplierId} disconnected from notifications hub", supplierId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
