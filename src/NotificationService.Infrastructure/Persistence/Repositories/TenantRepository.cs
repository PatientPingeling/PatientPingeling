using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class TenantRepository(NotificationDbContext dbContext) : ITenantRepository
  {
    private readonly NotificationDbContext _dbContext = dbContext;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }
  }
}