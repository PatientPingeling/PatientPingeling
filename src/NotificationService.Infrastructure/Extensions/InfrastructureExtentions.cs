using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotificationService.Infrastructure.Options;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Extensions
{
    public static class MessagingExtensions
    {
        // Removed 'this' from configuration
        public static IServiceCollection AddRabbitMQ(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Setup Options with Validation
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection("RabbitMq"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // 2. Register the Connection as a Singleton
            services.AddSingleton<IConnection>(sp =>
            {
                // We resolve the options HERE, inside the factory
                RabbitMqOptions options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

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