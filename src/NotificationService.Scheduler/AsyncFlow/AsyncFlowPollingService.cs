using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Scheduler.AsyncFlow
{
    public sealed class AsyncFlowPollingService(
        IServiceScopeFactory scopeFactory,
        ILogger<AsyncFlowPollingService> logger) : BackgroundService
    {
        private static readonly TimeSpan RunInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(5);
        private const int MaxPerCycle = 5;

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await PollPendingAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AsyncFlowPollingService failed.");
                }

                await Task.Delay(RunInterval, ct);
            }
        }

        private async Task PollPendingAsync(CancellationToken ct)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatchLogRepo = scope.ServiceProvider.GetRequiredService<IDispatchLogRepository>();
            var notificationLogRepo = scope.ServiceProvider.GetRequiredService<INotificationLogRepository>();
            var patientRepo = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
            var statusClient = scope.ServiceProvider.GetRequiredService<IAsyncFlowStatusClient>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var pending = await dispatchLogRepo.GetPendingAsyncFlowAsync(ct);
            if (pending.Count == 0)
                return;

            logger.LogInformation("AsyncFlow poll: checking {Count} pending notification(s).", pending.Count);

            foreach (var item in pending.Take(MaxPerCycle))
            {
                ct.ThrowIfCancellationRequested();

                var timedOut = DateTimeOffset.UtcNow - item.AttemptedAt > PendingTimeout;
                var status = await statusClient.GetStatusAsync(item.ExternalTrackingId, ct);

                if (status?.Status == "Completed")
                {
                    await dispatchLogRepo.AddAsync(new DispatchLog
                    {
                        Id = Guid.CreateVersion7(),
                        AttemptedAt = DateTimeOffset.UtcNow,
                        Outcome = Outcome.SUCCESS,
                        ExternalTrackingId = item.ExternalTrackingId,
                        ScheduledNotificationId = item.ScheduledNotificationId
                    }, ct);

                    await notificationLogRepo.AddAsync(new NotificationLog
                    {
                        Id = Guid.CreateVersion7(),
                        SentAt = DateTimeOffset.UtcNow,
                        Provider = "AsyncFlow",
                        ExternalMessageId = item.ExternalTrackingId,
                        Succeeded = true,
                        TenantId = item.TenantId
                    }, ct);

                    await patientRepo.UpdateLastCommunicationAsync(item.ScheduledNotificationId, ct);
                    await unitOfWork.CommitAsync();
                    logger.LogInformation("AsyncFlow notification {Id} confirmed delivered ({TrackingId}).", item.ScheduledNotificationId, item.ExternalTrackingId);
                }
                else if (status?.Status == "Failed" || (timedOut && status?.Status is "Queued" or "Processing" or null))
                {
                    var reason = timedOut && status?.Status is not "Failed" ? "timeout" : status?.ErrorDetails ?? "provider failure";

                    await dispatchLogRepo.AddAsync(new DispatchLog
                    {
                        Id = Guid.CreateVersion7(),
                        AttemptedAt = DateTimeOffset.UtcNow,
                        Outcome = Outcome.ERROR_TRANSIENT,
                        ScheduledNotificationId = item.ScheduledNotificationId
                    }, ct);

                    await unitOfWork.CommitAsync();
                    logger.LogWarning("AsyncFlow notification {Id} failed ({Reason}), marked for retry.", item.ScheduledNotificationId, reason);
                }
                else
                {
                    logger.LogDebug("AsyncFlow notification {Id} still {Status}.", item.ScheduledNotificationId, status?.Status ?? "unknown");
                }
            }
        }
    }
}
