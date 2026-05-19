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
            return await _dbContext.DispatchLogs.AsNoTracking().Where(x => x.ScheduledNotificationId == scheduledNotificationId).OrderByDescending(x => x.AttemptedAt).FirstOrDefaultAsync(ct);
        }
    }
}