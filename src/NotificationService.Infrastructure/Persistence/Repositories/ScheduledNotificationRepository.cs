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

        public async Task<IReadOnlyCollection<Guid>> GetPendingIdsByAppointmentIdAsync(int appointmentId, CancellationToken ct = default)
        {
            return await _dbContext.ScheduledNotifications
              .Where(s => s.AppointmentId == appointmentId)
              .Where(s => !_dbContext.DispatchLogs.Any(d => d.ScheduledNotificationId == s.Id && d.Outcome == Outcome.SUCCESS))
              .Select(s => s.Id)
              .ToListAsync(ct);
        }

        public async Task<int> DeletePendingByAppointmentIdAsync(int appointmentId, CancellationToken ct = default)
        {
            var toDelete = await _dbContext.ScheduledNotifications
                .Where(s => s.AppointmentId == appointmentId)
                .Where(s => !_dbContext.DispatchLogs.Any(d =>
                    d.ScheduledNotificationId == s.Id && d.Outcome == Outcome.SUCCESS))
                .ToListAsync(ct);

            _dbContext.ScheduledNotifications.RemoveRange(toDelete);
            return toDelete.Count;
        }

        public async Task<IReadOnlyCollection<ScheduledNotification>> GetPendingAsync(DateTimeOffset before, CancellationToken ct = default)
        {
            return await _dbContext.ScheduledNotifications
                .AsNoTracking()
                .Where(s => s.SendAt <= before)
                .Where(s => !s.Appointment.IsCancelled)
                .Where(s => _dbContext.DispatchLogs.Any(d => d.ScheduledNotificationId == s.Id))
                .Where(s =>
                    _dbContext.DispatchLogs
                        .Where(d => d.ScheduledNotificationId == s.Id)
                        .OrderByDescending(d => d.AttemptedAt)
                        .Select(d => d.Outcome)
                        .FirstOrDefault() is Outcome.NEW or Outcome.EXPIRED or Outcome.ERROR_TRANSIENT
                    || (
                        _dbContext.DispatchLogs
                            .Where(d => d.ScheduledNotificationId == s.Id)
                            .OrderByDescending(d => d.AttemptedAt)
                            .Select(d => d.Outcome)
                            .FirstOrDefault() == Outcome.INSCHEDULER &&
                        _dbContext.DispatchLogs
                            .Where(d => d.ScheduledNotificationId == s.Id)
                            .OrderByDescending(d => d.AttemptedAt)
                            .Select(d => d.AttemptedAt)
                            .FirstOrDefault() < DateTimeOffset.UtcNow.AddMinutes(-5)))
                .OrderBy(s => s.SendAt)
                .Take(50)
                .ToListAsync(ct);
        }
    }
}
