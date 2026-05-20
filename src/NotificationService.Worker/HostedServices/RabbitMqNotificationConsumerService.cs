using NotificationService.Infrastructure.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Worker.HostedServices
{
  public sealed class RabbitMqNotificationConsumerService(
    IConnectionFactory connectionFactory,
    // NotificationCommandMessageHandler messageProcessor,
    ILogger<RabbitMqNotificationConsumerService> logger) : BackgroundService
  {
    private readonly IConnectionFactory _connectionFactory = connectionFactory;
    // private readonly NotificationCommandMessageHandler _messageProcessor = messageProcessor;
    private readonly ILogger<RabbitMqNotificationConsumerService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
      await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken: ct);
      await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

      await channel.QueueDeclareAsync(
          queue: RabbitMqOptions.NotificationQueue,
          durable: true,
          exclusive: false,
          autoDelete: false,
          cancellationToken: ct);

      await channel.BasicQosAsync(
          prefetchSize: 0,
          prefetchCount: 1,
          global: false,
          cancellationToken: ct);

      var consumer = new AsyncEventingBasicConsumer(channel);
      consumer.ReceivedAsync += async (_, ea) =>
      {
        try
        {
          // await _messageProcessor.ProcessAsync(ea.Body, ct);

          await channel.BasicAckAsync(
                ea.DeliveryTag,
                multiple: false,
                cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
          _logger.LogInformation(
                "RabbitMQ consumer stopping while processing {DeliveryTag}.",
                ea.DeliveryTag);
        }
        catch (Exception ex)
        {
          _logger.LogError(
                ex,
                "Failed to process delivery {DeliveryTag}. Requeueing.",
                ea.DeliveryTag);

          await channel.BasicNackAsync(
                ea.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken: ct);
        }
      };

      await channel.BasicConsumeAsync(
          queue: RabbitMqOptions.NotificationQueue,
          autoAck: false,
          consumer: consumer,
          cancellationToken: ct);

      await Task.Delay(Timeout.Infinite, ct);
    }
  }
}
