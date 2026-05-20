using NotificationService.Scheduler.Polling;
using NotificationService.Scheduler.RabbitMQ;
using RabbitMQ.Client;
public class PollerBackgroundService(PollAction pollAction, ILogger<PollerBackgroundService> logger, RabbitMQEstablisher rabbitMQEstablisher) : BackgroundService
{
    private readonly RabbitMQEstablisher _rabbitMQEstablisher = rabbitMQEstablisher;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: MEOWWW Call establish connection        
        await _rabbitMQEstablisher.EstablishConnection();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pollAction.PollAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during polling.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}