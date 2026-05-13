using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
    public interface IPatientRepository
    {
        Task<Patient?> GetByIdAsync(int id, CancellationToken ct = default);
    }
}