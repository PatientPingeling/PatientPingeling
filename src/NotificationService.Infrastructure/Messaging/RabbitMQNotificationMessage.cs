using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Messaging;

public sealed class RabbitMQNotificationmessage
{
    public required Patient patient {get;set;}
    public required Appointment appointment {get;set;}
    public required ProviderCredential providerCredential {get;set;}
    public required Tenant tenant {get;set;}
    public required ScheduledNotification scheduledNotification {get;set;}
}