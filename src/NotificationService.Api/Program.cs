using FluentValidation;
using NotificationService.Api.Endpoints;
using NotificationService.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "PatientPingeling — Notification API";
        document.Info.Version = "v1";
        document.Info.Description = "Webhook ingestion endpoint for appointment notifications. Receives enriched appointment events from OpenMRS plugins and schedules patient reminders via configurable messaging providers.";
        return Task.CompletedTask;
    });
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Infrastructure
builder.Services
    .AddDatabase(builder.Configuration)
    .AddOpenTelemetry(
        builder.Configuration,
        serviceName: "NotificationService.Api",
        enableAspNetCore: true,
        enableHttpClient: true,
        enableEntityFrameworkCore: true);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// TODO: Add JsonStringEnumConverter

// Middleware
app.UseHttpsRedirection();

// Endpoints
app.MapWebhookEndpoints();

app.Run();