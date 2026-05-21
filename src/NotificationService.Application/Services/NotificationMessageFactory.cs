using NotificationService.Application.Factories;
using NotificationService.Application.Models;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Services;

public sealed class NotificationMessageFactory(IScheduledNotificationRepository scheduledNotificationRepository, IDispatchLogRepository dispatchLogRepository) : INotificationMessageFactory
{
    private readonly IScheduledNotificationRepository _scheduledNotificationRepository = scheduledNotificationRepository;
    private readonly IDispatchLogRepository _dispatchLogRepository = dispatchLogRepository;

    public async Task<NotificationMessage[]> CreateAsync(ScheduledNotification[] scheduledNotifications, CancellationToken ct = default)
    {
        if (scheduledNotifications.Length == 0)
            return [];

        var ids = scheduledNotifications.Select(n => n.Id).ToList();

        // Single batch query replaces N individual dispatch-log lookups (PERF-1).
        var latestLogs = await _dispatchLogRepository.GetLatestStatusBatchAsync(ids, ct);

        var eligibleIds = ids
            .Where(id =>
            {
                latestLogs.TryGetValue(id, out var log);
                // No log yet, or latest outcome is NEW → eligible.
                return log is null || log.Outcome == Outcome.NEW;
            })
            .ToList();

        if (eligibleIds.Count == 0)
            return [];

        var messages = new List<NotificationMessage>(eligibleIds.Count);

        foreach (var id in eligibleIds)
        {
            ct.ThrowIfCancellationRequested();

            var detailedNotification = await _scheduledNotificationRepository.GetByIdWithDetailsAsync(id, ct);
            if (detailedNotification is null)
                continue;

            var appointment = detailedNotification.Appointment;
            var tenant = appointment.Tenant;
            var providerCredentials = tenant.Credentials?.ToArray() ?? [];

            if (providerCredentials.Length == 0)
                continue;

            messages.Add(new NotificationMessage
            {
                Patient = appointment.Patient,
                Appointment = appointment,
                ProviderCredentials = providerCredentials,
                Tenant = tenant,
                ScheduledNotification = detailedNotification
            });
        }

        return messages.ToArray();
    }
}