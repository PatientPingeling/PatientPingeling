using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Extensions;
using NotificationService.Worker.HostedServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
        .AddDatabase(builder.Configuration)
        .AddMessageBroker(builder.Configuration)
        .AddMessageProviders(builder.Configuration)
        .AddOpenTelemetry(
                builder.Configuration,
                serviceName: "NotificationService.Worker",
                enableHttpClient: true,
                enableEntityFrameworkCore: true);

builder.Services.AddScoped<INotificationDispatchService, NotificationDispatchService>();
builder.Services.AddHostedService<RabbitMqNotificationConsumerService>();

using var host = builder.Build();

// Ensure DB migrations are applied before the worker starts consuming messages.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationService.Infrastructure.Persistence.NotificationDbContext>();
    db.Database.Migrate();
}

await host.RunAsync();
