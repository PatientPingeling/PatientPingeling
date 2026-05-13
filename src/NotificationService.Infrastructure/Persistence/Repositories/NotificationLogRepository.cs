using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class NotificationLogRepository : INotificationLogRepository
  {
    public Task AddAsync(NotificationLog log, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }
  }
}