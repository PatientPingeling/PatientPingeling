namespace NotificationService.Application.Abstractions
{
    public interface INotificationCommandPublisher
    {
        Task PublishAsync(Guid scheduledNotificationId, CancellationToken ct = default);
    }
}
