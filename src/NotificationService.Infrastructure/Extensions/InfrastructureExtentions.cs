using DelayBackoffType = Polly.DelayBackoffType;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotificationService.Application.Abstractions;
using NotificationService.Infrastructure.Options;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Persistence.Repositories;
using NotificationService.Infrastructure.Providers;
using NotificationService.Infrastructure.Providers.AsyncFlow;
using NotificationService.Infrastructure.Providers.LegacyLink;
using NotificationService.Infrastructure.Providers.SecurePost;
using NotificationService.Infrastructure.Providers.SwiftSend;
using NotificationService.Infrastructure.Security;
using System.Net;
using NotificationService.Application.Telemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Extensions
{
    public static class InfrastructureExtensions
    {
        public static IServiceCollection AddMessageProviders(this IServiceCollection services, IConfiguration configuration)
        {
            var baseUrl = configuration["Providers:BaseUrl"] ?? throw new InvalidOperationException("Missing required configuration: Providers:BaseUrl. Set it via environment variable Providers__BaseUrl or appsettings.json.");
            var studentGroup = configuration["Providers:StudentGroup"] ?? throw new InvalidOperationException("Missing required configuration: Providers:StudentGroup. Set it via environment variable Providers__StudentGroup or appsettings.json.");

            services.AddScoped<IMessageProviderFactory, MessageProviderFactory>();

            // SwiftSend
            services.AddKeyedScoped<IMessageProvider, SwiftSendProvider>("SwiftSend");
            services.AddHttpClient("SwiftSend", client =>
            {
                client.BaseAddress = new Uri(new Uri(baseUrl), "swiftsend");
                client.DefaultRequestHeaders.Add("X-STUDENT-GROUP", studentGroup);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            });

            // SecurePost
            services.AddKeyedScoped<IMessageProvider, SecurePostProvider>("SecurePost");
            services.AddHttpClient("SecurePost", client =>
            {
                client.BaseAddress = new Uri(new Uri(baseUrl), "securepost/");
                client.DefaultRequestHeaders.Add("X-STUDENT-GROUP", studentGroup);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);

                options.Retry.MaxRetryAttempts = 2;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // JWT/auth system → stricter breaker
                options.CircuitBreaker.FailureRatio = 0.3;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
            });

            // AsyncFlow
            services.AddKeyedScoped<IMessageProvider, AsyncFlowProvider>("AsyncFlow");
            services.AddHttpClient("AsyncFlow", client =>
            {
                client.BaseAddress = new Uri(new Uri(baseUrl), "asyncflow");
                client.DefaultRequestHeaders.Add("X-STUDENT-GROUP", studentGroup);
            })
            .AddStandardResilienceHandler(options =>
            {
                // Submission is async (returns 202 immediately) → shorter timeout budget
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // Queue-based system → medium tolerance
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(20);
            });

            // LegacyLink
            services.AddKeyedScoped<IMessageProvider, LegacyLinkProvider>("LegacyLink");
            services.AddHttpClient("LegacyLink", client =>
            {
                client.BaseAddress = new Uri(new Uri(baseUrl), "LegacyLink/");
                client.DefaultRequestHeaders.Add("X-STUDENT-GROUP", studentGroup);
            })
            .AddStandardResilienceHandler(options =>
            {
                // SOAP with max 3s delay → tighter budget than REST providers
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(6);

                options.Retry.MaxRetryAttempts = 5;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // Legacy SOAP dinosaur → more forgiving
                options.CircuitBreaker.FailureRatio = 0.6;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(45);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            });

            return services;
        }

        public static IServiceCollection AddAsyncFlowStatusPolling(this IServiceCollection services, IConfiguration configuration)
        {
            var baseUrl = configuration["Providers:BaseUrl"] ?? throw new InvalidOperationException("Missing required configuration: Providers:BaseUrl.");
            var studentGroup = configuration["Providers:StudentGroup"] ?? throw new InvalidOperationException("Missing required configuration: Providers:StudentGroup.");
            var asyncFlowApiKey = configuration["Providers:AsyncFlowApiKey"] ?? throw new InvalidOperationException("Missing required configuration: Providers:AsyncFlowApiKey.");

            services.AddScoped<IAsyncFlowStatusClient, AsyncFlowStatusClient>();
            services.AddHttpClient("AsyncFlowStatus", client =>
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Add("X-STUDENT-GROUP", studentGroup);
                client.DefaultRequestHeaders.Add("X-API-KEY", asyncFlowApiKey);
            });

            return services;
        }

        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("Missing required configuration: ConnectionStrings:Postgres. Set it via environment variable ConnectionStrings__Postgres or appsettings.json.");

            services.AddDbContext<NotificationDbContext>(options =>
            {
                // TODO: EnableRetryOnFailure is incompatible with manual transactions (UnitOfWork).
                // To re-enable retries, wrap all ExecuteInTransactionAsync calls in CreateExecutionStrategy().ExecuteAsync.
                options.UseNpgsql(connectionString);
            });

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            services.AddScoped<IPatientRepository, PatientRepository>();
            services.AddScoped<IScheduledNotificationRepository, ScheduledNotificationRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
            services.AddScoped<IProviderCredentialRepository, ProviderCredentialRepository>();
            services.AddScoped<IHashingService, Pbkdf2HashingService>();
            services.AddScoped<IDispatchLogRepository, DispatchLogRepository>();
            services.AddMemoryCache();
            services.AddSingleton<IEncryptionService>(sp =>
            {
                var key = configuration["Security:EncryptionKey"]
                    ?? throw new InvalidOperationException("Missing required configuration: Security:EncryptionKey.");
                var keyBytes = Convert.FromBase64String(key);
                if (keyBytes.Length is not (16 or 24 or 32))
                    throw new InvalidOperationException($"Security:EncryptionKey must decode to 16, 24, or 32 bytes for AES-GCM; got {keyBytes.Length}.");
                return new AesGcmEncryptionService(keyBytes);
            });

            return services;
        }

        public static IServiceCollection AddMessageBroker(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Setup Options with Validation
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // 2. Register the factory once; each hosted service owns its connection/channel lifetime.
            services.AddSingleton<IConnectionFactory>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                return new ConnectionFactory
                {
                    HostName = options.Host,
                    UserName = options.Username,
                    Password = options.Password,
                    Port = options.Port,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
                };
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

                    metrics.AddMeter(NotificationMetrics.MeterName);

                    if (enableAspNetCore)
                    {
                        metrics.AddAspNetCoreInstrumentation();
                        metrics.AddMeter("Microsoft.AspNetCore.Server.Kestrel");
                    }
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

        private static Action<OtlpExporterOptions> ConfigureExporter(OpenTelemetryOptions options)
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
