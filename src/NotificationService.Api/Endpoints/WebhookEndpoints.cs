using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Services;

namespace NotificationService.Api.Endpoints
{
    internal static class WebhookEndpoints
    {
        internal static WebApplication MapWebhookEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/webhooks");

            group.MapPost("/appointments", ReceiveAppointmentWebhook);

            return app;
        }

        private static IResult ReceiveAppointmentWebhook(IAppointmentIngestionService appointmentIngestionService, [FromBody] AppointmentWebhookRequest request)
        {
            if (request is null)
                return TypedResults.BadRequest();

            // Process webhook

            return TypedResults.Ok();
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    internal enum AppointmentAction { CREATED, UPDATED, CANCELLED, UNKNOWN }

    internal sealed record AppointmentWebhookRequest(
        AppointmentAction Action,
        string TenantId,
        AppointmentPatientDto Patient,
        AppointmentDetailsDto Appointment
    );

    internal sealed record AppointmentPatientDto(
        string ExternalId,
        string GivenName,
        string Email,
        string PhoneNumber
    );

    internal sealed record AppointmentDetailsDto(
        string ExternalId,          // Appointment UUID from source system — idempotency key
        DateTimeOffset ScheduledAt, // Timezone-aware — critical for 24h/1h scheduling
        string? Service,
        string Location,
        string? Instructions
    );
}