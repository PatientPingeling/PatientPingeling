using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotificationService.Infrastructure.Options;
using NotificationService.Infrastructure.Persistence;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Extensions
{
    public static class MessagingExtensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Missing required configuration: ConnectionStrings:Postgres. Set it via environment variable ConnectionStrings__Postgres or appsettings.json.");
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

        public static IServiceCollection AddOpenTelemetry(
            this IServiceCollection services,
            IConfiguration configuration,
            string serviceName,
            bool enableAspNetCore = false,
            bool enableHttpClient = false,
            bool enableEntityFrameworkCore = false)
        {
            services.AddOptions<OpenTelemetryOptions>()
                .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var options = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
                ?? new OpenTelemetryOptions();

            var builder = services.AddOpenTelemetry()
                .ConfigureResource(resource =>
                {
                    resource
                        .AddService(serviceName, serviceVersion: options.ServiceVersion)
                        .AddAttributes([new KeyValuePair<string, object>("deployment.environment", options.Environment)]);
                });

            if (options.EnableTracing)
            {
                builder.WithTracing(tracing =>
                {
                    tracing
                        .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)))
                        .AddSource("MassTransit")
                        .AddSource(Telemetry.ActivitySourceName);

                    if (enableAspNetCore) tracing.AddAspNetCoreInstrumentation();
                    if (enableHttpClient) tracing.AddHttpClientInstrumentation();
                    if (enableEntityFrameworkCore) tracing.AddEntityFrameworkCoreInstrumentation();

                    tracing.AddOtlpExporter(ConfigureExporter(options));
                });
            }

            if (options.EnableMetrics)
            {
                builder.WithMetrics(metrics =>
                {
                    metrics
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation();

                    if (enableAspNetCore) metrics.AddAspNetCoreInstrumentation();
                    if (enableHttpClient) metrics.AddHttpClientInstrumentation();

                    metrics.AddOtlpExporter(ConfigureExporter(options));
                });
            }

            if (options.EnableLogging)
            {
                builder.WithLogging(logging =>
                {
                    logging.AddOtlpExporter(ConfigureExporter(options));
                });
            }

            return services;
        }

        private static Action<OpenTelemetry.Exporter.OtlpExporterOptions> ConfigureExporter(OpenTelemetryOptions options)
        {
            return exporter =>
            {
                exporter.Endpoint = new Uri(options.Endpoint);
                exporter.Protocol = options.Protocol;
                exporter.TimeoutMilliseconds = (int)options.ExportTimeout.TotalMilliseconds;

                if (options.Headers.Count > 0)
                    exporter.Headers = string.Join(",", options.Headers.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            };
        }
    }
}