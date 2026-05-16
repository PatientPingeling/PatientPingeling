using FluentValidation;
using NotificationService.Api.Endpoints;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Extensions;
using NotificationService.Infrastructure.Persistence;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "PatientPingeling — Notification API";
        document.Info.Version = "v1";
        document.Info.Description = "API for the PatientPingeling notification service.";
        return Task.CompletedTask;
    });
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Application
builder.Services.AddScoped<IAppointmentIngestionService, AppointmentIngestionService>();
builder.Services.AddScoped<ITenantService, TenantService>();

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

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("testing"))
{
    app.MapOpenApi();

    // Seed database with mock data
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
    await DevDataSeeder.SeedAsync(db, encryption);
}

// Middleware
app.UseHttpsRedirection();

// Endpoints
app.MapWebhookEndpoints();

app.Run();
