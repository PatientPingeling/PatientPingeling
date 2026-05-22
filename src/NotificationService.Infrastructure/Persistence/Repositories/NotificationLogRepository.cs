using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
    public class NotificationLogRepository(NotificationDbContext dbContext) : INotificationLogRepository
    {
        private readonly NotificationDbContext _dbContext = dbContext;

        public Task AddAsync(NotificationLog log, CancellationToken ct = default)
        {
            _dbContext.NotificationLogs.Add(log);
            return Task.CompletedTask;
        }

        public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        {
            return await _dbContext.NotificationLogs
                .Where(n => n.SentAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }
    }
}