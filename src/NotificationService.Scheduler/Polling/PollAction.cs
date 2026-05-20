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
        IUnitOfWork unitOfWork,
        ILogger<PollAction> logger,
        IDispatchLogRepository dispatchLogRepository,
        RabbitMQEstablisher queueEstablisher,
        IScheduledNotificationRepository scheduledNotificationRepository,
        INotificationMessageFactory notificationMessageFactory)
    {
        private readonly ILogger<PollAction> _logger = logger;
        private readonly IDispatchLogRepository _dispatchLogRepository = dispatchLogRepository;
        private readonly RabbitMQEstablisher _queueEstablisher = queueEstablisher;
        private readonly IScheduledNotificationRepository _scheduledNotificationRepository = scheduledNotificationRepository;
        private readonly INotificationMessageFactory _notificationMessageFactory = notificationMessageFactory;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        public async Task PollAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Polling for scheduled notifications...");

            var pending = await _scheduledNotificationRepository.GetPendingAsync(DateTimeOffset.UtcNow, cancellationToken);

            var notificationMessages = await _notificationMessageFactory.CreateAsync(pending.ToArray(), cancellationToken);
            RabbitMQNotificationMessage[] notifications = notificationMessages
                .Select(RabbitMQNotificationMessage.FromNotificationMessage)
                .ToArray();

            if (!notifications.Any())
            {
                _logger.LogInformation("No scheduled notifications found, ending polling sequence.");
                return;
            }

            foreach (var notification in notifications)
            {
                // Step 1: Log INSCHEDULER status
                try
                {
                    await _unitOfWork.BeginTransactionAsync(cancellationToken);

                    var inSchedulerLog = new DispatchLog
                    {
                        AttemptedAt = DateTimeOffset.UtcNow,
                        Outcome = Outcome.INSCHEDULER,
                        ScheduledNotificationId = notification.ScheduledNotification.Id
                    };

                    await _dispatchLogRepository.AddAsync(inSchedulerLog, cancellationToken);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Notification {Id} set to dispatch outcome: INSCHEDULER.", notification.ScheduledNotification.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write INSCHEDULER dispatch log for notification {Id}, skipping.", notification.ScheduledNotification.Id);
                    continue;
                }

                // Step 2: Publish to RabbitMQ and log result
                try
                {
                    await _queueEstablisher.PublishAsync(notification, cancellationToken);
                    _logger.LogInformation("Notification {Id} published to RabbitMQ.", notification.ScheduledNotification.Id);

                    await _unitOfWork.BeginTransactionAsync(cancellationToken);

                    var inQueueLog = new DispatchLog
                    {
                        AttemptedAt = DateTimeOffset.UtcNow,
                        Outcome = Outcome.INQUEUE,
                        ScheduledNotificationId = notification.ScheduledNotification.Id
                    };

                    await _dispatchLogRepository.AddAsync(inQueueLog, cancellationToken);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Notification {Id} dispatch outcome set to: INQUEUE.", notification.ScheduledNotification.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish notification {Id} to RabbitMQ, rolling back to NEW.", notification.ScheduledNotification.Id);

                    try
                    {
                        await _unitOfWork.BeginTransactionAsync(cancellationToken);

                        var newLog = new DispatchLog
                        {
                            AttemptedAt = DateTimeOffset.UtcNow,
                            Outcome = Outcome.NEW,
                            ScheduledNotificationId = notification.ScheduledNotification.Id
                        };

                        await _dispatchLogRepository.AddAsync(newLog, cancellationToken);
                        await _unitOfWork.CommitAsync();

                        _logger.LogInformation("Notification {Id} dispatch outcome set back to: NEW.", notification.ScheduledNotification.Id);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Failed to write NEW dispatch log for notification {Id}.", notification.ScheduledNotification.Id);
                    }
                }
            }
        }
    }
}