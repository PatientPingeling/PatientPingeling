using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Api.Contracts;
using NotificationService.Api.Extensions;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Domain;

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
            [FromServices] IAppointmentIngestionService service,
            [FromServices] IValidator<AppointmentWebhookRequest> validator,
            [FromBody] AppointmentWebhookRequest request,
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            [FromHeader(Name = "X-Api-Key")] string? apiKey,
            CancellationToken ct
        )
        {
            if (tenantId == Guid.Empty)
            {
                return TypedResults.Problem("Missing or invalid X-Tenant-Id header.", statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return TypedResults.Problem("Missing X-Api-Key header.", statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
            }

            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return TypedResults.Problem(
                    detail: string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)),
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Failed");
            }

            var command = new IngestAppointmentCommand(
                request.Action,
                tenantId,
                new PatientInfo(
                    request.Patient.ExternalId,
                    request.Patient.GivenName,
                    request.Patient.Email,
                    request.Patient.PhoneNumber
                ),
                new AppointmentInfo(
                    request.Appointment.ExternalId,
                    request.Appointment.ScheduledAt,
                    request.Appointment.Service,
                    request.Appointment.Location,
                    request.Appointment.Instructions
                )
            );

            var result = await service.IngestAsync(command, ct);
            if (result.IsFailure)
            {
                return result.ToProblemDetails();
            }

            return TypedResults.Created();
        }
    }
}
