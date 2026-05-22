using NotificationService.Application.Models;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Factories;

public interface INotificationMessageFactory
{
    Task<NotificationMessage[]> CreateAsync(ScheduledNotification[] scheduledNotifications, CancellationToken ct = default);
}