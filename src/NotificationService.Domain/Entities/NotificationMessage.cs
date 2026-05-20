using NotificationService.Domain.Entities;

namespace NotificationService.Application.Models
{
    public sealed class NotificationMessage
    {
        public required Patient Patient { get; set; }
        public required Appointment Appointment { get; set; }
        public required ProviderCredential[] ProviderCredentials { get; set; }
        public required Tenant Tenant { get; set; }
        public required ScheduledNotification ScheduledNotification { get; set; }
    }
}
