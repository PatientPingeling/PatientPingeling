using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using NotificationService.Infrastructure.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Listener.HostedServices
{
  public sealed class RabbitMqListenerService(
      IConnection connection,
      IServiceProvider serviceProvider,
      ILogger<RabbitMqListenerService> logger) : BackgroundService
  {
    private readonly IConnection _connection = connection;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<RabbitMqListenerService> _logger = logger;
    private readonly ConcurrentDictionary<ulong, Task> _pendingMessages = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (stoppingToken.IsCancellationRequested is false)
      {
        try
        {
          if (_connection.IsOpen is false)
          {
            _logger.LogWarning("RabbitMQ connection is down. Retrying in 5s...");
            await Task.Delay(5000, stoppingToken);
            continue;
          }

          await using var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

          var lifecycleTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

          channel.ChannelShutdownAsync += (s, e) =>
          {
            lifecycleTcs.TrySetResult(false);
            return Task.CompletedTask;
          };

          _connection.ConnectionShutdownAsync += (s, e) =>
          {
            lifecycleTcs.TrySetResult(false);
            return Task.CompletedTask;
          };

          await channel.QueueDeclareAsync(
              queue: RabbitMqOptions.NotificationQueue,
              durable: true,
              exclusive: false,
              autoDelete: false,
              cancellationToken: stoppingToken);

          await channel.BasicQosAsync(0, 10, false, stoppingToken);

          var consumer = new AsyncEventingBasicConsumer(channel);
          consumer.ReceivedAsync += (sender, ea) => HandleMessageWithTrackingAsync(channel, ea, stoppingToken);

          await channel.BasicConsumeAsync(
              queue: RabbitMqOptions.NotificationQueue,
              autoAck: false,
              consumer: consumer,
              cancellationToken: stoppingToken);

          _logger.LogInformation("Connected to RabbitMQ v7+. Listening on {Queue}", RabbitMqOptions.NotificationQueue);

          using (stoppingToken.Register(() => lifecycleTcs.TrySetCanceled()))
          {
            await lifecycleTcs.Task;
          }
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Infrastructure error. Restarting listener in 5s...");
          await Task.Delay(5000, stoppingToken);
        }
      }

      if (_pendingMessages.IsEmpty is false)
      {
        _logger.LogInformation("Waiting for {Count} messages to drain...", _pendingMessages.Count);
        await Task.WhenAny(Task.WhenAll(_pendingMessages.Values), Task.Delay(10000));
      }
    }

    private async Task HandleMessageWithTrackingAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
      var task = ProcessMessageAsync(channel, ea, ct);
      _pendingMessages.TryAdd(ea.DeliveryTag, task);
      try { await task; }
      finally { _pendingMessages.TryRemove(ea.DeliveryTag, out _); }
    }

    private async Task ProcessMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
      using var scope = _serviceProvider.CreateScope();
      var notificationService = scope.ServiceProvider.GetRequiredService<IAppointmentIngestionService>();

      try
      {
        var message = Encoding.UTF8.GetString(ea.Body.ToArray());
        // var result = await notificationService.Equals(message);

        // if (result.IsFailure)
        // {
        //   await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, ct);
        // }
        // else
        // {
        //   await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
        // }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "System Exception for {Tag}. Requeueing.", ea.DeliveryTag);
        if (ct.IsCancellationRequested is false)
        {
          await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true, ct);
        }
      }
    }
  }
}