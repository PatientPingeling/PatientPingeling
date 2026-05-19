using NotificationService.Infrastructure.Extensions;
using NotificationService.Scheduler.RabbitMQ;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
        .AddDatabase(builder.Configuration)
        .AddMessageBroker(builder.Configuration)
        .AddOpenTelemetry(
                builder.Configuration,
                serviceName: "NotificationService.Scheduler",
                enableEntityFrameworkCore: true);
        
builder.Services.AddScoped<RabbitMQEstablisher>();
// builder.Services.AddScoped<Poller>();
// builder.Services.AddHostedService<PollerBackgroundService>();

using var host = builder.Build();

await host.RunAsync();