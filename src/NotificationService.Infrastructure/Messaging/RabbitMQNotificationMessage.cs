using NotificationService.Application.Models;

namespace NotificationService.Infrastructure.Messaging;

public sealed class RabbitMqProviderCredential
{
    public required string Key { get; init; }
    public required string EncryptedValue { get; init; }
}

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

    // Provider credentials are included as *encrypted* values.
    // The consumer is expected to decrypt them before calling the provider.
    public required RabbitMqProviderCredential[] ProviderCredentials { get; set; }

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
        Provider = message.Tenant.Provider,

        ProviderCredentials = message.ProviderCredentials
            .OrderBy(c => c.Key)
            .Select(c => new RabbitMqProviderCredential { Key = c.Key, EncryptedValue = c.EncryptedValue })
            .ToArray()
    };
}