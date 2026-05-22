using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
    public class PatientRepository(NotificationDbContext dbContext) : IPatientRepository
    {
        private readonly NotificationDbContext _dbContext = dbContext;

        public async Task<Patient?> GetByExternalIdAsync(string externalId, Guid tenantId, CancellationToken ct = default)
        {
            return await _dbContext.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.ExternalId == externalId && x.TenantId == tenantId, ct);
        }

        public async Task<Patient?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _dbContext.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task AddAsync(Patient patient, CancellationToken ct = default)
        {
            _dbContext.Patients.Add(patient);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Patient patient, CancellationToken ct = default)
        {
            _dbContext.Patients.Update(patient);
            return Task.CompletedTask;
        }

        public async Task UpdateLastCommunicationAsync(Guid scheduledNotificationId, CancellationToken ct = default)
        {
            await _dbContext.Patients
                .Where(p => p.Appointments.Any(a =>
                    a.ScheduledNotifications.Any(n => n.Id == scheduledNotificationId)))
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(p => p.LastCommunicationAt, DateTimeOffset.UtcNow), ct);
        }

        public async Task<int> DeleteStaleAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;

            return await _dbContext.Patients
                .Where(p => p.LastCommunicationAt < cutoff
                            && !p.Appointments.Any(a =>
                                !a.IsCancelled &&
                                a.ScheduledNotifications.Any(n => n.SendAt > now)))
                .ExecuteDeleteAsync(ct);
        }
    }
}