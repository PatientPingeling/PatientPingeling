// using NotificationService.Scheduler.Polling;
// public class PollerBackgroundService(PollAction pollAction, ILogger<PollerBackgroundService> logger) : BackgroundService
// {
//     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//     {
            //TODO: MEOWWW Call establish connection
//         while (!stoppingToken.IsCancellationRequested)
//         {
//             try
//             {
//                 await pollAction.PollAsync(stoppingToken);
//             }
//             catch (Exception ex)
//             {
//                 logger.LogError(ex, "Error occurred during polling.");
//             }

//             await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
//         }
//     }
// }