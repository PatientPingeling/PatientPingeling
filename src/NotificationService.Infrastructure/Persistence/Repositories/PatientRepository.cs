using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
    public class PatientRepository : IPatientRepository
    {
        public Task<Patient?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}