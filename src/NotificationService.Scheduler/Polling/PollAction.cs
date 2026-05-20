using NotificationService.Application.Abstractions;
using NotificationService.Application.Factories;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Scheduler.RabbitMQ;
using NotificationService.Domain.Entities;
using RabbitMQ.Client.Exceptions;
using System.Threading.Channels;

namespace NotificationService.Scheduler.Polling
{
    public class PollAction(
        ILogger<PollAction> logger,
        IDispatchLogRepository dispatchLogRepository,
        RabbitMQEstablisher queueEstablisher,
        IScheduledNotificationRepository scheduledNotificationRepository,
        INotificationMessageFactory notificationMessageFactory)
        // Class that contains service to fill rabbitMQNotification
    {
        private readonly ILogger<PollAction> _logger = logger;
        private readonly IDispatchLogRepository _dispatchLogRepository = dispatchLogRepository;
        private readonly RabbitMQEstablisher _queueEstablisher = queueEstablisher;
        private readonly IScheduledNotificationRepository _scheduledNotificationRepository = scheduledNotificationRepository;
        private readonly INotificationMessageFactory _notificationMessageFactory = notificationMessageFactory;

        public async Task PollAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Polling for scheduled notifications...");

            // Add in transaction log: Notification X status: trying to publish to queue
            var pending = await _scheduledNotificationRepository.GetPendingAsync(DateTimeOffset.UtcNow, cancellationToken);

            var notificationMessages = await _notificationMessageFactory.CreateAsync(pending.ToArray(), cancellationToken);
            RabbitMQNotificationMessage[] notifications = notificationMessages
                .Select(RabbitMQNotificationMessage.FromNotificationMessage)
                .ToArray();

            if (notifications.Count() == 0){
                _logger.LogInformation("No scheduled notifications found, ending polling sequence.");
                return;
            }

            foreach (var notification in notifications)
            {
                try
                    {
                        var dispatchLog = new DispatchLog
                        {
                            AttemptedAt = DateTimeOffset.UtcNow,
                            Outcome = Outcome.INSCHEDULER,
                            ScheduledNotificationId = notification.ScheduledNotification.Id
                        };

                        await _dispatchLogRepository.AddAsync(dispatchLog, cancellationToken);
                        
                        // Log success in the transaction log
                        _logger.LogInformation("Notification set to dispatch outcome: INSCHEDULER");
                    }
                    catch
                    {
                        _logger.LogError("Dispatch Log is unreachable, skipping notification.");
                    }
                // Als lukt
                try{
                    await _queueEstablisher.PublishAsync(notification, cancellationToken);
                    _logger.LogInformation("Notification {Id} published to RabbitMQ.", notification.ScheduledNotification.Id);
                }catch{
                    // Dispatchlog with outcome NEW
                    var dispatchLog = new DispatchLog
                    {
                        AttemptedAt = DateTimeOffset.UtcNow,
                        Outcome = Outcome.NEW,
                        ScheduledNotificationId = notification.ScheduledNotification.Id
                    };

                    await _dispatchLogRepository.AddAsync(dispatchLog, cancellationToken);
                    _logger.LogInformation("RabbitMQ is unreachable, set dispatch outcome back to NEW");
                }
            }
        }
    }
}
