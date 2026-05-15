using NotificationService.Application.Commands;

namespace NotificationService.Api.Contracts
{
    public sealed record AppointmentWebhookRequest(
        AppointmentAction Action,
        AppointmentPatientDto Patient,
        AppointmentDetailsDto Appointment
    );

    public sealed record AppointmentPatientDto(
        string ExternalId,
        string GivenName,
        string? Email,        // nullable — provider determines which contact method is required
        string? PhoneNumber   // nullable — provider determines which contact method is required
    );

    public sealed record AppointmentDetailsDto(
        string ExternalId,
        DateTimeOffset ScheduledAt,
        string? Service,
        string Location,
        string? Instructions
    );
}
