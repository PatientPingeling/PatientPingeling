using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Application.Services;
using NotificationService.Domain;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests;

/// <summary>
/// Security-focused tests that validate security properties of the system:
/// - Cross-tenant data isolation
/// - Credentials are always encrypted at rest and decrypted only at dispatch time
/// - API keys are never compared in plain text
/// - Unauthorized access is explicitly rejected
/// </summary>
[TestClass]
public sealed class SecurityTests
{
    public TestContext TestContext { get; set; } = null!;

    // ── Cross-tenant data isolation ────────────────────────────────────────────

    [TestMethod]
    [Description("All repository lookups must be scoped to TenantId — prevents cross-tenant data leakage.")]
    public async Task AppointmentIngestion_AllLookups_ScopedToTenantId()
    {
        var tenantId = Guid.NewGuid();
        var appointmentRepo = new Mock<IAppointmentRepository>();
        var patientRepo = new Mock<IPatientRepository>();
        var notificationRepo = new Mock<IScheduledNotificationRepository>();
        var dispatchRepo = new Mock<IDispatchLogRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        patientRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);
        appointmentRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var service = new AppointmentIngestionService(
            appointmentRepo.Object, patientRepo.Object, notificationRepo.Object,
            dispatchRepo.Object, unitOfWork.Object, Mock.Of<ILogger<AppointmentIngestionService>>());

        var command = new IngestAppointmentCommand(AppointmentAction.CREATED, tenantId,
            new PatientInfo("p-1", "Jan", "jan@test.nl", null),
            new AppointmentInfo("a-1", DateTimeOffset.UtcNow.AddDays(2), null, "Kliniek", null));

        await service.IngestAsync(command, TestContext.CancellationToken);

        // Patient lookup MUST include tenantId — never a global lookup
        patientRepo.Verify(r => r.GetByExternalIdAsync(
            It.IsAny<string>(), tenantId, It.IsAny<CancellationToken>()), Times.Once);

