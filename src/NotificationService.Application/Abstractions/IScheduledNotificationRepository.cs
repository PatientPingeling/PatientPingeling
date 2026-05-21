using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
    public interface IScheduledNotificationRepository
    {
        Task<ScheduledNotification?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyCollection<ScheduledNotification>> GetPendingAsync(DateTimeOffset before, CancellationToken ct = default);
        Task AddAsync(ScheduledNotification notification, CancellationToken ct = default);
        Task AddRangeAsync(IReadOnlyCollection<ScheduledNotification> notifications, CancellationToken ct = default);
        Task<IReadOnlyCollection<Guid>> GetPendingIdsByAppointmentIdAsync(int appointmentId, CancellationToken ct = default);
        Task<int> DeletePendingByAppointmentIdAsync(int appointmentId, CancellationToken ct = default);
    }
}
