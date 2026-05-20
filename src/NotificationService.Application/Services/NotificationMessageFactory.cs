using NotificationService.Application.Factories;
using NotificationService.Application.Models;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Messaging;

public sealed class NotificationMessageFactory(IScheduledNotificationRepository scheduledNotificationRepository, IDispatchLogRepository dispatchLogRepository) : INotificationMessageFactory
{
    private readonly IScheduledNotificationRepository _scheduledNotificationRepository = scheduledNotificationRepository;
    private readonly IDispatchLogRepository _dispatchLogRepository = dispatchLogRepository;

    public async Task<NotificationMessage[]> CreateAsync(ScheduledNotification[] scheduledNotifications, CancellationToken ct = default)
    {
        if (scheduledNotifications.Length == 0)
        {
            return [];
        }

        var messages = new List<NotificationMessage>(scheduledNotifications.Length);

        foreach (var scheduledNotification in scheduledNotifications)
        {
            ct.ThrowIfCancellationRequested();

            var latestDispatchLog = await _dispatchLogRepository
                .GetLatestStatusByScheduledApointmentIdASync(scheduledNotification.Id, ct);

            // Only create a message when the latest outcome is NEW.
            // If there's no dispatch log yet, we treat it as still eligible (i.e. not attempted).
            if (latestDispatchLog is not null && latestDispatchLog.Outcome != Outcome.NEW)
            {
                continue;
            }

            // Fetch the full object graph from the DB:
            // ScheduledNotification -> Appointment -> Patient + Tenant (+ Credentials)
            var detailedNotification = await _scheduledNotificationRepository.GetByIdWithDetailsAsync(scheduledNotification.Id, ct);
            if (detailedNotification is null)
            {
                continue;
            }

            var appointment = detailedNotification.Appointment;
            var patient = appointment.Patient;
            var tenant = appointment.Tenant;

            var providerCredentials = tenant.Credentials?.ToArray() ?? [];
            if (providerCredentials.Length == 0)
            {
                continue;
            }

            messages.Add(new NotificationMessage
            {
                Patient = patient,
                Appointment = appointment,
                ProviderCredentials = providerCredentials,
                Tenant = tenant,
                ScheduledNotification = detailedNotification
            });
        }

        return messages.ToArray();
    }
}