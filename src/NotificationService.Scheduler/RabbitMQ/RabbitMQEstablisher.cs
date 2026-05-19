using NotificationService.Infrastructure.Options;
using System.Text.Json;
using RabbitMQ.Client;
using System.Text;

namespace NotificationService.Scheduler.RabbitMQ
{
    public class RabbitMQEstablisher(IConnectionFactory connectionFactory)
    {
        private readonly IConnectionFactory _connectionFactory = connectionFactory;
        private IConnection? _connection;
        private IChannel? _channel;

        public async Task EstablishConnection()
        {
            // Creating connection and channel
            _connection = await _connectionFactory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            // Enabling fair dispatch
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false);

            // Declaring queue
            await _channel.QueueDeclareAsync(
                queue: RabbitMqOptions.NotificationQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        public async Task PublishAsync<T>(T message)
        {
            if (_channel is null)
                throw new InvalidOperationException("Call EstablishConnection first.");

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: RabbitMqOptions.NotificationQueue,
                body: body);
        }
    }
}