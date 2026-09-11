using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SpazaSure.NotificationService.Data;
using SpazaSure.NotificationService.Hubs;
using SpazaSure.NotificationService.Models;

namespace SpazaSure.NotificationService.Consumers;

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
        await Task.Delay(5000, stoppingToken); // Wait for RabbitMQ to be ready

        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "localhost",
            UserName = _config["RabbitMQ:Username"] ?? "spazasure",
            Password = _config["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true,
        };

        var retries = 0;
        while (!stoppingToken.IsCancellationRequested && retries < 10)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare exchange and queue
                _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);

                // Bind to all notification events
                _channel.QueueBind(QueueName, ExchangeName, "notification.#");
                _channel.QueueBind(QueueName, ExchangeName, "order.#");
                _channel.QueueBind(QueueName, ExchangeName, "product.#");
                _channel.QueueBind(QueueName, ExchangeName, "compliance.#");

                _logger.LogInformation("Connected to RabbitMQ. Consuming from queue: {Queue}", QueueName);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += OnMessageReceived;
                _channel.BasicConsume(QueueName, autoAck: false, consumer);

                // Keep alive until cancelled
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                retries++;
                _logger.LogWarning(ex, "Failed to connect to RabbitMQ (attempt {Attempt}/10). Retrying in 5s...", retries);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs args)
    {
        try
        {
            var body = Encoding.UTF8.GetString(args.Body.ToArray());
            var evt = JsonSerializer.Deserialize<NotificationEvent>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (evt == null)
            {
                _channel?.BasicAck(args.DeliveryTag, false);
                return;
            }

            // Save to database
            var notification = new Notification
            {
                SupplierId = evt.SupplierId,
                Type = evt.Type,
                Title = evt.Title,
                Message = evt.Message,
                Priority = evt.Priority,
                ReferenceId = evt.ReferenceId,
                CreatedAt = DateTime.UtcNow,
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();

            // Push real-time via SignalR to specific supplier
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

            _logger.LogInformation("Notification sent to supplier {SupplierId}: {Title}", evt.SupplierId, evt.Title);
            _channel?.BasicAck(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification message");
            _channel?.BasicNack(args.DeliveryTag, false, true); // Requeue on failure
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
