using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Extensions;
using NotificationService.Application.Factories;
using NotificationService.Application.Services;
using NotificationService.Scheduler.Cleanup;
using NotificationService.Scheduler.RabbitMQ;
using NotificationService.Scheduler.Polling;
using NotificationService.Scheduler.AsyncFlow;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
        .AddDatabase(builder.Configuration)
        .AddMessageBroker(builder.Configuration)
        .AddAsyncFlowStatusPolling(builder.Configuration)
        .AddOpenTelemetry(
                builder.Configuration,
                serviceName: "NotificationService.Scheduler",
                enableEntityFrameworkCore: true);

builder.Services.AddSingleton<RabbitMQEstablisher>();
builder.Services.AddScoped<INotificationMessageFactory, NotificationMessageFactory>();
builder.Services.AddScoped<PollAction>();
builder.Services.AddHostedService<PollerBackgroundService>();
builder.Services.AddHostedService<DataRetentionService>();
builder.Services.AddHostedService<AsyncFlowPollingService>();

using var host = builder.Build();

// Apply any pending EF Core migrations at startup so the scheduler can poll safely.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationService.Infrastructure.Persistence.NotificationDbContext>();
    db.Database.Migrate();
}

await host.RunAsync();