using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
  public interface IAppointmentRepository
  {
    Task IngestAsync(Patient patient, Appointment appointment, IReadOnlyCollection<ScheduledNotification> notifications, CancellationToken ct = default);
  }
}