        // Appointment lookup MUST include tenantId
        appointmentRepo.Verify(r => r.GetByExternalIdAsync(
            It.IsAny<string>(), tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [Description("CANCELLED lookup must be scoped to TenantId — a tenant cannot cancel another tenant's appointment.")]
    public async Task AppointmentIngestion_CancelledLookup_ScopedToTenantId()
    {
        var tenantId = Guid.NewGuid();
        var appointmentRepo = new Mock<IAppointmentRepository>();

        // Simulate: appointment not found for this tenant (belongs to another tenant)
        appointmentRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var service = new AppointmentIngestionService(
            appointmentRepo.Object, Mock.Of<IPatientRepository>(), Mock.Of<IScheduledNotificationRepository>(),
            Mock.Of<IDispatchLogRepository>(), Mock.Of<IUnitOfWork>(), Mock.Of<ILogger<AppointmentIngestionService>>());

        var command = new IngestAppointmentCommand(AppointmentAction.CANCELLED, tenantId,
            new PatientInfo("p-1", "Jan", null, null),
            new AppointmentInfo("a-1", DateTimeOffset.UtcNow.AddDays(1), null, "Kliniek", null));

        var result = await service.IngestAsync(command, TestContext.CancellationToken);

        // Must fail with NotFound — not silently cancel another tenant's appointment
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.NotFound, result.Error.Type);

        // Lookup was scoped to the requesting tenant's ID
        appointmentRepo.Verify(r => r.GetByExternalIdAsync(
            It.IsAny<string>(), tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── API key security ───────────────────────────────────────────────────────

    [TestMethod]
    [Description("API key validation must always route through IHashingService — plain text comparison is forbidden.")]
    public async Task TenantService_ApiKeyValidation_AlwaysUsesHashingService()
    {
        var tenantRepo = new Mock<ITenantRepository>();
        var hashingService = new Mock<IHashingService>();

        var tenant = new Tenant { ApiKeyHash = "pbkdf2$salt$hash" };
        tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        hashingService.Setup(h => h.Validate(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var service = new TenantService(tenantRepo.Object, hashingService.Object, Mock.Of<ILogger<TenantService>>());
        await service.ValidateApiKeyAsync(Guid.NewGuid(), "raw-api-key", TestContext.CancellationToken);

        // Hashing service must be called — direct string comparison would bypass this
        hashingService.Verify(h => h.Validate("pbkdf2$salt$hash", "raw-api-key"), Times.Once);
    }

    [TestMethod]
    [Description("Invalid API key must be rejected even when the tenant exists.")]
    public async Task TenantService_InvalidApiKey_ReturnsUnauthorized_NotNotFound()
    {
        var tenantRepo = new Mock<ITenantRepository>();
        var hashingService = new Mock<IHashingService>();

        tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { ApiKeyHash = "hash" });
        hashingService.Setup(h => h.Validate(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var service = new TenantService(tenantRepo.Object, hashingService.Object, Mock.Of<ILogger<TenantService>>());
        var result = await service.ValidateApiKeyAsync(Guid.NewGuid(), "wrong-key", TestContext.CancellationToken);

        // Must be Unauthorized — not NotFound (don't reveal whether tenant exists)
        Assert.AreEqual(ErrorType.Unauthorized, result.Error.Type);
    }

    // ── Credential encryption ──────────────────────────────────────────────────

    [TestMethod]
    [Description("Provider credentials must be decrypted before use — encrypted values must never reach the provider.")]
    public async Task DispatchService_ProviderCredentials_AlwaysDecryptedBeforeSending()
    {
        var encryptionService = new Mock<IEncryptionService>();
        var providerFactory = new Mock<IMessageProviderFactory>();
        var provider = new Mock<IMessageProvider>();

        provider.Setup(p => p.SupportedFormats).Returns(new HashSet<MessageFormat> { MessageFormat.Email });
        providerFactory.Setup(f => f.Create(It.IsAny<string>())).Returns(provider.Object);
        encryptionService.Setup(e => e.Decrypt("encrypted-secret")).Returns("plain-secret");

        IReadOnlyDictionary<string, string>? capturedCredentials = null;
        provider.Setup(p => p.SendAsync(It.IsAny<MessageFormat>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<MessageFormat, string, string, IReadOnlyDictionary<string, string>, CancellationToken>(
                (_, _, _, creds, _) => capturedCredentials = creds)
            .ReturnsAsync("msg-id");

        var service = new NotificationDispatchService(encryptionService.Object, providerFactory.Object,
            Mock.Of<ILogger<NotificationDispatchService>>());

        var message = new RabbitMQNotificationMessage
        {
            ScheduledNotificationId = Guid.NewGuid(),
            SendAt = DateTimeOffset.UtcNow,
            PatientName = "Jan",
            PatientEmail = "jan@test.nl",
            PatientPhone = "",
            AppointmentReason = "Controle",
            AppointmentLocation = "Kliniek",
            AppointmentScheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            TenantId = Guid.NewGuid(),
            TenantName = "Test Tenant",
            TenantTimeZone = "Europe/Amsterdam",
            Provider = "SwiftSend",
            ProviderCredentials = [new ProviderCredential { Key = "api_key", EncryptedValue = "encrypted-secret" }]
        };

        await service.DispatchAsync(message, TestContext.CancellationToken);

        // Decrypt must have been called for the encrypted value
        encryptionService.Verify(e => e.Decrypt("encrypted-secret"), Times.Once);

        // Provider received the DECRYPTED value — never the encrypted one
        Assert.IsNotNull(capturedCredentials);
        Assert.AreEqual("plain-secret", capturedCredentials["api_key"]);
        Assert.AreNotEqual("encrypted-secret", capturedCredentials["api_key"]);
    }

    [TestMethod]
    [Description("If credential decryption fails, the dispatch must be aborted — no partial sends with broken credentials.")]
    public async Task DispatchService_DecryptionFailure_AbortsDispatch_NoMessageSent()
    {
        var encryptionService = new Mock<IEncryptionService>();
        var providerFactory = new Mock<IMessageProviderFactory>();
        var provider = new Mock<IMessageProvider>();

        provider.Setup(p => p.SupportedFormats).Returns(new HashSet<MessageFormat> { MessageFormat.Email });
        providerFactory.Setup(f => f.Create(It.IsAny<string>())).Returns(provider.Object);
        encryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Throws(new Exception("key tampered"));

        var service = new NotificationDispatchService(encryptionService.Object, providerFactory.Object,
            Mock.Of<ILogger<NotificationDispatchService>>());

        var message = new RabbitMQNotificationMessage
        {
            ScheduledNotificationId = Guid.NewGuid(),
            SendAt = DateTimeOffset.UtcNow,
            PatientName = "Jan",
            PatientEmail = "jan@test.nl",
            PatientPhone = "",
            AppointmentReason = "Controle",
            AppointmentLocation = "Kliniek",
            AppointmentScheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            TenantId = Guid.NewGuid(),
            TenantName = "Test Tenant",
            TenantTimeZone = "Europe/Amsterdam",
            Provider = "SwiftSend",
            ProviderCredentials = [new ProviderCredential { Key = "api_key", EncryptedValue = "corrupt" }]
        };

        var result = await service.DispatchAsync(message, TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        // Provider.SendAsync must NEVER be called if decryption fails
        provider.Verify(p => p.SendAsync(It.IsAny<MessageFormat>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
