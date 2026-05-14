namespace NotificationService.Application.Commands
{
    public sealed record IngestAppointmentCommand(
        AppointmentAction Action,
        Guid TenantId,
        PatientInfo Patient,
        AppointmentInfo Appointment
    );

    public enum AppointmentAction { CREATED, UPDATED, CANCELLED, UNKNOWN }

    public sealed record PatientInfo(
        string ExternalId,
        string GivenName,
        string? Email,
        string? PhoneNumber
    );

    public sealed record AppointmentInfo(
        string ExternalId,
        DateTimeOffset ScheduledAt,
        string? Service,
        string Location,
        string? Instructions
    );
}
