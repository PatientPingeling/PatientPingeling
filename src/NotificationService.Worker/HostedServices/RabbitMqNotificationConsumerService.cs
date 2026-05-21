using System.Text;
using System.Text.Json;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Domain.Entities;
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
        private static readonly TimeSpan MessageSla = TimeSpan.FromHours(2);

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
                    var dispatchLogRepository = scope.ServiceProvider.GetRequiredService<IDispatchLogRepository>();
                    var notificationLogRepository = scope.ServiceProvider.GetRequiredService<INotificationLogRepository>();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    ct.ThrowIfCancellationRequested();

                    var message = Encoding.UTF8.GetString(ea.Body.Span);
                    var command = JsonSerializer.Deserialize<RabbitMQNotificationMessage>(message, JsonOptions) ?? throw new InvalidOperationException("Failed to deserialize RabbitMQ message body.");

                    // Idempotency check — if already dispatched successfully, ACK and skip
                    var latestLog = await dispatchLogRepository.GetLatestStatusByScheduledApointmentIdASync(command.ScheduledNotificationId, ct);
                    if (latestLog?.Outcome == Outcome.SUCCESS)
                    {
                        _logger.LogWarning("Notification {Id} already dispatched successfully, skipping duplicate.", command.ScheduledNotificationId);
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                        return;
                    }

                    // Staleness check — if message has been in-flight longer than the SLA, discard it
                    if (DateTimeOffset.UtcNow - command.EnqueuedAt > MessageSla)
                    {
                        _logger.LogWarning("Notification {Id} expired (enqueued at {EnqueuedAt}), discarding.", command.ScheduledNotificationId, command.EnqueuedAt);

                        await dispatchLogRepository.AddAsync(new DispatchLog
                        {
                            Id = Guid.CreateVersion7(),
                            AttemptedAt = DateTimeOffset.UtcNow,
                            Outcome = Outcome.EXPIRED,
                            ScheduledNotificationId = command.ScheduledNotificationId
                        }, ct);

                        await unitOfWork.CommitAsync();

                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                        return;
                    }

                    var result = await dispatchService.DispatchAsync(command, ct);

                    if (result.IsFailure)
                    {
                        var transient = result.Error.Type == Domain.ErrorType.Failure;
                        var outcome = transient ? Outcome.ERROR_429 : Outcome.ERROR_PERMANENT;

                        _logger.LogWarning("Dispatch failed for {Id}: {Error} (outcome: {Outcome})", command.ScheduledNotificationId, result.Error.Code, outcome);

                        await dispatchLogRepository.AddAsync(new DispatchLog
                        {
                            Id = Guid.CreateVersion7(),
                            AttemptedAt = DateTimeOffset.UtcNow,
                            Outcome = outcome,
                            ScheduledNotificationId = command.ScheduledNotificationId
                        }, ct);

                        await unitOfWork.CommitAsync();

                        // ERROR_PERMANENT → reject without requeue (bad payload, no contact info)
                        // ERROR_429 → requeue for retry (provider down, transient HTTP failure)
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: transient, cancellationToken: ct);
                        return;
                    }

                    _logger.LogInformation("Dispatched notification {Id}, external message {ExternalId}.", command.ScheduledNotificationId, result.Value);

                    await dispatchLogRepository.AddAsync(new DispatchLog
                    {
                        Id = Guid.CreateVersion7(),
                        AttemptedAt = DateTimeOffset.UtcNow,
                        Outcome = Outcome.SUCCESS,
                        ScheduledNotificationId = command.ScheduledNotificationId
                    }, ct);

                    await notificationLogRepository.AddAsync(new NotificationLog
                    {
                        Id = Guid.CreateVersion7(),
                        SentAt = DateTimeOffset.UtcNow,
                        Provider = command.Provider,
                        ExternalMessageId = result.Value,
                        Succeeded = true,
                        TenantId = command.TenantId
                    }, ct);

                    await unitOfWork.CommitAsync();

                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("RabbitMQ consumer stopping while processing {DeliveryTag}.", ea.DeliveryTag);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception processing delivery {DeliveryTag}. Requeueing.", ea.DeliveryTag);

                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
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
