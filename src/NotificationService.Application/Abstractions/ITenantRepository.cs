using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
  public interface ITenantRepository
  {
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
  }
}