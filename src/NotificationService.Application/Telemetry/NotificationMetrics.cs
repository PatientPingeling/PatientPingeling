using System.Diagnostics.Metrics;

namespace NotificationService.Application.Telemetry
{
    public static class NotificationMetrics
    {
        public const string MeterName = "PatientPingeling";

        private static readonly Meter _meter = new(MeterName, "1.0.0");

        // Incremented in NotificationDispatchService after each provider call.
        // Tags: provider (SwiftSend / SecurePost / ...), outcome (success / no_contact / failure)
        public static readonly Counter<long> NotificationsDispatched =
            _meter.CreateCounter<long>(
                "patientpingeling.notifications.dispatched",
                "notifications",
                "Notifications sent to a message provider, tagged by provider and outcome.");

        // Incremented in PollAction when a notification is successfully published to RabbitMQ.
        public static readonly Counter<long> NotificationsEnqueued =
            _meter.CreateCounter<long>(
                "patientpingeling.notifications.enqueued",
                "notifications",
                "Notifications picked up by the scheduler and published to the queue.");
    }
}
