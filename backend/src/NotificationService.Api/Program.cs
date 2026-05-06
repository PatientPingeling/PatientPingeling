using NotificationService.Infrastructure.Persistence;

using NotificationService.Api.Endpoints;
using NotificationService.Api.Options;

using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

using RabbitMQ.Client;


var builder = WebApplication.CreateBuilder(args);

// Configuration
var configuration = builder.Configuration;
builder.Services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));

builder.Services.AddOpenApi();

// Database
builder.Services.AddDbContext<NotificationDbContext>(options =>
{
    options.UseNpgsql(configuration.GetConnectionString("Postgres"));
});

// RabbitMQ
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
    return new ConnectionFactory
    {
        HostName = options.Host,
        UserName = options.Username,
        Password = options.Password,
        Port = options.Port,
        VirtualHost = options.VirtualHost
    };
});

// HTTP Resilience
// builder.Services.ConfigureHttpClientDefaults(http =>
// {
//     http.AddStandardResilienceHandler();
// });


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Middleware
app.UseHttpsRedirection();

// Endpoints
app.MapNotifications();


app.Run();