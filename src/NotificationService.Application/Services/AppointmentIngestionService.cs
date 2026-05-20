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
    IUnitOfWork unitOfWork,
    ILogger<AppointmentIngestionService> logger) : IAppointmentIngestionService
  {
    private readonly IAppointmentRepository _appointmentRepository = appointmentRepository;
    private readonly IScheduledNotificationRepository _scheduledNotificationRepository = scheduledNotificationRepository;
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
        _logger.LogInformation("Skipping duplicate CREATED webhook for appointment {ExternalId}", command.Appointment.ExternalId);
        return Result.Failure(new Error("appointment.duplicate", "Appointment already exists.", ErrorType.Duplicate));
      }

      // TODO: @DanielvG-IT Add dispatchlogging "new" when doing this. maybe in updating also idk atm!

      var appointment = new Appointment
      {
        ExternalId = command.Appointment.ExternalId,
        Reason = command.Appointment.Service ?? string.Empty,
        Instructions = command.Appointment.Instructions,
        Location = command.Appointment.Location,
        ScheduledAt = command.Appointment.ScheduledAt.ToUniversalTime(),
        TenantId = command.TenantId,
        // If patient is existing (AsNoTracking), use FK only to avoid duplicate insert
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
      }, "persist.db_error", "persist appointment", ct);

      if (result.IsSuccess)
      {
        _logger.LogInformation("Appointment {ExternalId} created for tenant {TenantId}", command.Appointment.ExternalId, command.TenantId);
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
        _logger.LogWarning("Appointment {ExternalId} not found for tenant {TenantId}", command.Appointment.ExternalId, command.TenantId);
        return Result.Failure(new Error("appointment.not_found", "Appointment not found.", ErrorType.NotFound));
      }

      appointment.Patient.GivenName = command.Patient.GivenName;
      appointment.Patient.Email = command.Patient.Email ?? string.Empty;
      appointment.Patient.PhoneNumber = command.Patient.PhoneNumber ?? string.Empty;

      appointment.Reason = command.Appointment.Service ?? string.Empty;
      appointment.Instructions = command.Appointment.Instructions;
      appointment.Location = command.Appointment.Location;

      var oldScheduledAt = appointment.ScheduledAt;
      appointment.ScheduledAt = command.Appointment.ScheduledAt.ToUniversalTime();

      var result = await ExecuteInTransactionAsync(async () =>
      {
        await _patientRepository.UpdateAsync(appointment.Patient, ct); // TODO: only update if patient fields actually changed
        await _appointmentRepository.UpdateAsync(appointment, ct);

        if (oldScheduledAt != command.Appointment.ScheduledAt)
        {
          var notifications = CreateScheduledNotifications(appointment, appointment.ScheduledAt);
          await _scheduledNotificationRepository.DeletePendingByAppointmentIdAsync(appointment.Id, ct);
          await _scheduledNotificationRepository.AddRangeAsync(notifications, ct);
        }
      }, "update.db_error", "update appointment", ct);

      if (result.IsSuccess)
      {
        _logger.LogInformation("Appointment {ExternalId} updated for tenant {TenantId}", command.Appointment.ExternalId, command.TenantId);
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
        await _scheduledNotificationRepository.DeletePendingByAppointmentIdAsync(appointment.Id, ct);
        await _appointmentRepository.UpdateAsync(appointment, ct);
      }, "cancel.db_error", "cancel appointment", ct);

      if (result.IsSuccess)
      {
        _logger.LogInformation("Appointment {ExternalId} cancelled for tenant {TenantId}", command.Appointment.ExternalId, command.TenantId);
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
      return
      [
        new() { Id = Guid.CreateVersion7(), SendAt = scheduledAt.AddHours(-24), Appointment = appointment },
        new() { Id = Guid.CreateVersion7(), SendAt = scheduledAt.AddHours(-1), Appointment = appointment }
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
