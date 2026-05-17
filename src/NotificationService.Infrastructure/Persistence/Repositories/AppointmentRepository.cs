using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class AppointmentRepository(NotificationDbContext dbContext) : IAppointmentRepository
  {
    private readonly NotificationDbContext _dbContext = dbContext;

    public async Task<Appointment?> GetByExternalIdAsync(string externalId, Guid tenantId, CancellationToken ct = default)
    {
      return await _dbContext.Appointments.Include(a => a.Patient).AsNoTracking().FirstOrDefaultAsync(x => x.ExternalId == externalId && x.TenantId == tenantId, ct);
    }

    public Task AddAsync(Appointment appointment, CancellationToken ct = default)
    {
      _dbContext.Appointments.Add(appointment);
      return Task.CompletedTask;
    }

    public Task UpdateAsync(Appointment appointment, CancellationToken ct = default)
    {
      _dbContext.Appointments.Update(appointment);
      return Task.CompletedTask;
    }
  }
}