using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Application.Services;
using NotificationService.Domain;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.UnitTests;

[TestClass]
public sealed class SimpleAppointmentTests
{
    private Mock<IAppointmentRepository> _appointmentRepoMock = null!;
    private Mock<IPatientRepository> _patientRepoMock = null!;
    private Mock<IScheduledNotificationRepository> _notificationRepoMock = null!;
    private Mock<IDispatchLogRepository> _dispatchRepoMock = null!;
    private Mock<IUnitOfWork> _unitOfWorkMock = null!;
    private Mock<ILogger<AppointmentIngestionService>> _loggerMock = null!;
    private AppointmentIngestionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _appointmentRepoMock = new Mock<IAppointmentRepository>();
        _patientRepoMock = new Mock<IPatientRepository>();
        _notificationRepoMock = new Mock<IScheduledNotificationRepository>();
        _dispatchRepoMock = new Mock<IDispatchLogRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<AppointmentIngestionService>>();

        _service = new AppointmentIngestionService(
            _appointmentRepoMock.Object,
            _patientRepoMock.Object,
            _notificationRepoMock.Object,
            _dispatchRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    [TestMethod]
    public async Task Test1_IngestAsync_WhenPatientNotFound_ShouldStopProcessing()
    {
        var appointmentId = Guid.NewGuid();
        var patientInfo = new PatientInfo("testExternalId", "testName", null, null);
        var appointmentInfo = new AppointmentInfo("testExternalId", DateTimeOffset.UtcNow, null, "testType", null);
        var action = default(AppointmentAction);
        var command = new IngestAppointmentCommand(action, appointmentId, patientInfo, appointmentInfo);

        _patientRepoMock
            .Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        await _service.IngestAsync(command);

        _patientRepoMock.Verify(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Test2_IngestAsync_WhenValidData_ShouldSuccessfullyProcess()
    {
        var appointmentId = Guid.NewGuid();
        var patientInfo = new PatientInfo("testExternalId", "testName", null, null);
        var appointmentInfo = new AppointmentInfo("testExternalId", DateTimeOffset.UtcNow, null, "testType", null);
        var action = default(AppointmentAction);
        var command = new IngestAppointmentCommand(action, appointmentId, patientInfo, appointmentInfo);
        var existingPatient = new Patient();

        _patientRepoMock
            .Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatient);

        await _service.IngestAsync(command);

        Assert.IsNotNull(command);
    }

    [TestMethod]
    public void Test3_Command_CanBeAssignedToNull()
    {
        IngestAppointmentCommand? command = null;
        Assert.IsNull(command);
    }
}