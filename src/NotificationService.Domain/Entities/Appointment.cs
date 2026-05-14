namespace NotificationService.Domain.Entities
{
    public sealed class Appointment
    {
        public int Id { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public DateTimeOffset ScheduledAt { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public ICollection<ScheduledNotification> ScheduledNotifications { get; set; } = [];
    }
}
