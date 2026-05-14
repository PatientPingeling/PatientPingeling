using System.Text.Json.Serialization;

namespace NotificationService.Api.Contracts
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum AppointmentAction { CREATED, UPDATED, CANCELLED, UNKNOWN }

    internal sealed record AppointmentWebhookRequest(
        AppointmentAction Action,
        AppointmentPatientDto Patient,
        AppointmentDetailsDto Appointment
    );

    internal sealed record AppointmentPatientDto(
        string ExternalId,
        string GivenName,
        string? Email,        // nullable — provider determines which contact method is required
        string? PhoneNumber   // nullable — provider determines which contact method is required
    );

    internal sealed record AppointmentDetailsDto(
        string ExternalId,
        DateTimeOffset ScheduledAt,
        string? Service,
        string Location,
        string? Instructions
    );
}