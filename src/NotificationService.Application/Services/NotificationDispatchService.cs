using Microsoft.Extensions.Logging;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Domain;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Services
{
    public class NotificationDispatchService(
        IEncryptionService encryptionService,
        IMessageProviderFactory providerFactory,
        ILogger<NotificationDispatchService> logger) : INotificationDispatchService
    {
        private readonly ILogger<NotificationDispatchService> _logger = logger;
        private readonly IEncryptionService _encryptionService = encryptionService;
        private readonly IMessageProviderFactory _providerFactory = providerFactory;

        public async Task<Result<string>> DispatchAsync(RabbitMQNotificationMessage notificationMessage, CancellationToken ct)
        {
            // TODO: validate notificationMessage.Provider against tenant's actual Provider in DB using TenantId to prevent provider spoofing (security issue #56)
            var provider = _providerFactory.Create(notificationMessage.Provider);

            var resolved = ResolveFormatAndRecipient(provider, notificationMessage);
            if (resolved is null)
            {
                return Result<string>.Failure(new Error("notification.no_contact", "No supported contact method available for this patient.", ErrorType.Validation));
            }
            var (format, recipient) = resolved.Value;

            var message = BuildMessage(format, notificationMessage);

            Dictionary<string, string> decryptedCreds;
            try
            {
                decryptedCreds = notificationMessage.ProviderCredentials.ToDictionary(
                    c => c.Key,
                    c => _encryptionService.Decrypt(c.EncryptedValue)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt credentials for tenant {TenantId}.", notificationMessage.TenantId);
                return Result<string>.Failure(new Error("credentials.decrypt_error", "Failed to decrypt provider credentials.", ErrorType.Failure));
            }

            try
            {
                var externalMessageId = await provider.SendAsync(format, message, recipient, decryptedCreds, ct);
                return Result<string>.Success(externalMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provider {Provider} failed to send notification {Id}.", notificationMessage.Provider, notificationMessage.ScheduledNotificationId);
                return Result<string>.Failure(new Error("provider.send_error", "The message provider failed to deliver the notification.", ErrorType.Failure));
            }
        }

        private static (MessageFormat format, string recipient)? ResolveFormatAndRecipient(IMessageProvider provider, RabbitMQNotificationMessage msg)
        {
            foreach (var format in provider.SupportedFormats)
            {
                var recipient = format switch
                {
                    MessageFormat.Email => msg.PatientEmail,
                    MessageFormat.Sms => msg.PatientPhone,
                    MessageFormat.Push => msg.PatientPhone,
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(recipient) is false)
                    return (format, recipient);
            }

            return null;
        }

        private static string BuildMessage(MessageFormat format, RabbitMQNotificationMessage msg) => format switch
        {
            MessageFormat.Push => BuildPushMessage(msg),
            MessageFormat.Sms => BuildSmsMessage(msg),
            MessageFormat.Email => BuildEmailMessage(msg),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        private static string BuildEmailMessage(RabbitMQNotificationMessage msg)
        {
            var localAppointmentTime = ToTenantLocalTime(msg.AppointmentScheduledAt, msg.TenantTimeZone);
            return $"""
            Beste {msg.PatientName},

            U heeft op {localAppointmentTime:dddd d MMMM yyyy} om {localAppointmentTime:HH:mm} een afspraak bij {msg.AppointmentLocation}. {msg.AppointmentInstructions}

            Met vriendelijke groet,
            {msg.Provider}
            """;
            // TODO: replace {msg.Provider} with tenant display name — add TenantName field to RabbitMQNotificationMessage (#56)
        }

        private static string BuildSmsMessage(RabbitMQNotificationMessage msg)
        {
            var localAppointmentTime = ToTenantLocalTime(msg.AppointmentScheduledAt, msg.TenantTimeZone);
            return $"Beste {msg.PatientName}, u heeft op {localAppointmentTime:d MMMM} om {localAppointmentTime:HH:mm} een afspraak bij {msg.AppointmentLocation}. {msg.AppointmentInstructions}";
        }

        private static string BuildPushMessage(RabbitMQNotificationMessage msg)
        {
            var localAppointmentTime = ToTenantLocalTime(msg.AppointmentScheduledAt, msg.TenantTimeZone);
            return $"Afspraak herinnering: {localAppointmentTime:d MMM} {localAppointmentTime:HH:mm} - {msg.AppointmentLocation}";
        }

        // Converts the absolute appointment time to the tenant's local wall-clock time so
        // patients read "14:00" in their own timezone instead of the UTC moment.
        // Falls back to UTC if the tenant's TimeZone is missing or unknown on this host.
        private static DateTimeOffset ToTenantLocalTime(DateTimeOffset utcMoment, string tenantTimeZone)
        {
            if (string.IsNullOrWhiteSpace(tenantTimeZone)) return utcMoment;

            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tenantTimeZone);
                return TimeZoneInfo.ConvertTime(utcMoment, tz);
            }
            catch (TimeZoneNotFoundException)
            {
                return utcMoment;
            }
            catch (InvalidTimeZoneException)
            {
                return utcMoment;
            }
        }
    }
}
