using NotificationService.Application.Abstractions;

namespace NotificationService.Scheduler.Cleanup
{
    public sealed class PatientDataCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<PatientDataCleanupService> logger) : BackgroundService
    {
        private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(14);
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await AnonymizeStalePatients(ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "PatientDataCleanupService failed.");
                }

                await Task.Delay(RunInterval, ct);
            }
        }

        private async Task AnonymizeStalePatients(CancellationToken ct)
        {
            var cutoff = DateTimeOffset.UtcNow - RetentionWindow;

            await using var scope = scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();

            var count = await repo.AnonymizeStaleAsync(cutoff, ct);

            if (count > 0)
                logger.LogInformation("GDPR cleanup: anonymized {Count} patient(s) with no activity since {Cutoff:O}.", count, cutoff);
            else
                logger.LogDebug("GDPR cleanup: no stale patients found (cutoff {Cutoff:O}).", cutoff);
        }
    }
}
