using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
  public interface IScheduledNotificationRepository
  {
    Task<IReadOnlyCollection<ScheduledNotification>> GetPendingAsync(DateTimeOffset before, CancellationToken ct = default);
    Task AddAsync(ScheduledNotification notification, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, ScheduledNotificationStatus status, CancellationToken ct = default);
  }
}