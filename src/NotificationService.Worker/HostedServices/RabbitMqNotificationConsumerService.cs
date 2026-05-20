using System.Text;
using System.Text.Json;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Infrastructure.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Worker.HostedServices
{
  public sealed class RabbitMqNotificationConsumerService(
    IServiceScopeFactory scopeFactory,
    IConnectionFactory connectionFactory,
    ILogger<RabbitMqNotificationConsumerService> logger) : BackgroundService
  {
    private readonly IConnectionFactory _connectionFactory = connectionFactory;
    private readonly ILogger<RabbitMqNotificationConsumerService> _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
          await using var scope = _scopeFactory.CreateAsyncScope();
          var dispatchService = scope.ServiceProvider.GetRequiredService<INotificationDispatchService>();

          ct.ThrowIfCancellationRequested();

          var message = Encoding.UTF8.GetString(ea.Body.Span);
          var command = JsonSerializer.Deserialize<RabbitMQNotificationMessage>(message, JsonOptions) ?? throw new InvalidOperationException("RabbitMQ notification command is missing a scheduled notification id.");

          // TODO: check message staleness — if DateTimeOffset.UtcNow is far past command.SendAt, ACK and log Outcome.EXPIRED instead of dispatching (#56)

          var result = await dispatchService.DispatchAsync(command, ct);
          if (result.IsFailure)
          {
            // TODO: distinguish transient (requeue: true) from permanent failures (requeue: false / dead-letter) based on result.Error.Type to avoid infinite requeue loops (#56)
            throw new InvalidOperationException($"Notification dispatch failed: {result.Error.Code}.");
          }

          _logger.LogInformation("Dispatched scheduled notification {ScheduledNotificationId} through provider message {ExternalMessageId}.", command.ScheduledNotificationId, result.Value);

          // TODO: write DispatchLog (Outcome.SUCCESS, HttpStatusCode, command.ScheduledNotificationId) via IDispatchLogRepository
          // TODO: write NotificationLog (billing record) via INotificationLogRepository

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
