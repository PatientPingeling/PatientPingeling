using NotificationService.Application.Models;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Commands
{
    public sealed class RabbitMQNotificationMessage
    {
        public required Guid ScheduledNotificationId { get; set; }
        public required DateTimeOffset SendAt { get; set; }

        public required string PatientName { get; set; }
        public required string PatientEmail { get; set; }
        public required string PatientPhone { get; set; }

        public required string AppointmentReason { get; set; }
        public required string AppointmentLocation { get; set; }
        public string? AppointmentInstructions { get; set; }
        public required DateTimeOffset AppointmentScheduledAt { get; set; }

        public required Guid TenantId { get; set; }
        public required string TenantName { get; set; }
        public required string TenantTimeZone { get; set; }
        public required string Provider { get; set; }
        // TODO: replace ProviderCredential entity with a dedicated DTO (Key + EncryptedValue only) to avoid leaking EF navigation properties onto the queue (#56)
        public required ProviderCredential[] ProviderCredentials { get; set; }

        public DateTimeOffset EnqueuedAt { get; set; }

        public static RabbitMQNotificationMessage FromNotificationMessage(NotificationMessage message) => new()
        {
            ScheduledNotificationId = message.ScheduledNotification.Id,
            SendAt = message.ScheduledNotification.SendAt,

            PatientName = message.Patient.GivenName,
            PatientEmail = message.Patient.Email,
            PatientPhone = message.Patient.PhoneNumber,

            AppointmentReason = message.Appointment.Reason,
            AppointmentLocation = message.Appointment.Location,
            AppointmentInstructions = message.Appointment.Instructions,
            AppointmentScheduledAt = message.Appointment.ScheduledAt,

            TenantId = message.Tenant.Id,
            TenantName = message.Tenant.Name,
            TenantTimeZone = message.Tenant.TimeZone,
            Provider = message.Tenant.Provider,
            ProviderCredentials = message.ProviderCredentials,

            EnqueuedAt = DateTimeOffset.UtcNow
        };
    }
}