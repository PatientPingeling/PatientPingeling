using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
  public interface IAppointmentRepository
  {
    Task<Appointment?> GetByExternalIdAsync(string externalId, CancellationToken ct = default);
    Task AddAsync(Appointment appointment, CancellationToken ct = default);
    Task UpdateAsync(Appointment appointment, CancellationToken ct = default);

  }
}