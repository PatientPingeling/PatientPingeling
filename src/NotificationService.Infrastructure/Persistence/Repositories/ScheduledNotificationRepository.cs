using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class ScheduledNotificationRepository(NotificationDbContext dbContext) : IScheduledNotificationRepository
  {
    private readonly NotificationDbContext _dbContext = dbContext;


    public async Task<ScheduledNotification?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
      return await _dbContext.ScheduledNotifications
          .Include(n => n.Appointment)
              .ThenInclude(a => a.Patient)
          .Include(n => n.Appointment)
              .ThenInclude(a => a.Tenant)
                  .ThenInclude(t => t.Credentials)
          .AsSplitQuery()
          .AsNoTracking()
          .FirstOrDefaultAsync(n => n.Id == id, ct);
    }

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
      // SELECT FOR UPDATE SKIP LOCKED — locks rows so the Scheduler cannot concurrently
      // mark them as Processing between our read and the SaveChanges commit.
      var toDelete = await _dbContext.ScheduledNotifications
        .FromSqlRaw(@"SELECT * FROM ""ScheduledNotifications""
                      WHERE ""AppointmentId"" = {0}
                      AND ""Status"" = 'Pending'
                      FOR UPDATE SKIP LOCKED", appointmentId)
        .ToListAsync(ct);

      _dbContext.ScheduledNotifications.RemoveRange(toDelete);
      return toDelete.Count;
    }

    public Task<IReadOnlyCollection<ScheduledNotification>> GetPendingAsync(DateTimeOffset before, CancellationToken ct = default)
    {
      throw new NotImplementedException(); // TODO @JanssenJochem
    }

    public Task UpdateStatusAsync(Guid id, ScheduledNotificationStatus status, CancellationToken ct = default)
    {
      throw new NotImplementedException(); // TODO @JanssenJochem
    }


  }
}