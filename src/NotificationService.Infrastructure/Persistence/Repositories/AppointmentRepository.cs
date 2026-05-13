using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
  public class AppointmentRepository : IAppointmentRepository
  {
    public Task IngestAsync(Patient patient, Appointment appointment, IReadOnlyCollection<ScheduledNotification> notifications, CancellationToken ct = default)
    {
      throw new NotImplementedException();
    }
  }
}