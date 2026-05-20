using NotificationService.Application.Models;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Messaging;

public sealed class RabbitMQNotificationMessage
{
    public required Patient Patient { get; set; }
    public required Appointment Appointment { get; set; }
    public required ProviderCredential ProviderCredential { get; set; }
    public required Tenant Tenant { get; set; }
    public required ScheduledNotification ScheduledNotification { get; set; }

    public static RabbitMQNotificationMessage FromNotificationMessage(NotificationMessage message) => new()
    {
        Patient = message.Patient,
        Appointment = message.Appointment,
        ProviderCredential = message.ProviderCredential,
        Tenant = message.Tenant,
        ScheduledNotification = message.ScheduledNotification
    };
}