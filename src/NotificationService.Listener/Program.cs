using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.Application.Interfaces;
using NotificationService.Infrastructure.Extensions;
using NotificationService.Listener.HostedServices;

using NotificationServiceClass = NotificationService.Application.Services.NotificationService;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMQ(builder.Configuration);

builder.Services.AddScoped<INotificationService, NotificationServiceClass>();

builder.Services.AddHostedService<RabbitMqListenerService>();

using var host = builder.Build();

await host.RunAsync();