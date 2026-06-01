using NotificationService.Scheduler.Polling;
using NotificationService.Scheduler.RabbitMQ;
using RabbitMQ.Client;
namespace NotificationService.Scheduler.Polling;

public class PollerBackgroundService(
    IServiceScopeFactory scopeFactory,
    RabbitMQEstablisher rabbitMQEstablisher,
    ILogger<PollerBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await rabbitMQEstablisher.EstablishConnection();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var pollAction = scope.ServiceProvider.GetRequiredService<PollAction>();
                await pollAction.PollAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during polling.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}