using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class ProviderCredentialRepository : IProviderCredentialRepository
  {
    public Task<IReadOnlyCollection<ProviderCredential>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }
  }
}