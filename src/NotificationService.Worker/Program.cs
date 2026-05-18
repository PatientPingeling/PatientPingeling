using NotificationService.Infrastructure.Extensions;

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

using var host = builder.Build();

await host.RunAsync();