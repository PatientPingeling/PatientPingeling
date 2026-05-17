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
            app.MapGroup("/webhooks").MapPost("/appointments", ReceiveAppointmentWebhook);
            return app;
        }

        private static async Task<IResult> ReceiveAppointmentWebhook(
            [FromServices] ITenantService tenantService,
            [FromServices] IAppointmentIngestionService ingestionService,
            [FromServices] IValidator<AppointmentWebhookRequest> validator,
            [FromServices] ILoggerFactory loggerFactory,
            [FromBody] AppointmentWebhookRequest request,
            [FromHeader(Name = "X-Tenant-Id")] Guid? tenantId,
            [FromHeader(Name = "X-Api-Key")] string? apiKey,
            CancellationToken ct
        )
        {
            if (tenantId is null || tenantId == Guid.Empty)
            {
                return TypedResults.Problem("Missing or invalid X-Tenant-Id header.", statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return TypedResults.Problem("Missing X-Api-Key header.", statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
            }

            var logger = loggerFactory.CreateLogger("WebhookEndpoints");

            var apiKeyResult = await tenantService.ValidateApiKeyAsync(tenantId.Value, apiKey, ct);
            if (apiKeyResult.IsFailure)
            {
                logger.LogWarning("Unauthorized webhook request for tenant {TenantId}", tenantId);
                return TypedResults.Problem("Invalid API key.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
            }

            var validation = await validator.ValidateAsync(request, ct);
            if (validation.IsValid is false)
            {
                return TypedResults.Problem(detail: string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)), statusCode: StatusCodes.Status400BadRequest, title: "Validation Failed");
            }

            var command = new IngestAppointmentCommand(
                request.Action,
                tenantId.Value,
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

            var result = await ingestionService.IngestAsync(command, ct);
            if (result.IsFailure)
            {
                if (result.Error.Type == ErrorType.Duplicate)
                {
                    return TypedResults.Ok(new { message = "Appointment already exists." });
                }

                return result.ToProblemDetails();
            }

            return TypedResults.Created((string?)null, new
            {
                appointmentExternalId = request.Appointment.ExternalId,
                patientExternalId = request.Patient.ExternalId,
                tenantId = tenantId.Value,
                action = request.Action.ToString()
            });
        }
    }
}
