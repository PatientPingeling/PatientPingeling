var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// // Database
// builder.Services.AddDbContext<NotificationDbContext>(options =>
// {
//     options.UseNpgsql(configuration.GetConnectionString("Postgres"));
// });

// // RabbitMQ
// builder.Services.AddSingleton<IConnectionFactory>(sp =>
// {
//     var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
//     return new ConnectionFactory
//     {
//         HostName = options.Host,
//         UserName = options.Username,
//         Password = options.Password,
//         Port = options.Port,
//         VirtualHost = options.VirtualHost
//     };
// });

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

app.Run();