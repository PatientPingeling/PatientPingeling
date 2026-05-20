using NotificationService.Infrastructure.Messaging;
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
            _channel = await _connection.CreateChannelAsync();

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

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, _jsonOptions));

            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: RabbitMqOptions.NotificationQueue,
                body: body,
                cancellationToken: ct);
        }
    }
}