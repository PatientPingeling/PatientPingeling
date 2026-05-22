using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Services;
using NotificationService.Domain;
using NotificationService.Domain.Entities;

namespace NotificationService.Tests;

[TestClass]
public sealed class NotificationMessageFactoryTests
{
    private Mock<IScheduledNotificationRepository> _notificationRepo = null!;
    private Mock<IDispatchLogRepository> _dispatchLogRepo = null!;
    private NotificationMessageFactory _factory = null!;

    [TestInitialize]
    public void Setup()
    {
        _notificationRepo = new Mock<IScheduledNotificationRepository>();
        _dispatchLogRepo = new Mock<IDispatchLogRepository>();
        _factory = new NotificationMessageFactory(_notificationRepo.Object, _dispatchLogRepo.Object);
    }

    [TestMethod]
    public async Task CreateAsync_EmptyInput_ReturnsEmptyArray()
    {
        var result = await _factory.CreateAsync([]);
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public async Task CreateAsync_AllNotificationsAlreadyInQueue_ReturnsEmpty()
    {
        var id = Guid.NewGuid();
        var notifications = new[] { new ScheduledNotification { Id = id } };

        _dispatchLogRepo.Setup(r => r.GetLatestStatusBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DispatchLog?> { [id] = new DispatchLog { Outcome = Outcome.INQUEUE } });

        var result = await _factory.CreateAsync(notifications);

        Assert.AreEqual(0, result.Length);
        _notificationRepo.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task CreateAsync_NoLog_NotificationIsEligible()
    {
        var id = Guid.NewGuid();
        var notifications = new[] { new ScheduledNotification { Id = id } };

        _dispatchLogRepo.Setup(r => r.GetLatestStatusBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DispatchLog?> { [id] = null });
        _notificationRepo.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledNotification?)null);

        await _factory.CreateAsync(notifications);

        _notificationRepo.Verify(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateAsync_NewStatusLog_NotificationIsEligible()
    {
        var id = Guid.NewGuid();
        var notifications = new[] { new ScheduledNotification { Id = id } };

        _dispatchLogRepo.Setup(r => r.GetLatestStatusBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DispatchLog?> { [id] = new DispatchLog { Outcome = Outcome.NEW } });
        _notificationRepo.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduledNotification?)null);

        await _factory.CreateAsync(notifications);

        _notificationRepo.Verify(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateAsync_DetailedNotificationIsNull_IsSkipped()
    {
        var id = Guid.NewGuid();
        SetupEligible(id, detailedNotification: null);

        var result = await _factory.CreateAsync([new ScheduledNotification { Id = id }]);

        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public async Task CreateAsync_NotificationWithNoCredentials_IsSkipped()
    {
        var id = Guid.NewGuid();
        var detailed = BuildDetailedNotification(id, credentials: []);
        SetupEligible(id, detailed);

        var result = await _factory.CreateAsync([new ScheduledNotification { Id = id }]);

        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public async Task CreateAsync_ValidNotification_ReturnsMessage()
    {
        var id = Guid.NewGuid();
        var detailed = BuildDetailedNotification(id,
            credentials: [new ProviderCredential { Key = "api_key", EncryptedValue = "enc" }]);
        SetupEligible(id, detailed);

        var result = await _factory.CreateAsync([new ScheduledNotification { Id = id }]);

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual("Jan Jansen", result[0].Patient.GivenName);
    }

    [TestMethod]
    public async Task CreateAsync_MixOfEligibleAndIneligible_ReturnsOnlyEligible()
    {
        var eligibleId = Guid.NewGuid();
        var ineligibleId = Guid.NewGuid();

        _dispatchLogRepo.Setup(r => r.GetLatestStatusBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DispatchLog?>
            {
                [eligibleId] = null,
                [ineligibleId] = new DispatchLog { Outcome = Outcome.SUCCESS }
            });

        var detailed = BuildDetailedNotification(eligibleId,
            credentials: [new ProviderCredential { Key = "k", EncryptedValue = "v" }]);
        _notificationRepo.Setup(r => r.GetByIdWithDetailsAsync(eligibleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailed);

        var result = await _factory.CreateAsync([
            new ScheduledNotification { Id = eligibleId },
            new ScheduledNotification { Id = ineligibleId }
        ]);

        Assert.AreEqual(1, result.Length);
        _notificationRepo.Verify(r => r.GetByIdWithDetailsAsync(ineligibleId, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SetupEligible(Guid id, ScheduledNotification? detailedNotification)
    {
        _dispatchLogRepo.Setup(r => r.GetLatestStatusBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DispatchLog?> { [id] = null });
        _notificationRepo.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailedNotification);
    }

    private static ScheduledNotification BuildDetailedNotification(Guid id, ICollection<ProviderCredential> credentials)
    {
        var tenant = new Tenant { Credentials = credentials };
        var patient = new Patient { GivenName = "Jan Jansen" };
        var appointment = new Appointment { Tenant = tenant, Patient = patient };
        return new ScheduledNotification { Id = id, Appointment = appointment };
    }
}
