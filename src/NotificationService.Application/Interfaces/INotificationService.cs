using NotificationService.Domain;

namespace NotificationService.Application.Interfaces
{
    public interface INotificationService
    {
        Task<Result> ProcessNotificationAsync(string message);
    }
}