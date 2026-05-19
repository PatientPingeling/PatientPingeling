using NotificationService.Application.Abstractions;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Extensions;
using NotificationService.Worker.HostedServices;
using NotificationService.Worker.Messaging;

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
builder.Services.AddScoped<NotificationCommandMessageHandler>();
builder.Services.AddSingleton<NotificationCommandMessageHandler>();
builder.Services.AddHostedService<RabbitMqNotificationConsumerService>();

using var host = builder.Build();

await host.RunAsync();
