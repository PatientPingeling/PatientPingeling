using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence
{
    public static class DevDataSeeder
    {
        // API key: "test-secret" — SHA-256 hashed
        private static readonly Guid DevTenantId = new("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        private static readonly Guid SecurePostTenantId = new("4fa85f64-5717-4562-b3fc-2c963f66afa7");
        private const string DevApiKeyHash = "9caf06bb4436cdbfa20af9121a626bc1093c4f54b31c0fa937957856135345b6";

        public static async Task SeedAsync(NotificationDbContext db, IEncryptionService encryption)
        {
            if (await db.Tenants.AnyAsync())
            {
                return;
            }

            // Dev tenant — SwiftSend (placeholder, not yet implemented)
            db.Tenants.Add(new Tenant
            {
                Id = DevTenantId,
                Name = "Dev Tenant (SwiftSend)",
                TimeZone = "Europe/Amsterdam",
                Provider = "SwiftSend",
                ApiKeyHash = DevApiKeyHash
            });

            // SecurePost tenant
            var securePostTenant = new Tenant
            {
                Id = SecurePostTenantId,
                Name = "Dev Tenant (SecurePost)",
                TimeZone = "Europe/Amsterdam",
                Provider = "SecurePost",
                ApiKeyHash = DevApiKeyHash
            };

            securePostTenant.Credentials =
            [
                new ProviderCredential
                {
                    Key = "ClientId",
                    EncryptedValue = encryption.Encrypt("securepost-client-id"),
                    TenantId = SecurePostTenantId
                },
                new ProviderCredential
                {
                    Key = "ClientSecret",
                    EncryptedValue = encryption.Encrypt("securepost-secret-key"),
                    TenantId = SecurePostTenantId
                }
            ];

            db.Tenants.Add(securePostTenant);

            await db.SaveChangesAsync();
        }
    }
}
