using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class ScheduledNotificationRepository : IScheduledNotificationRepository
  {
    public Task AddAsync(ScheduledNotification notification, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }

    public Task<IReadOnlyCollection<ScheduledNotification>> GetPendingAsync(DateTimeOffset before, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }

    public Task UpdateStatusAsync(Guid id, ScheduledNotificationStatus status, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }
  }
}