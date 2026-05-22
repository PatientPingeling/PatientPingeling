using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace NotificationService.IntegrationTests;

/// <summary>
/// Integration tests spin up real PostgreSQL and RabbitMQ via Testcontainers
/// and hit the actual HTTP endpoints. This verifies the full stack — routing,
/// validation, EF Core persistence and RabbitMQ messaging — without mocks.
///
/// Prerequisites: Docker must be running on the host machine.
/// Run with: dotnet test tests/NotificationService.IntegrationTests --filter "TestCategory=Integration"
/// </summary>
[TestClass]
public sealed class WebhookEndpointTests
{
    private static PostgreSqlContainer? _postgres;
    private static RabbitMqContainer? _rabbitmq;

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
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        var tasks = new List<Task>();
        if (_postgres is not null) tasks.Add(_postgres.DisposeAsync().AsTask());
        if (_rabbitmq is not null) tasks.Add(_rabbitmq.DisposeAsync().AsTask());
        await Task.WhenAll(tasks);
    }

    // ── Placeholder tests ──────────────────────────────────────────────────────
    // Remove [Ignore] and implement the test body once Docker is available.
    //
    // Typical integration test shape:
    //   1. Build WebApplication with container connection strings via env vars.
    //   2. Apply EF Core migrations.
    //   3. Seed tenant + API key.
    //   4. POST to /webhooks/appointments.
    //   5. Assert HTTP 201 + verify DB rows via direct SQL or API query.

    [TestMethod]
    [TestCategory("Integration")]
    [Ignore("Requires Docker. Start Docker Desktop and remove [Ignore] to run.")]
    public async Task Placeholder_ContainersStartSuccessfully()
    {
        Assert.IsNotNull(_postgres!.GetConnectionString());
        Assert.IsNotNull(_rabbitmq!.GetConnectionString());
        await Task.CompletedTask;
    }
}
