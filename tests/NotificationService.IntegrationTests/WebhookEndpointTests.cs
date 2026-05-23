using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace NotificationService.IntegrationTests;

/// <summary>
/// Integration tests spin up real PostgreSQL and RabbitMQ via Testcontainers and hit
/// the actual HTTP endpoints through WebApplicationFactory. This verifies the full stack:
/// routing, header validation, API key authentication, EF Core persistence, and the
/// ingestion business rules — without any mocks.
///
/// Prerequisites: Docker must be running on the host machine.
/// Run with: dotnet test tests/NotificationService.IntegrationTests --filter "TestCategory=Integration"
/// </summary>
[TestClass]
public sealed class WebhookEndpointTests
{
    private static PostgreSqlContainer _postgres = null!;
    private static RabbitMqContainer _rabbitmq = null!;
    private static WebApplicationFactory<Program> _factory = null!;
    private static HttpClient _client = null!;

    private static readonly Guid TestTenantId = Guid.NewGuid();
    private const string TestApiKey = "integration-test-api-key";

    // Valid 32-byte AES-256 key (all zeroes, acceptable for tests only)
    private const string TestEncryptionKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
#pragma warning disable CS0618
        _postgres = new PostgreSqlBuilder()
#pragma warning restore CS0618
            .WithImage("postgres:18-alpine")
            .WithDatabase("notificationdb_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

#pragma warning disable CS0618
        _rabbitmq = new RabbitMqBuilder()
#pragma warning restore CS0618
            .WithImage("rabbitmq:4-management-alpine")
            .Build();

        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Security:EncryptionKey"] = TestEncryptionKey,
                        // Disable OTLP export so tests don't need a Grafana/OTel collector
                        ["OpenTelemetry:EnableTracing"] = "false",
                        ["OpenTelemetry:EnableMetrics"] = "false",
                        ["OpenTelemetry:EnableLogging"] = "false",
                    });
                });
                builder.ConfigureServices(services =>
                {
                    // In Minimal API + .NET 10, AddDatabase(builder.Configuration) evaluates
                    // GetConnectionString immediately during service registration — before
                    // ConfigureAppConfiguration overrides are applied. So we replace the
                    // DbContext registration directly here, which runs after all app services
                    // are registered and definitively wins.
                    var existing = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<NotificationDbContext>));
                    if (existing != null) services.Remove(existing);
                    services.AddDbContext<NotificationDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));
                });
            });

        // CreateClient() triggers app startup, which runs EF Core migrations automatically
        _client = _factory.CreateClient();

        // Seed one tenant so all tests can authenticate
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var hashing = scope.ServiceProvider.GetRequiredService<IHashingService>();

        await db.Set<Tenant>().AddAsync(new Tenant
        {
            Id = TestTenantId,
            Name = "Integration Test Tenant",
            TimeZone = "Europe/Amsterdam",
            Provider = "SwiftSend",
            ApiKeyHash = hashing.Hash(TestApiKey),
        });
        await db.SaveChangesAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitmq.DisposeAsync().AsTask());
    }

    // ── Header validation ──────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_MissingTenantIdHeader_Returns400()
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/webhooks/appointments")
        {
            Content = JsonContent.Create(ValidBody("missing-tenant-hdr"))
        };
        message.Headers.Add("X-Api-Key", TestApiKey);

        var response = await _client.SendAsync(message);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_MissingApiKeyHeader_Returns400()
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/webhooks/appointments")
        {
            Content = JsonContent.Create(ValidBody("missing-apikey-hdr"))
        };
        message.Headers.Add("X-Tenant-Id", TestTenantId.ToString());

        var response = await _client.SendAsync(message);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_WrongApiKey_Returns401()
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/webhooks/appointments")
        {
            Content = JsonContent.Create(ValidBody("wrong-key-apm"))
        };
        message.Headers.Add("X-Tenant-Id", TestTenantId.ToString());
        message.Headers.Add("X-Api-Key", "definitely-wrong-key");

        var response = await _client.SendAsync(message);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Happy path ─────────────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_ValidCreatedRequest_Returns201()
    {
        var response = await _client.SendAsync(BuildRequest(ValidBody("apm-created-001")));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_ValidCreatedRequest_PersistsAppointmentToDatabase()
    {
        const string externalId = "apm-db-check-001";
        await _client.SendAsync(BuildRequest(ValidBody(externalId)));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var exists = await db.Set<Appointment>()
            .AnyAsync(a => a.ExternalId == externalId && a.TenantId == TestTenantId);

        Assert.IsTrue(exists, "Appointment was not persisted to the database.");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_DuplicateAppointment_Returns200()
    {
        var body = ValidBody("apm-duplicate-001");

        await _client.SendAsync(BuildRequest(body));            // creates
        var response = await _client.SendAsync(BuildRequest(body)); // duplicate

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ── UPDATED ───────────────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_ValidUpdatedRequest_Returns201()
    {
        // First create the appointment, then update it with a new time
        const string externalId = "apm-updated-001";
        await _client.SendAsync(BuildRequest(ValidBody(externalId)));

        var updateBody = new
        {
            Action = "UPDATED",
            Patient = new { ExternalId = "pat-001", GivenName = "Jan Jansen", Email = "jan@test.nl", PhoneNumber = "+31612345678" },
            Appointment = new { ExternalId = externalId, ScheduledAt = DateTimeOffset.UtcNow.AddDays(5), Location = "Kamer 2", Service = "Controle", Instructions = (string?)null }
        };

        var response = await _client.SendAsync(BuildRequest(updateBody));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_UpdatedAppointmentNotFound_Returns201AsUpsert()
    {
        // UPDATED for a non-existing appointment must upsert (create it as new)
        // Why: UPDATED events can arrive before CREATED due to network reordering
        var body = new
        {
            Action = "UPDATED",
            Patient = new { ExternalId = "pat-upsert", GivenName = "Upsert Patient", Email = (string?)null, PhoneNumber = (string?)null },
            Appointment = new { ExternalId = "apm-upsert-nonexistent", ScheduledAt = DateTimeOffset.UtcNow.AddDays(3), Location = "Polikliniek", Service = (string?)null, Instructions = (string?)null }
        };

        var response = await _client.SendAsync(BuildRequest(body));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    // ── Cancellation ───────────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_CancelledExistingAppointment_Returns201()
    {
        // Create an appointment first, then cancel it
        const string externalId = "apm-cancel-existing-001";
        await _client.SendAsync(BuildRequest(ValidBody(externalId)));

        var cancelBody = new
        {
            Action = "CANCELLED",
            Patient = new { ExternalId = "pat-001", GivenName = "Jan Jansen", Email = (string?)null, PhoneNumber = (string?)null },
            Appointment = new { ExternalId = externalId, ScheduledAt = DateTimeOffset.UtcNow.AddDays(3), Location = "Polikliniek", Service = (string?)null, Instructions = (string?)null }
        };

        var response = await _client.SendAsync(BuildRequest(cancelBody));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_CancelledAppointmentThatDoesNotExist_Returns404()
    {
        var body = new
        {
            Action = "CANCELLED",
            Patient = new { ExternalId = "pat-cancel", GivenName = "Cancel Patient", Email = (string?)null, PhoneNumber = (string?)null },
            Appointment = new { ExternalId = "apm-never-created", ScheduledAt = DateTimeOffset.UtcNow.AddDays(1), Location = "Polikliniek", Service = (string?)null, Instructions = (string?)null }
        };

        var response = await _client.SendAsync(BuildRequest(body));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_InvalidPayload_Returns400()
    {
        // Empty fields + UNKNOWN action must be rejected by FluentValidation
        // before any business logic runs
        var invalidBody = new
        {
            Action = "UNKNOWN",
            Patient = new { ExternalId = "", GivenName = "", Email = (string?)null, PhoneNumber = (string?)null },
            Appointment = new { ExternalId = "", ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), Location = "", Service = (string?)null, Instructions = (string?)null }
        };

        var response = await _client.SendAsync(BuildRequest(invalidBody));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Unknown tenant ────────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PostWebhook_UnknownTenantId_Returns401()
    {
        // A non-existent tenant must return 401 (not 404) to avoid leaking
        // information about which tenant IDs exist in the system
        var unknownTenantId = Guid.NewGuid();
        var message = new HttpRequestMessage(HttpMethod.Post, "/webhooks/appointments")
        {
            Content = JsonContent.Create(ValidBody("apm-unknown-tenant"))
        };
        message.Headers.Add("X-Tenant-Id", unknownTenantId.ToString());
        message.Headers.Add("X-Api-Key", TestApiKey);

        var response = await _client.SendAsync(message);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private HttpRequestMessage BuildRequest(object body) =>
        new(HttpMethod.Post, "/webhooks/appointments")
        {
            Content = JsonContent.Create(body),
            Headers =
            {
                { "X-Tenant-Id", TestTenantId.ToString() },
                { "X-Api-Key", TestApiKey }
            }
        };

    private static object ValidBody(string appointmentExternalId) => new
    {
        Action = "CREATED",
        Patient = new { ExternalId = "pat-001", GivenName = "Jan Jansen", Email = "jan@test.nl", PhoneNumber = "+31612345678" },
        Appointment = new
        {
            ExternalId = appointmentExternalId,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(3),
            Service = "Controle",
            Location = "Polikliniek",
            Instructions = (string?)null
        }
    };
}
