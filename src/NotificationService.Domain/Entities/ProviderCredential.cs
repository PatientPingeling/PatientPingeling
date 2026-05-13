namespace NotificationService.Domain.Entities
{
    public sealed class ProviderCredential
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string EncryptedValue { get; set; } = string.Empty;

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
    }
}
