using NotificationService.Infrastructure.Extensions;
using NotificationService.Application.Factories;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Scheduler.RabbitMQ;
using NotificationService.Scheduler.Polling;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
        .AddDatabase(builder.Configuration)
        .AddMessageBroker(builder.Configuration)
        .AddOpenTelemetry(
                builder.Configuration,
                serviceName: "NotificationService.Scheduler",
                enableEntityFrameworkCore: true);
        
builder.Services.AddScoped<RabbitMQEstablisher>();
builder.Services.AddScoped<INotificationMessageFactory, NotificationMessageFactory>();
builder.Services.AddScoped<PollAction>();
builder.Services.AddHostedService<PollerBackgroundService>();

using var host = builder.Build();

await host.RunAsync();