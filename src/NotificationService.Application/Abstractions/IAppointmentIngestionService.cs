using NotificationService.Application.Commands;
using NotificationService.Domain;

namespace NotificationService.Application.Abstractions
{
    public interface IAppointmentIngestionService
    {
        Task<Result> IngestAsync(IngestAppointmentCommand command, CancellationToken ct = default);
    }
}