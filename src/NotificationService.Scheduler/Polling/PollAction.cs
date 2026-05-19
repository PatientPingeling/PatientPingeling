using NotificationService.Application.Abstractions;
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
        RabbitMQEstablisher queueEstablisher)
        // Class that contains service to fill rabbitMQNotification
    {
        private readonly ILogger<PollAction> _logger = logger;
        private readonly IDispatchLogRepository _dispatchLogRepository = dispatchLogRepository;
        private readonly RabbitMQEstablisher _queueEstablisher = queueEstablisher;

        public async Task PollAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Polling for scheduled notifications...");

            // Add in transaction log: Notification X status: trying to publish to queue

            foreach (var notification in notifications)
            {
                try
                    {
                        // Publish the notification
                        await _queueEstablisher.PublishAsync(notification);

                        // Log success in the transaction log
                        await _dispatchLogRepository.LogAsync(notification.Id, "Sent to queue");
                        _logger.LogInformation("Notification {Id} successfully published to RabbitMQ.", notification.Id);
                    }
                    catch (BrokerUnreachableException ex)
                    {
                        _logger.LogError(ex, "RabbitMQ is unreachable. Notification {Id} will be retried.", notification.Id);
                        await _dispatchLogRepository.LogAsync(notification.Id, "Waiting (to be sent again)");
                    }
                    catch (OperationInterruptedException ex)
                    {
                        _logger.LogError(ex, "RabbitMQ operation failed for notification {Id}.", notification.Id);
                        await _dispatchLogRepository.LogAsync(notification.Id, "Waiting (to be sent again)");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An unexpected error occurred while publishing notification {Id}.", notification.Id);
                        await _dispatchLogRepository.LogAsync(notification.Id, "Failed");
                    }
                // Als lukt
                // Transaction als success: Notification X status: Sent to queue
                // Als failed: Notification X status: waiting (to be sent again)
                _logger.LogInformation("Notification {Id} published to RabbitMQ.", notification.Id);
            }
        }
    }
}
