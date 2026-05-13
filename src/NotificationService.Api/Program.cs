using NotificationService.Api.Endpoints;
using NotificationService.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Database
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

// Middleware
app.UseHttpsRedirection();

// Endpoints
app.MapWebhookEndpoints();

app.Run();