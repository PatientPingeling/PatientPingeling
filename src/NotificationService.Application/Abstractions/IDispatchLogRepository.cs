using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions
{
    public interface IDispatchLogRepository
    {
        Task AddAsync(DispatchLog log, CancellationToken ct = default);
    }
}