using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence
{
    public static class DevDataSeeder
    {
        private static readonly Guid DevTenantId = new("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        private const string DevApiKeyHash = "9caf06bb4436cdbfa20af9121a626bc1093c4f54b31c0fa937957856135345b6"; // API key: " " — SHA-256 hashed

        public static async Task SeedAsync(NotificationDbContext db)
        {
            if (await db.Tenants.AnyAsync())
            {
                return;
            }

            db.Tenants.Add(new Tenant
            {
                Id = DevTenantId,
                Name = "Dev Tenant",
                TimeZone = "Europe/Amsterdam",
                Provider = "SwiftSend",
                ApiKeyHash = DevApiKeyHash
            });

            await db.SaveChangesAsync();
        }
    }
}
