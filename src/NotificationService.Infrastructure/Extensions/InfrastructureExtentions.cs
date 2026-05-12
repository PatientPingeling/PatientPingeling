using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotificationService.Infrastructure.Options;
using NotificationService.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Extensions
{
    public static class MessagingExtensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Postgres") ?? throw new Exception("bro! where are my environment variables?");
            services.AddDbContext<NotificationDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(5);
                });
            });

            return services;
        }

        public static IServiceCollection AddMessageBroker(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Setup Options with Validation
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection("RabbitMq"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // 2. Register the Connection as a Singleton
            services.AddSingleton(sp =>
            {
                // We resolve the options HERE, inside the factory
                var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                var factory = new ConnectionFactory()
                {
                    HostName = options.Host,
                    UserName = options.Username,
                    Password = options.Password,
                    Port = options.Port
                };

                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            });

            return services;
        }
    }
}