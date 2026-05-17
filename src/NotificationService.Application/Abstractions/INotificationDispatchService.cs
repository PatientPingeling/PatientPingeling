using NotificationService.Domain;

namespace NotificationService.Application.Abstractions
{
    public interface INotificationDispatchService
    {
        Task<Result<string>> DispatchAsync(Guid scheduledNotificationId, CancellationToken ct = default);
    }
}
