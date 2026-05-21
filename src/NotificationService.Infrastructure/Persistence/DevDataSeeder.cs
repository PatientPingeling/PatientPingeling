using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence
{
    public static class DevDataSeeder
    {
        private static readonly Guid SwiftSendTenantId  = new("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        private static readonly Guid SecurePostTenantId = new("4fa85f64-5717-4562-b3fc-2c963f66afa7");
        private static readonly Guid LegacyLinkTenantId = new("5fa85f64-5717-4562-b3fc-2c963f66afa8");
        private static readonly Guid AsyncFlowTenantId  = new("6fa85f64-5717-4562-b3fc-2c963f66afa9");
        private const string DevApiKey = "test-secret";

        public static async Task SeedAsync(NotificationDbContext db, IEncryptionService encryption, IHashingService hashing, ILogger logger)
        {
            if (await db.Tenants.AnyAsync())
            {
                logger.LogInformation("DevDataSeeder: tenants already exist, skipping seed.");
                return;
            }

            logger.LogInformation("DevDataSeeder: seeding dev tenants...");

            var apiKeyHash = hashing.Hash(DevApiKey);

            db.Tenants.Add(new Tenant
            {
                Id = SwiftSendTenantId,
                Name = "Dev Tenant (SwiftSend)",
                TimeZone = "Europe/Amsterdam",
                Provider = "SwiftSend",
                ApiKeyHash = apiKeyHash,
                Credentials =
                [
                    new ProviderCredential
                    {
                        Key = "ApiKey",
                        EncryptedValue = encryption.Encrypt("swiftsend-api-key"),
                        TenantId = SwiftSendTenantId
                    }
                ]
            });

            var securePostTenant = new Tenant
            {
                Id = SecurePostTenantId,
                Name = "Dev Tenant (SecurePost)",
                TimeZone = "Europe/Amsterdam",
                Provider = "SecurePost",
                ApiKeyHash = apiKeyHash,
                Credentials =
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
              ]
            };

            db.Tenants.Add(securePostTenant);

            db.Tenants.Add(new Tenant
            {
                Id = LegacyLinkTenantId,
                Name = "Dev Tenant (LegacyLink)",
                TimeZone = "Europe/Amsterdam",
                Provider = "LegacyLink",
                ApiKeyHash = apiKeyHash,
                Credentials =
                [
                    new ProviderCredential
                    {
                        Key = "Username",
                        EncryptedValue = encryption.Encrypt("legacylink-user"),
                        TenantId = LegacyLinkTenantId
                    },
                    new ProviderCredential
                    {
                        Key = "Password",
                        EncryptedValue = encryption.Encrypt("legacylink-password"),
                        TenantId = LegacyLinkTenantId
                    }
                ]
            });

            db.Tenants.Add(new Tenant
            {
                Id = AsyncFlowTenantId,
                Name = "Dev Tenant (AsyncFlow)",
                TimeZone = "Europe/Amsterdam",
                Provider = "AsyncFlow",
                ApiKeyHash = apiKeyHash,
                Credentials =
                [
                    new ProviderCredential
                    {
                        Key = "ApiKey",
                        EncryptedValue = encryption.Encrypt("asyncflow-api-key"),
                        TenantId = AsyncFlowTenantId
                    }
                ]
            });

            await db.SaveChangesAsync();

            logger.LogInformation("DevDataSeeder: seeded 4 tenants (SwiftSend, SecurePost, LegacyLink, AsyncFlow).");
        }
    }
}
