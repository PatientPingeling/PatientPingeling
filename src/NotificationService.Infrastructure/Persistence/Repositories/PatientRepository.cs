using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
    public class PatientRepository(NotificationDbContext dbContext) : IPatientRepository
    {
        private readonly NotificationDbContext _dbContext = dbContext;

        public async Task<Patient?> GetByExternalIdAsync(string externalId, CancellationToken ct = default)
        {
            return await _dbContext.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.ExternalId == externalId, ct);
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
    }
}