namespace NotificationService.Domain.Entities
{
    public sealed class Tenant
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TimeZone { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;

        public ICollection<ProviderCredential> Credentials { get; set; } = [];
    }
}
