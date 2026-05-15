namespace NotificationService.Domain.Entities
{
    public sealed class Patient
    {
        public int Id { get; set; }
        public string GivenName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public ICollection<Appointment> Appointments { get; set; } = [];


        // TODO: Fix this!
        public DateTimeOffset LastCommunicationAt { get; set; }
    }
}
