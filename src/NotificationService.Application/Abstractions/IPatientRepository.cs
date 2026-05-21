using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
    public interface IPatientRepository
    {
        Task<Patient?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Patient?> GetByExternalIdAsync(string externalId, Guid tenantId, CancellationToken ct = default);
        Task AddAsync(Patient patient, CancellationToken ct = default);
        Task UpdateAsync(Patient patient, CancellationToken ct = default);
        Task<int> AnonymizeStaleAsync(DateTimeOffset cutoff, CancellationToken ct = default);
    }
}