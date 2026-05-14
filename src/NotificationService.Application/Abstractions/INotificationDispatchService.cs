namespace NotificationService.Application.Abstractions
{
    public interface INotificationDispatchService
    {
        Task DispatchAsync(Guid scheduledNotificationId, CancellationToken ct);
    }
}
