namespace NotificationService.Application.Abstractions
{
    public sealed record PendingAsyncDispatch(
        Guid DispatchLogId,
        Guid ScheduledNotificationId,
        string ExternalTrackingId,
        DateTimeOffset AttemptedAt,
        Guid TenantId);

    public sealed record AsyncFlowMessageStatus(
        string TrackingId,
        string Status,
        string? ErrorDetails);

    public interface IAsyncFlowStatusClient
    {
        Task<AsyncFlowMessageStatus?> GetStatusAsync(string trackingId, CancellationToken ct = default);
    }
}
