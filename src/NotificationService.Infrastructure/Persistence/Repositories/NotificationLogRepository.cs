using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class NotificationLogRepository(NotificationDbContext dbContext) : INotificationLogRepository
  {
    private readonly NotificationDbContext _dbContext = dbContext;

    public Task AddAsync(NotificationLog log, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }
  }
}