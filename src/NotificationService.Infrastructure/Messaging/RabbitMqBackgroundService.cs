using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Infrastructure.Messaging
{
  public abstract class RabbitMqBackgroundService(IConnection connection, ILogger logger) : BackgroundService
  {
    private readonly ConcurrentDictionary<ulong, Task> _pendingMessages = new();
    private readonly IConnection _connection = connection;
    private readonly ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      // outer while loop
      // connection check + delay
      // create channel (await using)
      // setup lifecycleTcs + shutdown handlers
      // call SetupChannelAsync(channel, stoppingToken)  <-- subclass decides what happens here
      // wait for lifecycleTcs
      // drain _pendingMessages on shutdown
    }

    protected async Task HandleMessageWithTrackingAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
      // copy from RabbitMqListenerService — this stays shared
    }

    protected abstract Task ProcessMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct);

    protected abstract Task SetupChannelAsync(IChannel channel, CancellationToken ct);
  }
}
