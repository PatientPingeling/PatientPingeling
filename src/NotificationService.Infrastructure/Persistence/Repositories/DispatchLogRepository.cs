using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
    public class DispatchLogRepository(NotificationDbContext dbContext) : IDispatchLogRepository
    {
        private readonly NotificationDbContext _dbContext = dbContext;

        public Task AddAsync(DispatchLog log, CancellationToken ct = default)
        {
            _dbContext.DispatchLogs.Add(log);
            return Task.CompletedTask;
        }

        public async Task<DispatchLog?> GetLatestStatusByScheduledApointmentIdASync(Guid scheduledNotificationId, CancellationToken ct = default)
        {
            return await _dbContext.DispatchLogs
                .AsNoTracking()
                .Where(x => x.ScheduledNotificationId == scheduledNotificationId)
                .OrderByDescending(x => x.AttemptedAt)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyDictionary<Guid, DispatchLog?>> GetLatestStatusBatchAsync(IReadOnlyCollection<Guid> scheduledNotificationIds, CancellationToken ct = default)
        {
            var logs = await _dbContext.DispatchLogs
                .AsNoTracking()
                .Where(d => scheduledNotificationIds.Contains(d.ScheduledNotificationId))
                .ToListAsync(ct);

            return scheduledNotificationIds.ToDictionary(
                id => id,
                id => (DispatchLog?)logs
                    .Where(l => l.ScheduledNotificationId == id)
                    .MaxBy(l => l.AttemptedAt));
        }

        public async Task<IReadOnlyList<PendingAsyncDispatch>> GetPendingAsyncFlowAsync(CancellationToken ct = default)
        {
            return await _dbContext.DispatchLogs
                .AsNoTracking()
                .Where(d => d.Outcome == Outcome.PENDING_ASYNC && d.ExternalTrackingId != null)
                .Where(d => !_dbContext.DispatchLogs.Any(d2 =>
                    d2.ScheduledNotificationId == d.ScheduledNotificationId &&
                    d2.AttemptedAt > d.AttemptedAt))
                .Join(_dbContext.ScheduledNotifications,
                    d => d.ScheduledNotificationId,
                    sn => sn.Id,
                    (d, sn) => new { d, sn })
                .Join(_dbContext.Appointments,
                    x => x.sn.AppointmentId,
                    a => a.Id,
                    (x, a) => new PendingAsyncDispatch(
                        x.d.Id,
                        x.d.ScheduledNotificationId,
                        x.d.ExternalTrackingId!,
                        x.d.AttemptedAt,
                        a.TenantId))
                .ToListAsync(ct);
        }
    }
}