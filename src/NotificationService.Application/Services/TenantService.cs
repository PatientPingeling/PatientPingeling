using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Domain;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Services
{
  public class TenantService(ITenantRepository tenantRepository, IHashingService hashingService, ILogger<TenantService> logger) : ITenantService
  {
    private readonly ITenantRepository _tenantRepository = tenantRepository;
    private readonly IHashingService _hashingService = hashingService;
    private readonly ILogger<TenantService> _logger = logger;

    public async Task<Result> ValidateApiKeyAsync(Guid tenantId, string apiKey, CancellationToken ct = default)
    {
      if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(apiKey))
      {
        return Result.Failure(new Error("tenant.invalid_request", "Tenant ID or API key is missing.", ErrorType.Validation));
      }

      Tenant? tenant;
      try
      {
        tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to retrieve tenant {TenantId}", tenantId);
        return Result.Failure(new Error("tenant.db_error", "Failed to retrieve tenant.", ErrorType.Failure));
      }

      if (tenant is null)
      {
        return Result.Failure(new Error("tenant.not_found", "Tenant not found.", ErrorType.NotFound));
      }

      var isMatch = _hashingService.Validate(hashedValue: tenant.ApiKeyHash, plainText: apiKey);
      if (isMatch is false)
      {
        return Result.Failure(new Error("tenant.invalid_api_key", "API key does not match.", ErrorType.Unauthorized));
      }

      return Result.Success();
    }
  }
}