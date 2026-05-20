using NotificationService.Application.Models;

namespace NotificationService.Infrastructure.Messaging;

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
    public required string Provider { get; set; }

    // Set when the message is published to RabbitMQ.
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
        Provider = message.Tenant.Provider
    };
}