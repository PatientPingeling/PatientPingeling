using NotificationService.Application.Abstractions;
using NotificationService.Domain;

namespace NotificationService.Application.Services
{
  public class TenantService(ITenantRepository tenantRepository, IHashingService hashingService) : ITenantService
  {
    private readonly ITenantRepository _tenantRepository = tenantRepository;
    private readonly IHashingService _hashingService = hashingService;

    public async Task<Result<bool>> ValidateApiKeyAsync(Guid tenantId, string apiKey, CancellationToken ct = default)
    {
      if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(apiKey))
      {
        return Result.Failure<bool>(new Error("tenant.invalid_request", "Tenant ID or API key is missing.", ErrorType.Validation));
      }

      var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
      if (tenant is null)
      {
        return Result.Failure<bool>(new Error("tenant.not_found", "Tenant not found.", ErrorType.NotFound));
      }

      var isMatch = _hashingService.Validate(hashedValue: tenant.ApiKeyHash, plainText: apiKey);
      return Result.Success(isMatch);
    }
  }
}