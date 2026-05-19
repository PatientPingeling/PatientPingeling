using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence
{
    public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
    {
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<NotificationLog> NotificationLogs { get; set; }
        public DbSet<DispatchLog> DispatchLogs { get; set; }
        public DbSet<ProviderCredential> ProviderCredentials { get; set; }
        public DbSet<ScheduledNotification> ScheduledNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            modelBuilder.Entity<ProviderCredential>(entity =>
            {
                entity.Property(e => e.Key)
                    .HasMaxLength(256)
                    .IsRequired();

                entity.Property(e => e.EncryptedValue)
                    .IsRequired();
            });

            modelBuilder.Entity<ScheduledNotification>(entity =>
            {
                entity.HasIndex(e => e.SendAt);
            });

            modelBuilder.Entity<DispatchLog>(entity =>
            {
                entity.Property(e => e.Outcome)
                    .HasConversion<string>();

                entity.HasIndex(e => new { e.ScheduledNotificationId, e.AttemptedAt });
            });

            modelBuilder.Entity<NotificationLog>(entity =>
            {
                entity.HasIndex(e => new { e.TenantId, e.SentAt });
            });
        }
    }
}
