using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories
{
    public class ScheduledNotificationRepository(NotificationDbContext dbContext) : IScheduledNotificationRepository
    {
        private readonly NotificationDbContext _dbContext = dbContext;


        public async Task<ScheduledNotification?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        {
            return await _dbContext.ScheduledNotifications
                .Include(n => n.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(n => n.Appointment)
                    .ThenInclude(a => a.Tenant)
                        .ThenInclude(t => t.Credentials)
                .AsSplitQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == id, ct);
        }

        public Task AddAsync(ScheduledNotification notification, CancellationToken ct = default)
        {
            _dbContext.ScheduledNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IReadOnlyCollection<ScheduledNotification> notifications, CancellationToken ct = default)
        {
            _dbContext.ScheduledNotifications.AddRange(notifications);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyCollection<Guid>> GetPendingIdsByAppointmentIdAsync(int appointmentId, CancellationToken ct = default)
        {
            return await _dbContext.ScheduledNotifications
              .Where(s => s.AppointmentId == appointmentId)
              .Where(s => !_dbContext.DispatchLogs.Any(d => d.ScheduledNotificationId == s.Id && d.Outcome == Outcome.SUCCESS))
              .Select(s => s.Id)
              .ToListAsync(ct);
        }

        public async Task<int> DeletePendingByAppointmentIdAsync(int appointmentId, CancellationToken ct = default)
        {
            // SELECT FOR UPDATE SKIP LOCKED — locks rows so the Scheduler cannot concurrently
            // pick them for dispatch between our read and the SaveChanges commit.
            var toDelete = await _dbContext.ScheduledNotifications
              .FromSqlRaw("""
                      SELECT *
                      FROM "ScheduledNotifications" s
                      WHERE s."AppointmentId" = {0}
                      AND NOT EXISTS (
                        SELECT 1
                        FROM "DispatchLogs" d
                        WHERE d."ScheduledNotificationId" = s."Id"
                        AND d."Outcome" = 'SUCCESS'
                      )
                      FOR UPDATE SKIP LOCKED
                      """, appointmentId)
              .ToListAsync(ct);

            _dbContext.ScheduledNotifications.RemoveRange(toDelete);
            return toDelete.Count;
        }

        public async Task<IReadOnlyCollection<ScheduledNotification>> GetPendingAsync(DateTimeOffset before, CancellationToken ct = default)
        {
            return await _dbContext.ScheduledNotifications
              .FromSqlRaw("""
                      SELECT *
                      FROM "ScheduledNotifications" s
                      WHERE s."SendAt" <= {0}
                      AND EXISTS (
                        SELECT 1
                        FROM "Appointments" a
                        WHERE a."Id" = s."AppointmentId"
                        AND a."IsCancelled" = FALSE
                      )
                      AND EXISTS (
                        SELECT 1
                        FROM "DispatchLogs" d
                        WHERE d."ScheduledNotificationId" = s."Id"
                          AND d."AttemptedAt" = (
                            SELECT MAX(d2."AttemptedAt")
                            FROM "DispatchLogs" d2
                            WHERE d2."ScheduledNotificationId" = s."Id"
                          )
                          AND (
                            d."Outcome" IN ('NEW', 'EXPIRED', 'ERROR_429')
                            OR (d."Outcome" = 'INSCHEDULER' AND d."AttemptedAt" < NOW() - INTERVAL '5 minutes')
                          )
                      )
                      ORDER BY s."SendAt"
                      LIMIT 50
                      FOR UPDATE SKIP LOCKED
                      """, before)
              .ToListAsync(ct);
        }
    }
}
