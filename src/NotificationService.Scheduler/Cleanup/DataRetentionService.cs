using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;

namespace NotificationService.Scheduler.Cleanup
{
    public sealed class DataRetentionService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataRetentionService> logger) : BackgroundService
    {
        private static readonly TimeSpan PatientRetention = TimeSpan.FromDays(14);
        private static readonly TimeSpan NotificationLogRetention = TimeSpan.FromDays(365);
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await RunRetentionPolicies(ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "DataRetentionService failed.");
                }

                await Task.Delay(RunInterval, ct);
            }
        }

        private async Task RunRetentionPolicies(CancellationToken ct)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
            var notificationLogRepo = scope.ServiceProvider.GetRequiredService<INotificationLogRepository>();

            var patientCutoff = DateTimeOffset.UtcNow - PatientRetention;
            var deletedPatients = await patientRepo.DeleteStaleAsync(patientCutoff, ct);
            if (deletedPatients > 0)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Retention: deleted {Count} patient(s) and related data inactive since {Cutoff:O}.", deletedPatients, patientCutoff);
                }
            }

            var logCutoff = DateTimeOffset.UtcNow - NotificationLogRetention;
            var deletedLogs = await notificationLogRepo.DeleteOlderThanAsync(logCutoff, ct);
            if (deletedLogs > 0)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Retention: deleted {Count} notification log(s) older than {Cutoff:O}.", deletedLogs, logCutoff);
                }
            }
        }
    }
}
