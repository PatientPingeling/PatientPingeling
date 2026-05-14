using NotificationService.Application.Abstractions;

namespace NotificationService.Application.Services
{
    public class NotificationDispatchService(
        IMessageProviderFactory providerFactory,
        IScheduledNotificationRepository scheduledNotificationRepository,
        INotificationLogRepository notificationLogRepository,
        IEncryptionService encryptionService) : INotificationDispatchService
    {
        public Task DispatchAsync(Guid scheduledNotificationId, CancellationToken ct)
        {
            // 1. Load ScheduledNotification + Appointment + Patient + Tenant from DB
            // 2. Build message: "Beste {GivenName}, U heeft op {date} om {time} een afspraak bij {location}. {instructions}"
            // 3. Resolve provider: providerFactory.Create(tenant.Provider)
            // 4. Decrypt provider credentials via encryptionService
            // 5. Determine recipient (phone or email based on provider type)
            // 6. Call provider.SendAsync(message, recipient, credentials, ct)
            // 7. Write NotificationLog with ExternalMessageId + Succeeded flag
            throw new NotImplementedException();
        }
    }
}
