namespace NotificationService.Domain.Entities
{
    public sealed class NotificationLog
    {
        public Guid Id { get; set; }
        public DateTimeOffset SentAt { get; set; }
        public string Provider { get; set; } = string.Empty;
        public bool Succeeded { get; set; }

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
    }
}
