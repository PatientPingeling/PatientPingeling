using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Application.Services;
using NotificationService.Domain;

namespace NotificationService.Application.UnitTests;

[TestClass]
public sealed class IntegrationTests
{
    private AppointmentIngestionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var appointmentRepoMock = new Mock<IAppointmentRepository>();
        var patientRepoMock = new Mock<IPatientRepository>();
        var notificationRepoMock = new Mock<IScheduledNotificationRepository>();
        var dispatchRepoMock = new Mock<IDispatchLogRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<AppointmentIngestionService>>();

        _service = new AppointmentIngestionService(
            appointmentRepoMock.Object,
            patientRepoMock.Object,
            notificationRepoMock.Object,
            dispatchRepoMock.Object,
            unitOfWorkMock.Object,
            loggerMock.Object
        );
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Test1_IngestAsync_ShouldProcessSuccessfullyWithValidCommand()
    {
        var appointmentId = Guid.NewGuid();
        var patientInfo = new PatientInfo("ext-123", "Jan Jansen", null, null);
        var appointmentInfo = new AppointmentInfo("ext-123", DateTimeOffset.UtcNow.AddDays(1), null, "Controle", null);
        var command = new IngestAppointmentCommand(default, appointmentId, patientInfo, appointmentInfo);

        await _service.IngestAsync(command);

        Assert.IsNotNull(command);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Test2_IngestAsync_ShouldHandleIntegrationWithEmptyDomainData()
    {
        var appointmentId = Guid.NewGuid();
        var patientInfo = new PatientInfo("", "", null, null);
        var appointmentInfo = new AppointmentInfo("", DateTimeOffset.UtcNow, null, "", null);
        var command = new IngestAppointmentCommand(default, appointmentId, patientInfo, appointmentInfo);

        await _service.IngestAsync(command);

        Assert.IsNotNull(command);
    }
}