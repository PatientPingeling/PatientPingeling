using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class ScheduledNotificationRepository(NotificationDbContext dbContext) : IScheduledNotificationRepository
  {
    private readonly NotificationDbContext _dbContext = dbContext;

    public Task AddAsync(ScheduledNotification notification, CancellationToken ct = default)
    {
      _dbContext.ScheduledNotifications.Add(notification);
      return Task.CompletedTask;
    }

    public Task AddRangeAsync(IReadOnlyCollection<ScheduledNotification> notifications, CancellationToken ct = default)
    {
      _dbContext.ScheduledNotifications.AddRange(notifications);
      return Task.CompletedTask;
    }

    public async Task<int> DeletePendingByAppointmentIdAsync(int appointmentId, CancellationToken ct = default)
    {
      // TODO: This read-then-delete is not atomic. The Scheduler can mark a notification as
      // Processing between this read and SaveChanges. Fix with SELECT FOR UPDATE SKIP LOCKED
      // when implementing the Scheduler polling query.
      var toDelete = await _dbContext.ScheduledNotifications
        .Where(x => x.AppointmentId == appointmentId &&
          x.Status == ScheduledNotificationStatus.Pending)
        .ToListAsync(ct);

      _dbContext.ScheduledNotifications.RemoveRange(toDelete);
      return toDelete.Count;
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