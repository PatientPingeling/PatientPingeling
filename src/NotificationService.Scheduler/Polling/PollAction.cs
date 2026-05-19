// using NotificationService.Infrastructure.Messaging;
// using System.Threading.Channels;

// namespace NotificationService.Scheduler.Polling
// {
//     public class PollAction(
//         ILogger<PollAction> logger,
//         IScheduledNotificationRepository notificationRepository,
//         ITransactionLogRepository transactionLogRepository,
//         NotificationSchedulerService schedulerService)
//     {
//         private readonly ILogger<PollAction> _logger = logger;
//         private readonly IScheduledNotificationRepository _notificationRepository = notificationRepository;
//         private readonly ITransactionLogRepository _transactionLogRepository = transactionLogRepository;
//         private readonly NotificationSchedulerService _schedulerService = schedulerService;

//         public async Task PollAsync(CancellationToken cancellationToken)
//         {
//             _logger.LogInformation("Polling for scheduled notifications...");

//             var notifications = await _notificationRepository.GetPendingAsync(cancellationToken);
//             // Add in transaction log: Notification X status: trying to publish to queue

//             foreach (var notification in notifications)
//             {
//                 var alreadyInSystem = await _transactionLogRepository.ExistsAsync(notification.Id, cancellationToken);

//                 if (alreadyInSystem)
//                 {
//                     _logger.LogInformation("Notification {Id} already in system, skipping.", notification.Id);
//                     continue;
//                 }

//                 await _schedulerService.PublishAsync(notification);
//                 // Als lukt
//                 // Transaction als success: Notification X status: Sent to queue
//                 // Als failed: Notification X status: waiting (to be sent again)
//                 _logger.LogInformation("Notification {Id} published to RabbitMQ.", notification.Id);
//             }
//         }
//     }
// }
