using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
  public interface INotificationLogRepository
  {
    Task AddAsync(NotificationLog log, CancellationToken ct = default);
  }
}