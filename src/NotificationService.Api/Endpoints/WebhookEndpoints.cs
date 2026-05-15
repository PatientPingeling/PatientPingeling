using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Api.Contracts;
using NotificationService.Api.Extensions;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;

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

            var apiKeyResult = await tenantService.ValidateApiKeyAsync(tenantId, apiKey, ct);
            if (apiKeyResult.IsFailure || apiKeyResult.Value is false) // Check if hash matches what is in DB
            {
                return TypedResults.Problem("Invalid API key.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
            }

            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return TypedResults.Problem(detail: string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)), statusCode: StatusCodes.Status400BadRequest, title: "Validation Failed");
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

            var result = await ingestionService.IngestAsync(command, ct);
            if (result.IsFailure)
            {
                return result.ToProblemDetails();
            }

            return TypedResults.Created();
        }
    }
}
