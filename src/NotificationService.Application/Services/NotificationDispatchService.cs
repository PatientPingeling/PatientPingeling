using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Domain;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Services
{
    public class NotificationDispatchService(
        IEncryptionService encryptionService,
        IMessageProviderFactory providerFactory,
        ILogger<NotificationDispatchService> logger,
        IScheduledNotificationRepository scheduledNotificationRepository) : INotificationDispatchService
    {
        private readonly ILogger<NotificationDispatchService> _logger = logger;
        private readonly IEncryptionService _encryptionService = encryptionService;
        private readonly IMessageProviderFactory _providerFactory = providerFactory;
        private readonly IScheduledNotificationRepository _scheduledNotificationRepository = scheduledNotificationRepository;

        public async Task<Result<string>> DispatchAsync(Guid scheduledNotificationId, CancellationToken ct)
        {
            // Load ScheduledNotification + Appointment + Patient + Tenant from DB
            ScheduledNotification? notification;
            try
            {
                notification = await _scheduledNotificationRepository.GetByIdWithDetailsAsync(scheduledNotificationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load scheduled notification {Id} from database.", scheduledNotificationId);
                return Result<string>.Failure(new Error("notification.db_error", "Failed to retrieve scheduled notification from database.", ErrorType.Failure));
            }
            if (notification is null)
            {
                return Result<string>.Failure(new Error("notification.not_found", "Scheduled notification not found.", ErrorType.NotFound));
            }

            var appointment = notification.Appointment;
            var patient = appointment.Patient;
            var tenant = appointment.Tenant;

            var provider = _providerFactory.Create(tenant.Provider);

            // Build message
            var resolved = ResolveFormatAndRecipient(provider, patient);
            if (resolved is null)
            {
                return Result<string>.Failure(new Error("notification.no_contact", "No supported contact method available for this patient.", ErrorType.Validation)); // TODO Reduce failure as much as humanly possible!
            }
            var (format, recipient) = resolved.Value;

            var message = BuildMessage(format, patient, appointment, tenant);

            // Decrypt provider credentials 🤯
            Dictionary<string, string> decryptedCreds;
            try
            {
                decryptedCreds = tenant.Credentials.ToDictionary(
                    c => c.Key,
                    c => _encryptionService.Decrypt(c.EncryptedValue)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt credentials for tenant {TenantId}.", tenant.Id);
                return Result<string>.Failure(new Error("credentials.decrypt_error", "Failed to decrypt provider credentials.", ErrorType.Failure));
            }

            // Call provider to send message
            try
            {
                var externalMessageId = await provider.SendAsync(format, message, recipient, decryptedCreds, ct);
                return Result<string>.Success(externalMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provider {Provider} failed to send notification {Id}.", tenant.Provider, scheduledNotificationId);
                return Result<string>.Failure(new Error("provider.send_error", "The message provider failed to deliver the notification.", ErrorType.Failure));
            }
        }

        private static (MessageFormat format, string recipient)? ResolveFormatAndRecipient(IMessageProvider provider, Patient patient)
        {
            foreach (var format in provider.SupportedFormats)
            {
                var recipient = format switch
                {
                    MessageFormat.Email => patient.Email,
                    MessageFormat.Sms => patient.PhoneNumber,
                    MessageFormat.Push => patient.PhoneNumber,
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(recipient) is false)
                {
                    return (format, recipient);
                }
            }

            return null;
        }

        private static string BuildMessage(MessageFormat format, Patient patient, Appointment appointment, Tenant tenant) => format switch
        {
            MessageFormat.Push => BuildPushMessage(appointment),
            MessageFormat.Sms => BuildSmsMessage(patient, appointment),
            MessageFormat.Email => BuildEmailMessage(patient, appointment, tenant),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        private static string BuildEmailMessage(Patient patient, Appointment appointment, Tenant tenant)
        {
            return $"""
            Beste {patient.GivenName},

            U heeft op {appointment.ScheduledAt:dddd d MMMM yyyy} om {appointment.ScheduledAt:HH:mm} een afspraak bij {appointment.Location}. {appointment.Instructions}

            Met vriendelijke groet,
            {tenant.Name}
            """;
        }

        private static string BuildSmsMessage(Patient patient, Appointment appointment)
        {
            return $"Beste {patient.GivenName}, u heeft op {appointment.ScheduledAt:d MMMM} om {appointment.ScheduledAt:HH:mm} een afspraak bij {appointment.Location}. {appointment.Instructions}";
        }

        private static string BuildPushMessage(Appointment appointment)
        {
            return $"Afspraak herinnering: {appointment.ScheduledAt:d MMM} {appointment.ScheduledAt:HH:mm} - {appointment.Location}";
        }
    }
}
