using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
    public class ProviderCredentialRepository(NotificationDbContext dbContext) : IProviderCredentialRepository
    {
        private readonly NotificationDbContext _dbContext = dbContext;

        public Task AddAsync(ProviderCredential credential, CancellationToken ct = default)
        {
            _dbContext.ProviderCredentials.Add(credential);
            return Task.CompletedTask;
        }

        public async Task DeleteByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            await _dbContext.ProviderCredentials
                .Where(c => c.TenantId == tenantId)
                .ExecuteDeleteAsync(ct);
        }
    }
}