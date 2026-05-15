using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class TenantRepository(NotificationDbContext dbContext) : ITenantRepository
  {
    private readonly NotificationDbContext _dbContext = dbContext;

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
      return await _dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken: ct);
    }
  }
}