using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{

  public class ProviderCredentialRepository(NotificationDbContext dbContext) : IProviderCredentialRepository
  {
    private readonly NotificationDbContext _dbContext = dbContext;

    public Task AddAsync(ProviderCredential credential, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }

    public Task DeleteByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }
  }
}