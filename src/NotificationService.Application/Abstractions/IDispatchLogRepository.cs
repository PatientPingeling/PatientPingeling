using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
    public interface IDispatchLogRepository
    {
        Task AddAsync(DispatchLog log, CancellationToken ct = default);
        Task<DispatchLog?> GetLatestStatusByScheduledApointmentIdASync(Guid scheduledNotificationId, CancellationToken ct = default);
        Task<IReadOnlyDictionary<Guid, DispatchLog?>> GetLatestStatusBatchAsync(IReadOnlyCollection<Guid> scheduledNotificationIds, CancellationToken ct = default);
        Task<IReadOnlyList<PendingAsyncDispatch>> GetPendingAsyncFlowAsync(CancellationToken ct = default);
        Task UpdateOutcomeAsync(Guid dispatchLogId, Outcome outcome, CancellationToken ct = default);
    }
}