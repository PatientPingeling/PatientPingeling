using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Domain;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Services
{
    public class AppointmentIngestionService(
      IAppointmentRepository appointmentRepository,
      IPatientRepository patientRepository,
      IScheduledNotificationRepository scheduledNotificationRepository,
      IDispatchLogRepository dispatchLogRepository,
      IUnitOfWork unitOfWork,
      ILogger<AppointmentIngestionService> logger) : IAppointmentIngestionService
    {
        private readonly IAppointmentRepository _appointmentRepository = appointmentRepository;
        private readonly IScheduledNotificationRepository _scheduledNotificationRepository = scheduledNotificationRepository;
        private readonly IDispatchLogRepository _dispatchLogRepository = dispatchLogRepository;
        private readonly IPatientRepository _patientRepository = patientRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<AppointmentIngestionService> _logger = logger;

        //! =================================
        //! ==      MAIN ENTRYPOINT        ==
        //! =================================
        public async Task<Result> IngestAsync(IngestAppointmentCommand command, CancellationToken ct = default)
        {
            if (command is null ||
                command.Patient is null ||
                command.Appointment is null ||
                string.IsNullOrWhiteSpace(command.Patient.ExternalId) ||
                string.IsNullOrWhiteSpace(command.Appointment.ExternalId))
            {
                return Result.Failure(new Error("command.invalid", "Command, patient, or appointment data is missing.", ErrorType.Validation));
            }

            return command.Action switch
            {
                AppointmentAction.CREATED => await HandleCreatedAsync(command, ct),
                AppointmentAction.UPDATED => await HandleUpdateAsync(command, ct),
                AppointmentAction.CANCELLED => await HandleCancelledAsync(command, ct),
                _ => Result.Failure(new Error("action.unknown", "Unknown appointment action.", ErrorType.Validation))
            };
        }

        //! =================================
        //! ==      ACTION HANDLERS        ==
        //! =================================
        private async Task<Result> HandleCreatedAsync(IngestAppointmentCommand command, CancellationToken ct)
        {
            var (patient, isNewPatient) = await ResolvePatientAsync(command, ct);
            if (patient is null)
            {
                return Result.Failure(new Error("patient.db_error", "Failed to retrieve or create patient.", ErrorType.Failure));
            }

            Appointment? existing;
            try
            {
                existing = await _appointmentRepository.GetByExternalIdAsync(command.Appointment.ExternalId, command.TenantId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check existing appointment {ExternalId}", command.Appointment.ExternalId);
                return Result.Failure(new Error("appointment.db_error", "Failed to check for existing appointment.", ErrorType.Failure));
            }

            if (existing is not null)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Skipping duplicate CREATED webhook for appointment {ExternalId}", command.Appointment.ExternalId);
                }
                return Result.Failure(new Error("appointment.duplicate", "Appointment already exists.", ErrorType.Duplicate));
            }

            return await PersistNewAppointmentAsync(command, patient, isNewPatient, ct);
        }

        private async Task<Result> PersistNewAppointmentAsync(IngestAppointmentCommand command, Patient patient, bool isNewPatient, CancellationToken ct)
        {
            patient.LastCommunicationAt = DateTimeOffset.UtcNow;

            var appointment = new Appointment
            {
                ExternalId = command.Appointment.ExternalId,
                Reason = command.Appointment.Service ?? string.Empty,
                Instructions = command.Appointment.Instructions,
                Location = command.Appointment.Location,
                ScheduledAt = command.Appointment.ScheduledAt.ToUniversalTime(),
                TenantId = command.TenantId,
                PatientId = isNewPatient ? 0 : patient.Id,
                Patient = isNewPatient ? patient : null!
            };

            var notifications = CreateScheduledNotifications(appointment, appointment.ScheduledAt);

            var result = await ExecuteInTransactionAsync(async () =>
            {
                if (isNewPatient)
                {
                    await _patientRepository.AddAsync(patient, ct);
                }

                await _appointmentRepository.AddAsync(appointment, ct);
                await _scheduledNotificationRepository.AddRangeAsync(notifications, ct);

                foreach (var n in notifications)
                {
                    await _dispatchLogRepository.AddAsync(new DispatchLog
                    {
                        Id = Guid.CreateVersion7(),
                        AttemptedAt = DateTimeOffset.UtcNow,
                        Outcome = Outcome.NEW,
                        ScheduledNotificationId = n.Id
                    }, ct);
                }
            }, "persist.db_error", "persist appointment", ct);

            if (result.IsSuccess)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Appointment {ExternalId} created for tenant {TenantId}", command.Appointment.ExternalId, command.TenantId);
                }
            }

            return result;
        }

        private async Task<Result> HandleUpdateAsync(IngestAppointmentCommand command, CancellationToken ct)
        {
            Appointment? appointment;
            try
            {
                appointment = await _appointmentRepository.GetByExternalIdAsync(command.Appointment.ExternalId, command.TenantId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve appointment {ExternalId}", command.Appointment.ExternalId);
                return Result.Failure(new Error("appointment.db_error", "Failed to retrieve appointment.", ErrorType.Failure));
            }

            if (appointment is null)
            {
                _logger.LogWarning("UPDATED webhook for unknown appointment {ExternalId} — upserting as new.", command.Appointment.ExternalId);

                var (patient, isNewPatient) = await ResolvePatientAsync(command, ct);
                if (patient is null)
                {
                    return Result.Failure(new Error("patient.db_error", "Failed to retrieve or create patient.", ErrorType.Failure));
                }

                return await PersistNewAppointmentAsync(command, patient, isNewPatient, ct);
            }

            var patientChanged = appointment.Patient.GivenName != command.Patient.GivenName
                || (command.Patient.Email is not null && appointment.Patient.Email != command.Patient.Email)
                || (command.Patient.PhoneNumber is not null && appointment.Patient.PhoneNumber != command.Patient.PhoneNumber);

            appointment.Patient.GivenName = command.Patient.GivenName;
            if (command.Patient.Email is not null)
            {
                appointment.Patient.Email = command.Patient.Email;
            }
            if (command.Patient.PhoneNumber is not null)
            {
                appointment.Patient.PhoneNumber = command.Patient.PhoneNumber;
            }
            appointment.Patient.LastCommunicationAt = DateTimeOffset.UtcNow;

            appointment.Reason = command.Appointment.Service ?? string.Empty;
            appointment.Instructions = command.Appointment.Instructions;
            appointment.Location = command.Appointment.Location;

            var oldScheduledAt = appointment.ScheduledAt;
            appointment.ScheduledAt = command.Appointment.ScheduledAt.ToUniversalTime();
            var timeChanged = oldScheduledAt != appointment.ScheduledAt;

            // Fetch pending IDs before the transaction so we can write CANCELLED logs inside it.
            // We intentionally do NOT delete the old ScheduledNotification rows: the FK is CASCADE,
            // so deleting them would also remove the CANCELLED DispatchLog entries we just wrote.
            // GetPendingAsync filters by latest dispatch log outcome, so CANCELLED rows are already
            // invisible to the scheduler without requiring a hard delete.
            IReadOnlyCollection<Guid> pendingIdsToCancel = timeChanged
                ? await _scheduledNotificationRepository.GetPendingIdsByAppointmentIdAsync(appointment.Id, ct)
                : [];

            var result = await ExecuteInTransactionAsync(async () =>
            {
                if (patientChanged)
                {
                    await _patientRepository.UpdateAsync(appointment.Patient, ct);
                }
                await _appointmentRepository.UpdateAsync(appointment, ct);

                if (timeChanged)
                {
                    foreach (var id in pendingIdsToCancel)
                    {
                        await _dispatchLogRepository.AddAsync(new DispatchLog
                        {
                            Id = Guid.CreateVersion7(),
                            AttemptedAt = DateTimeOffset.UtcNow,
                            Outcome = Outcome.CANCELLED,
                            ScheduledNotificationId = id
                        }, ct);
                    }

                    var notifications = CreateScheduledNotifications(appointment, appointment.ScheduledAt);
                    await _scheduledNotificationRepository.AddRangeAsync(notifications, ct);

                    foreach (var n in notifications)
                    {
                        await _dispatchLogRepository.AddAsync(new DispatchLog
                        {
                            Id = Guid.CreateVersion7(),
                            AttemptedAt = DateTimeOffset.UtcNow,
                            Outcome = Outcome.NEW,
                            ScheduledNotificationId = n.Id
                        }, ct);
                    }
                }
            }, "update.db_error", "update appointment", ct);

            if (result.IsSuccess)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Appointment {ExternalId} updated for tenant {TenantId}", command.Appointment.ExternalId, command.TenantId);
                }
            }

            return result;
        }

        private async Task<Result> HandleCancelledAsync(IngestAppointmentCommand command, CancellationToken ct)
        {
            Appointment? appointment;
            try
            {
                appointment = await _appointmentRepository.GetByExternalIdAsync(command.Appointment.ExternalId, command.TenantId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve appointment {ExternalId}", command.Appointment.ExternalId);
                return Result.Failure(new Error("appointment.db_error", "Failed to retrieve appointment.", ErrorType.Failure));
            }

            if (appointment is null)
            {
                _logger.LogWarning("Appointment {ExternalId} not found for tenant {TenantId} on CANCELLED", command.Appointment.ExternalId, command.TenantId);
                return Result.Failure(new Error("appointment.not_found", "Appointment not found.", ErrorType.NotFound));
            }

            appointment.IsCancelled = true;

            var result = await ExecuteInTransactionAsync(async () =>
            {
                var pendingIds = await _scheduledNotificationRepository.GetPendingIdsByAppointmentIdAsync(appointment.Id, ct);

                foreach (var id in pendingIds)
                {
                    await _dispatchLogRepository.AddAsync(new DispatchLog
                    {
                        Id = Guid.CreateVersion7(),
                        AttemptedAt = DateTimeOffset.UtcNow,
                        Outcome = Outcome.CANCELLED,
                        ScheduledNotificationId = id
                    }, ct);
                }

                // ScheduledNotification rows are intentionally kept for audit history.
                // IsCancelled = true prevents GetPendingAsync from re-queuing them.
                await _appointmentRepository.UpdateAsync(appointment, ct);
            }, "cancel.db_error", "cancel appointment", ct);

            if (result.IsSuccess)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Appointment {ExternalId} cancelled for tenant {TenantId}", command.Appointment.ExternalId, command.TenantId);
                }
            }

            return result;
        }

        //! =================================
        //! ==      HELPER METHODS         ==
        //! =================================
        private async Task<(Patient? patient, bool isNew)> ResolvePatientAsync(IngestAppointmentCommand command, CancellationToken ct)
        {
            try
            {
                var existing = await _patientRepository.GetByExternalIdAsync(command.Patient.ExternalId, command.TenantId, ct);
                if (existing is not null)
                {
                    return (existing, false);
                }

                return (new Patient
                {
                    ExternalId = command.Patient.ExternalId,
                    GivenName = command.Patient.GivenName,
                    Email = command.Patient.Email ?? string.Empty,
                    PhoneNumber = command.Patient.PhoneNumber ?? string.Empty,
                    TenantId = command.TenantId,
                }, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve patient {ExternalId}", command.Patient.ExternalId);
                return (null, false);
            }
        }

        private static ScheduledNotification[] CreateScheduledNotifications(Appointment appointment, DateTimeOffset scheduledAt)
        {
            var now = DateTimeOffset.UtcNow;
            var untilAppointment = scheduledAt - now;
            var sendAt24h = scheduledAt.AddHours(-24);
            var sendAt1h = scheduledAt.AddHours(-1);
            var immediateVsOneHourMargin = TimeSpan.FromMinutes(30);

            // Far in advance: preserve both reminder moments.
            if (untilAppointment > TimeSpan.FromHours(24))
            {
                return
                [
                  new() { Id = Guid.CreateVersion7(), SendAt = sendAt24h, Appointment = appointment },
          new() { Id = Guid.CreateVersion7(), SendAt = sendAt1h, Appointment = appointment }
                ];
            }

            // Last hour: only immediate reminder.
            if (untilAppointment <= TimeSpan.FromHours(1))
            {
                return
                [
                  new() { Id = Guid.CreateVersion7(), SendAt = now, Appointment = appointment }
                ];
            }

            // Between 24h and 1h: send now + 1h-before, unless these are too close.
            var oneHourReminderDelay = sendAt1h - now;
            if (oneHourReminderDelay <= immediateVsOneHourMargin)
            {
                return
                [
                  new() { Id = Guid.CreateVersion7(), SendAt = sendAt1h, Appointment = appointment }
                ];
            }

            return
            [
              new() { Id = Guid.CreateVersion7(), SendAt = now, Appointment = appointment },
        new() { Id = Guid.CreateVersion7(), SendAt = sendAt1h, Appointment = appointment }
            ];
        }

        // Wraps repo operations in a transaction — commit is always the last step
        private async Task<Result> ExecuteInTransactionAsync(Func<Task> work, string errorCode, string operationName, CancellationToken ct)
        {
            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await work();
                await _unitOfWork.CommitAsync(ct);
                return Result.Success();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to {Operation}", operationName);
                return Result.Failure(new Error(errorCode, $"Failed to {operationName}.", ErrorType.Failure));
            }
        }
    }
}
