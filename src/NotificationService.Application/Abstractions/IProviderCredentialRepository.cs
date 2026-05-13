using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
  public interface IProviderCredentialRepository
  {
    Task<IReadOnlyCollection<ProviderCredential>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
  }
}