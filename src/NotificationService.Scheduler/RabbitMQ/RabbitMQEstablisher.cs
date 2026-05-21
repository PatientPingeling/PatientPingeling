using NotificationService.Application.Commands;
using NotificationService.Infrastructure.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;
using System.Text;

namespace NotificationService.Scheduler.RabbitMQ
{
    public class RabbitMQEstablisher(IConnectionFactory connectionFactory)
    {
        private readonly IConnectionFactory _connectionFactory = connectionFactory;
        private IConnection? _connection;
        private IChannel? _channel;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        public async Task EstablishConnection()
        {
            _connection = await _connectionFactory.CreateConnectionAsync();

            // PublisherConfirmationsEnabled + Tracking: BasicPublishAsync blocks until
            // the broker sends basic.ack, so silently-lost messages are impossible.
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false);

            await _channel.QueueDeclareAsync(
                queue: RabbitMqOptions.NotificationQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        public async Task PublishAsync(RabbitMQNotificationMessage message, CancellationToken ct = default)
        {
            if (_channel is null)
                throw new InvalidOperationException("Call EstablishConnection first.");

            ct.ThrowIfCancellationRequested();

            // Timestamp the moment we enqueue the message.
            message.EnqueuedAt = DateTimeOffset.UtcNow;

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, _jsonOptions));

            // With PublisherConfirmationTrackingEnabled, this awaits the broker's basic.ack.
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: RabbitMqOptions.NotificationQueue,
                body: body,
                cancellationToken: ct);
        }
    }
}