using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Api.Contracts;

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

        private static async Task<IResult> ReceiveAppointmentWebhook(
           [FromBody] AppointmentWebhookRequest request,
           [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
           [FromHeader(Name = "X-Api-Key")] string? apiKey,
           [FromServices] IValidator<AppointmentWebhookRequest> validator,
           CancellationToken ct
        )
        {
            if (tenantId == Guid.Empty)
                return TypedResults.Problem("Missing or invalid X-Tenant-Id header.", statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");

            if (string.IsNullOrWhiteSpace(apiKey))
                return TypedResults.Problem("Missing X-Api-Key header.", statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");

            // Log incoming webhook details
            Console.WriteLine($"Webhook received: Action={request.Action}, TenantId={tenantId}, PatientId={request.Patient.ExternalId}, AppointmentId={request.Appointment.ExternalId}");

            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return TypedResults.Problem(
                    detail: string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)),
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Failed"
                );
            }

            // TODO: replace with real ingestion service call once feat/webhook-implementation is merged

            return TypedResults.Created("/webhooks/appointments", new
            {
                received = true,
                action = request.Action.ToString(),
                tenantId,
                patientExternalId = request.Patient.ExternalId,
                appointmentExternalId = request.Appointment.ExternalId
            });
        }
    }
}
