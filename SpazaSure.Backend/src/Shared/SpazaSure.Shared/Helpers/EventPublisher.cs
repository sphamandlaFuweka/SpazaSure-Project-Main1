using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// Publishes events to RabbitMQ for the NotificationService to consume.
/// Register as a singleton via DI: builder.Services.AddSingleton&lt;EventPublisher&gt;();
/// </summary>
public class EventPublisher : IDisposable
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private const string ExchangeName = "spazasure.events";

    public EventPublisher(string host = "localhost", string username = "spazasure", string password = "guest")
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = host,
                UserName = username,
                Password = password,
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
        }
        catch
        {
            // If RabbitMQ is unavailable, notifications just won't be sent
            // The service continues to work without them
        }
    }

    /// <summary>
    /// Publish a notification event that will be picked up by the NotificationService
    /// </summary>
    /// <param name="supplierId">The target supplier's user ID (or shop userId for shop notifications)</param>
    /// <param name="type">Notification type: order, product, compliance, system</param>
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
        if (_channel == null) return;

        try
        {
            var evt = new
            {
                SupplierId = supplierId,
                Type = type,
                Title = title,
                Message = message,
                Priority = priority,
                ReferenceId = referenceId,
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));
            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                basicProperties: props,
                body: body);
        }
        catch
        {
            // Silently fail — notifications are non-critical
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
