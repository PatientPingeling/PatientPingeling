using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
    public interface IProviderCredentialRepository
    {
        Task AddAsync(ProviderCredential credential, CancellationToken ct = default);
        Task DeleteByTenantAsync(Guid tenantId, CancellationToken ct = default);
    }
}
