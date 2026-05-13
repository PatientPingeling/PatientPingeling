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
            // group.MapPost("/patients", ReceivePatientWebhook);

            return app;
        }

        private static IResult ReceiveAppointmentWebhook(IAppointmentIngestionService appointmentIngestionService, [FromBody] AppointmentWebhookRequest request)
        {
            if (request is null)
            {
                return TypedResults.BadRequest();
            }

            // Process webhook

            return TypedResults.Ok();
        }
    }

    internal sealed record AppointmentWebhookRequest(string Name, string ContactDetail, AppointmentDetails Appointment);
    internal sealed record AppointmentDetails(DateTime AppointmentDateTime, string AppointmentReason, string AppointmentLocation);
}