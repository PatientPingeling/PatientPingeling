namespace NotificationService.Domain.Entities
{
    public sealed class DispatchLog
    {
        public Guid Id { get; set; }
        public DateTimeOffset AttemptedAt { get; set; }
        public Outcome Outcome { get; set; }
        public int? HttpStatusCode { get; set; }
        public string? ExternalTrackingId { get; set; }

        public Guid ScheduledNotificationId { get; set; }
        public ScheduledNotification ScheduledNotification { get; set; } = null!;
    }

    public enum Outcome
    {
        NEW,
        SUCCESS,
        EXPIRED,
        CANCELLED,
        INSCHEDULER,
        INQUEUE,
        ERROR_TRANSIENT,
        ERROR_PERMANENT,
        PENDING_ASYNC
    }
}