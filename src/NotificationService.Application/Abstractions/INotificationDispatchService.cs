using NotificationService.Application.Commands;
using NotificationService.Domain;

namespace NotificationService.Application.Abstractions
{
    public interface INotificationDispatchService
    {
        Task<Result<string>> DispatchAsync(RabbitMQNotificationMessage notificationMessage, CancellationToken ct = default);
    }
}
