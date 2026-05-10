using NotificationService.Infrastructure.Extensions;
using NotificationService.Infrastructure.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// // Database
// builder.Services.AddDbContext<NotificationDbContext>(options =>
// {
//     options.UseNpgsql(configuration.GetConnectionString("Postgres"));
// });

// HTTP Resilience
// builder.Services.ConfigureHttpClientDefaults(http =>
// {
//     http.AddStandardResilienceHandler();
// });


WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Middleware
app.UseHttpsRedirection();

app.Run();