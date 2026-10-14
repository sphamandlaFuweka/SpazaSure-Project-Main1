using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SpazaSure.NotificationService.Data;
using SpazaSure.NotificationService.Hubs;
using SpazaSure.NotificationService.Models;

namespace SpazaSure.NotificationService.Consumers;

/// <summary>
/// Stores every notification event published to the spazasure.events exchange and pushes it
/// to the recipient over SignalR. Keeps reconnecting for as long as the service runs, so
/// notifications resume on their own after RabbitMQ restarts.
/// </summary>
public class RabbitMqConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly IConfiguration _config;
    private IConnection? _connection;
    private IModel? _channel;

    private const string QueueName = "notifications";
    private const string ExchangeName = "spazasure.events";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RabbitMqConsumer(
        IServiceScopeFactory scopeFactory,
        IHubContext<NotificationHub> hubContext,
        ILogger<RabbitMqConsumer> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "localhost",
            UserName = _config["RabbitMQ:Username"] ?? "spazasure",
            Password = _config["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        };

        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.BasicQos(0, 20, false);

                _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
                // The exchange only carries notification events, so take all of them
                // (order.*, payout.*, compliance.*, notification.* ...)
                _channel.QueueBind(QueueName, ExchangeName, "#");

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += OnMessageReceived;
                _channel.BasicConsume(QueueName, autoAck: false, consumer);
                _logger.LogInformation("Connected to RabbitMQ. Consuming from queue: {Queue}", QueueName);
                attempt = 0;

                // Stay here while the connection is alive (automatic recovery handles short
                // network blips); reconnect from scratch if it is closed for good.
                while (!stoppingToken.IsCancellationRequested && _connection.IsOpen)
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                    _logger.LogWarning("RabbitMQ connection closed. Reconnecting...");
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                attempt++;
                var delay = TimeSpan.FromSeconds(Math.Min(60, 5 * attempt));
                _logger.LogWarning(ex, "Failed to connect to RabbitMQ (attempt {Attempt}). Retrying in {Delay}s...",
                    attempt, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
            finally
            {
                try { _channel?.Dispose(); } catch { }
                try { _connection?.Dispose(); } catch { }
                _channel = null;
                _connection = null;
            }
        }
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs args)
    {
        var channel = ((AsyncEventingBasicConsumer)sender).Model;
        NotificationEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<NotificationEvent>(Encoding.UTF8.GetString(args.Body.ToArray()), JsonOptions);
        }
        catch (JsonException ex)
        {
            // A malformed message will never succeed; drop it instead of looping forever
            _logger.LogError(ex, "Dropping malformed notification message");
            channel.BasicAck(args.DeliveryTag, false);
            return;
        }

        if (evt is null || string.IsNullOrWhiteSpace(evt.SupplierId) || string.IsNullOrWhiteSpace(evt.Title))
        {
            channel.BasicAck(args.DeliveryTag, false);
            return;
        }

        try
        {
            var notification = new Notification
            {
                SupplierId = evt.SupplierId,
                Type = Truncate(string.IsNullOrWhiteSpace(evt.Type) ? "system" : evt.Type, 50),
                Title = Truncate(evt.Title, 255),
                Message = Truncate(evt.Message ?? "", 1000),
                Priority = evt.Priority == "high" ? "high" : "normal",
                ReferenceId = evt.ReferenceId is null ? null : Truncate(evt.ReferenceId, 255),
                CreatedAt = DateTime.UtcNow,
            };

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                db.Notifications.Add(notification);
                await db.SaveChangesAsync();
            }
            channel.BasicAck(args.DeliveryTag, false);

            // Real-time push is best effort; clients also poll, so a failure here is harmless
            try
            {
                await _hubContext.Clients.Group(evt.SupplierId).SendAsync("ReceiveNotification", new
                {
                    notification.Id,
                    notification.Type,
                    notification.Title,
                    notification.Message,
                    notification.Priority,
                    notification.ReferenceId,
                    notification.IsRead,
                    notification.CreatedAt,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR push failed for {UserId}", evt.SupplierId);
            }

            _logger.LogInformation("Notification stored for user {UserId}: {Title}", evt.SupplierId, evt.Title);
        }
        catch (Exception ex)
        {
            // Retry once (e.g. the database was briefly unavailable), then give up on the message
            _logger.LogError(ex, "Error processing notification message (redelivered: {Redelivered})", args.Redelivered);
            channel.BasicNack(args.DeliveryTag, false, requeue: !args.Redelivered);
        }
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    public override void Dispose()
    {
        try { _channel?.Close(); } catch { }
        try { _connection?.Close(); } catch { }
        base.Dispose();
    }
}
