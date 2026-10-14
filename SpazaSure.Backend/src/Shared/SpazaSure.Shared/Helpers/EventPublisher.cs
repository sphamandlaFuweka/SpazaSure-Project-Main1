using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// Publishes events to RabbitMQ for the NotificationService to consume.
/// Register as a singleton via DI: builder.Services.AddSingleton&lt;EventPublisher&gt;();
/// Connects lazily and reconnects on its own, so a service that starts before RabbitMQ
/// is ready (or loses its connection later) still delivers notifications afterwards.
/// </summary>
public class EventPublisher : IDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly object _lock = new();
    private IConnection? _connection;
    private IModel? _channel;
    private DateTime _nextConnectAttempt = DateTime.MinValue;
    private const string ExchangeName = "spazasure.events";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    public EventPublisher(string host = "localhost", string username = "spazasure", string password = "guest")
    {
        _factory = new ConnectionFactory
        {
            HostName = host,
            UserName = username,
            Password = password,
            AutomaticRecoveryEnabled = true,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
        };
        lock (_lock) EnsureChannel();
    }

    /// <summary>Opens the connection if needed. Must be called while holding _lock.</summary>
    private bool EnsureChannel()
    {
        if (_channel is { IsOpen: true }) return true;
        if (DateTime.UtcNow < _nextConnectAttempt) return false;
        try
        {
            _channel?.Dispose();
            _connection?.Dispose();
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
            return true;
        }
        catch
        {
            // RabbitMQ unavailable: try again on a later publish rather than on every call
            _channel = null;
            _connection = null;
            _nextConnectAttempt = DateTime.UtcNow + RetryDelay;
            return false;
        }
    }

    /// <summary>
    /// Publish a notification event that will be picked up by the NotificationService
    /// </summary>
    /// <param name="supplierId">The recipient's user ID (supplier, shop owner or admin)</param>
    /// <param name="type">Notification type: order, product, compliance, wallet, payout, account, system</param>
    /// <param name="title">Short title</param>
    /// <param name="message">Notification message body</param>
    /// <param name="priority">normal or high</param>
    /// <param name="referenceId">Optional reference ID (orderId, productId, etc.)</param>
    /// <param name="routingKey">RabbitMQ routing key, e.g. "order.placed", "product.approved"</param>
    public void PublishNotification(
        string supplierId,
        string type,
        string title,
        string message,
        string priority = "normal",
        string? referenceId = null,
        string routingKey = "notification.general")
    {
        if (string.IsNullOrWhiteSpace(supplierId)) return;

        var evt = new
        {
            SupplierId = supplierId,
            Type = type,
            Title = title,
            Message = message.Length > 1000 ? message[..1000] : message,
            Priority = priority,
            ReferenceId = referenceId,
        };
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));

        // IModel isn't thread-safe and this publisher is a singleton shared by all requests
        lock (_lock)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (!EnsureChannel()) return;
                try
                {
                    var props = _channel!.CreateBasicProperties();
                    props.Persistent = true;
                    props.ContentType = "application/json";
                    _channel.BasicPublish(ExchangeName, routingKey, props, body);
                    return;
                }
                catch
                {
                    // Stale channel: drop it and retry once on a fresh connection
                    _channel = null;
                    _nextConnectAttempt = DateTime.MinValue;
                }
            }
        }
    }

    /// <summary>Sends the same notification to several users (e.g. every admin).</summary>
    public void PublishNotificationToMany(
        IEnumerable<string> userIds,
        string type,
        string title,
        string message,
        string priority = "normal",
        string? referenceId = null,
        string routingKey = "notification.general")
    {
        foreach (var userId in userIds.Distinct())
            PublishNotification(userId, type, title, message, priority, referenceId, routingKey);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            try { _channel?.Close(); } catch { }
            try { _connection?.Close(); } catch { }
        }
        GC.SuppressFinalize(this);
    }
}
