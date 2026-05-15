using NotificationService.Domain;

namespace NotificationService.Application.Abstractions
{
    public interface ITenantService
    {
        Task<Result<bool>> ValidateApiKeyAsync(Guid tenantId, string apiKey, CancellationToken ct = default);
    }
}