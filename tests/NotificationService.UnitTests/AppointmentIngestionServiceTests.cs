using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Application.Services;
using NotificationService.Domain;
using NotificationService.Domain.Entities;

namespace NotificationService.Tests;

[TestClass]
public sealed class AppointmentIngestionServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    private Mock<IAppointmentRepository> _appointmentRepo = null!;
    private Mock<IPatientRepository> _patientRepo = null!;
    private Mock<IScheduledNotificationRepository> _notificationRepo = null!;
    private Mock<IDispatchLogRepository> _dispatchLogRepo = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private AppointmentIngestionService _service = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly PatientInfo ValidPatient = new("ext-p1", "Jan Jansen", "jan@test.nl", "+31612345678");
    private static readonly AppointmentInfo FarFutureAppointment = new("ext-a1", DateTimeOffset.UtcNow.AddDays(3), "Controle", "Polikliniek", null);

    [TestInitialize]
    public void Setup()
    {
        _appointmentRepo = new Mock<IAppointmentRepository>();
        _patientRepo = new Mock<IPatientRepository>();
        _notificationRepo = new Mock<IScheduledNotificationRepository>();
        _dispatchLogRepo = new Mock<IDispatchLogRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();

        _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _service = new AppointmentIngestionService(
            _appointmentRepo.Object,
            _patientRepo.Object,
            _notificationRepo.Object,
            _dispatchLogRepo.Object,
            _unitOfWork.Object,
            Mock.Of<ILogger<AppointmentIngestionService>>()
        );
    }

    // ── Guard clauses ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_NullCommand_ReturnsValidationFailure()
    {
        var result = await _service.IngestAsync(null!, TestContext.CancellationToken);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    [TestMethod]
    public async Task IngestAsync_EmptyPatientExternalId_ReturnsValidationFailure()
    {
        var cmd = new IngestAppointmentCommand(AppointmentAction.CREATED, TenantId,
            new PatientInfo("", "Jan", null, null), FarFutureAppointment);
        var result = await _service.IngestAsync(cmd, TestContext.CancellationToken);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    [TestMethod]
    public async Task IngestAsync_EmptyAppointmentExternalId_ReturnsValidationFailure()
    {
        var cmd = new IngestAppointmentCommand(AppointmentAction.CREATED, TenantId,
            ValidPatient, new AppointmentInfo("", DateTimeOffset.UtcNow.AddDays(1), null, "Test", null));
        var result = await _service.IngestAsync(cmd, TestContext.CancellationToken);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    [TestMethod]
    public async Task IngestAsync_UnknownAction_ReturnsValidationFailure()
    {
        var cmd = new IngestAppointmentCommand(AppointmentAction.UNKNOWN, TenantId, ValidPatient, FarFutureAppointment);
        var result = await _service.IngestAsync(cmd, TestContext.CancellationToken);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    // ── CREATED handler ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_Created_DuplicateAppointment_ReturnsDuplicateFailure()
    {
        _patientRepo.Setup(r => r.GetByExternalIdAsync(ValidPatient.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient());
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(FarFutureAppointment.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Appointment());

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CREATED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Duplicate, result.Error.Type);
    }

    [TestMethod]
    public async Task IngestAsync_Created_PatientDbThrows_ReturnsFailure()
    {
        _patientRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CREATED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Failure, result.Error.Type);
    }

    [TestMethod]
    public async Task IngestAsync_Created_AppointmentDbThrows_ReturnsFailure()
    {
        _patientRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CREATED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task IngestAsync_Created_NewPatient_AddsPatientAndAppointment()
    {
        SetupNewAppointmentFlow();

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CREATED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        _patientRepo.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Once);
        _appointmentRepo.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IngestAsync_Created_ExistingPatient_DoesNotAddPatient()
    {
        _patientRepo.Setup(r => r.GetByExternalIdAsync(ValidPatient.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = 5 });
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(FarFutureAppointment.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CREATED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        _patientRepo.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Never);
        _appointmentRepo.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IngestAsync_Created_TransactionFails_RollsBackAndReturnsFailure()
    {
        SetupNewAppointmentFlow();
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CREATED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CREATED – scheduling scenarios ────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_Created_AppointmentFarInFuture_CreatesTwoNotifications()
    {
        SetupNewAppointmentFlow();
        var captured = CaptureNotifications();

        var cmd = Cmd(AppointmentAction.CREATED, scheduledAt: DateTimeOffset.UtcNow.AddHours(36));
        await _service.IngestAsync(cmd, TestContext.CancellationToken);

        Assert.HasCount(2, captured);
    }

    [TestMethod]
    public async Task IngestAsync_Created_AppointmentWithinOneHour_CreatesOneImmediateNotification()
    {
        SetupNewAppointmentFlow();
        var captured = CaptureNotifications();

        var cmd = Cmd(AppointmentAction.CREATED, scheduledAt: DateTimeOffset.UtcNow.AddMinutes(30));
        await _service.IngestAsync(cmd, TestContext.CancellationToken);

        Assert.HasCount(1, captured);
    }

    [TestMethod]
    public async Task IngestAsync_Created_AppointmentBetween1hAnd24h_CloseToOneHour_CreatesOneNotification()
    {
        SetupNewAppointmentFlow();
        var captured = CaptureNotifications();

        // 1h15m ahead: the 1h-reminder is only 15min away, within the 30min merge margin
        var cmd = Cmd(AppointmentAction.CREATED, scheduledAt: DateTimeOffset.UtcNow.AddMinutes(75));
        await _service.IngestAsync(cmd, TestContext.CancellationToken);

        Assert.HasCount(1, captured);
    }

    [TestMethod]
    public async Task IngestAsync_Created_AppointmentBetween1hAnd24h_FarFromOneHour_CreatesTwoNotifications()
    {
        SetupNewAppointmentFlow();
        var captured = CaptureNotifications();

        // 3h ahead: now-reminder + 1h-before reminder both fit
        var cmd = Cmd(AppointmentAction.CREATED, scheduledAt: DateTimeOffset.UtcNow.AddHours(3));
        await _service.IngestAsync(cmd, TestContext.CancellationToken);

        Assert.HasCount(2, captured);
    }

    // ── CANCELLED handler ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_Cancelled_AppointmentNotFound_ReturnsNotFoundFailure()
    {
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CANCELLED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.NotFound, result.Error.Type);
    }

    [TestMethod]
    public async Task IngestAsync_Cancelled_AppointmentDbThrows_ReturnsFailure()
    {
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CANCELLED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Failure, result.Error.Type);
    }

    [TestMethod]
    public async Task IngestAsync_Cancelled_AppointmentFound_SetsIsCancelledAndWritesLogs()
    {
        var appointment = new Appointment { Id = 1 };
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(FarFutureAppointment.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _notificationRepo.Setup(r => r.GetPendingIdsByAppointmentIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Guid.NewGuid(), Guid.NewGuid()]);

        var result = await _service.IngestAsync(Cmd(AppointmentAction.CANCELLED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(appointment.IsCancelled);
        _appointmentRepo.Verify(r => r.UpdateAsync(appointment, It.IsAny<CancellationToken>()), Times.Once);
        _dispatchLogRepo.Verify(
            r => r.AddAsync(It.Is<DispatchLog>(l => l.Outcome == Outcome.CANCELLED), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── UPDATED handler ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task IngestAsync_Updated_AppointmentDbThrows_ReturnsFailure()
    {
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB down"));

        var result = await _service.IngestAsync(Cmd(AppointmentAction.UPDATED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task IngestAsync_Updated_AppointmentNotFound_UpsertsAsNew()
    {
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(FarFutureAppointment.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);
        _patientRepo.Setup(r => r.GetByExternalIdAsync(ValidPatient.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var result = await _service.IngestAsync(Cmd(AppointmentAction.UPDATED), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        _appointmentRepo.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IngestAsync_Updated_TimeChanged_CancelsOldNotificationsAndCreatesNew()
    {
        var originalTime = DateTimeOffset.UtcNow.AddDays(1).ToUniversalTime();
        var patient = new Patient { Id = 1, GivenName = "Jan", Email = "jan@test.nl", PhoneNumber = "+31612345678" };
        var appointment = new Appointment { Id = 2, Patient = patient, ScheduledAt = originalTime };
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(FarFutureAppointment.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _notificationRepo.Setup(r => r.GetPendingIdsByAppointmentIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Guid.NewGuid()]);

        var result = await _service.IngestAsync(Cmd(AppointmentAction.UPDATED, scheduledAt: DateTimeOffset.UtcNow.AddDays(5)), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        _dispatchLogRepo.Verify(
            r => r.AddAsync(It.Is<DispatchLog>(l => l.Outcome == Outcome.CANCELLED), It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationRepo.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<ScheduledNotification>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IngestAsync_Updated_TimeUnchanged_DoesNotRescheduleNotifications()
    {
        var sameTime = DateTimeOffset.UtcNow.AddDays(3).ToUniversalTime();
        var patient = new Patient { Id = 1, GivenName = "Oud Naam", Email = null!, PhoneNumber = null! };
        var appointment = new Appointment { Id = 2, Patient = patient, ScheduledAt = sameTime };
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(FarFutureAppointment.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var result = await _service.IngestAsync(Cmd(AppointmentAction.UPDATED, scheduledAt: sameTime), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        _notificationRepo.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<ScheduledNotification>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task IngestAsync_Updated_PatientChanged_UpdatesPatient()
    {
        var sameTime = DateTimeOffset.UtcNow.AddDays(3).ToUniversalTime();
        var patient = new Patient { Id = 1, GivenName = "Oud Naam", Email = null!, PhoneNumber = null! };
        var appointment = new Appointment { Id = 2, Patient = patient, ScheduledAt = sameTime };
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(FarFutureAppointment.ExternalId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        // New name triggers patient update
        var updatedPatient = new PatientInfo(ValidPatient.ExternalId, "Nieuwe Naam", "new@test.nl", "+31699999999");
        var cmd = new IngestAppointmentCommand(AppointmentAction.UPDATED, TenantId, updatedPatient,
            new AppointmentInfo(FarFutureAppointment.ExternalId, sameTime, null, "Polikliniek", null));
        await _service.IngestAsync(cmd, TestContext.CancellationToken);

        _patientRepo.Verify(r => r.UpdateAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private IngestAppointmentCommand Cmd(AppointmentAction action, DateTimeOffset? scheduledAt = null) =>
        new(action, TenantId, ValidPatient,
            new AppointmentInfo(FarFutureAppointment.ExternalId, scheduledAt ?? FarFutureAppointment.ScheduledAt,
                FarFutureAppointment.Service, FarFutureAppointment.Location, FarFutureAppointment.Instructions));

    private void SetupNewAppointmentFlow()
    {
        _patientRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);
        _appointmentRepo.Setup(r => r.GetByExternalIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);
    }

    private List<ScheduledNotification> CaptureNotifications()
    {
        var list = new List<ScheduledNotification>();
        _notificationRepo
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<ScheduledNotification>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<ScheduledNotification>, CancellationToken>((n, _) => list.AddRange(n))
            .Returns(Task.CompletedTask);
        return list;
    }
}
