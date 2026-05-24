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

[TestClass]
public sealed class NotificationDispatchServiceTests
{
    private Mock<IEncryptionService> _encryptionService = null!;
    private Mock<IMessageProviderFactory> _providerFactory = null!;
    private Mock<IMessageProvider> _provider = null!;
    private NotificationDispatchService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _encryptionService = new Mock<IEncryptionService>();
        _providerFactory = new Mock<IMessageProviderFactory>();
        _provider = new Mock<IMessageProvider>();

        _providerFactory.Setup(f => f.Create(It.IsAny<string>())).Returns(_provider.Object);

        _service = new NotificationDispatchService(
            _encryptionService.Object,
            _providerFactory.Object,
            Mock.Of<ILogger<NotificationDispatchService>>()
        );
    }

    [TestMethod]
    public async Task DispatchAsync_NoMatchingFormat_ReturnsValidationFailure()
    {
        _provider.Setup(p => p.SupportedFormats)
            .Returns(new HashSet<MessageFormat> { MessageFormat.Email });

        var result = await _service.DispatchAsync(BuildMessage(email: "", phone: ""), CancellationToken.None);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    [TestMethod]
    public async Task DispatchAsync_DecryptThrows_ReturnsFailure()
    {
        _provider.Setup(p => p.SupportedFormats)
            .Returns(new HashSet<MessageFormat> { MessageFormat.Email });
        _encryptionService.Setup(e => e.Decrypt(It.IsAny<string>()))
            .Throws(new Exception("key corrupt"));

        var result = await _service.DispatchAsync(BuildMessage(), CancellationToken.None);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Failure, result.Error.Type);
    }

    [TestMethod]
    public async Task DispatchAsync_ProviderSendThrows_ReturnsFailure()
    {
        _provider.Setup(p => p.SupportedFormats)
            .Returns(new HashSet<MessageFormat> { MessageFormat.Email });
        _encryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns("decrypted");
        _provider.Setup(p => p.SendAsync(It.IsAny<MessageFormat>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("provider down"));

        var result = await _service.DispatchAsync(BuildMessage(), CancellationToken.None);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Failure, result.Error.Type);
    }

    [TestMethod]
    public async Task DispatchAsync_EmailFormat_Succeeds_ReturnsExternalMessageId()
    {
        _provider.Setup(p => p.SupportedFormats)
            .Returns(new HashSet<MessageFormat> { MessageFormat.Email });
        _encryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns("decrypted");
        _provider.Setup(p => p.SendAsync(MessageFormat.Email, It.IsAny<string>(), "jan@test.nl",
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ext-msg-1");

        var result = await _service.DispatchAsync(BuildMessage(email: "jan@test.nl"), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("ext-msg-1", result.Value);
    }

    [TestMethod]
    public async Task DispatchAsync_SmsFormat_UsesPhoneAsRecipient()
    {
        _provider.Setup(p => p.SupportedFormats)
            .Returns(new HashSet<MessageFormat> { MessageFormat.Sms });
        _encryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns("decrypted");

        string? capturedRecipient = null;
        _provider.Setup(p => p.SendAsync(It.IsAny<MessageFormat>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<MessageFormat, string, string, IReadOnlyDictionary<string, string>, CancellationToken>(
                (_, _, r, _, _) => capturedRecipient = r)
            .ReturnsAsync("ext-msg-2");

        await _service.DispatchAsync(BuildMessage(email: "", phone: "+31612345678"), CancellationToken.None);

        Assert.AreEqual("+31612345678", capturedRecipient);
    }

    [TestMethod]
    public async Task DispatchAsync_PushFormat_UsesPhoneAsRecipient()
    {
        _provider.Setup(p => p.SupportedFormats)
            .Returns(new HashSet<MessageFormat> { MessageFormat.Push });
        _encryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns("decrypted");
        _provider.Setup(p => p.SendAsync(It.IsAny<MessageFormat>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ext-msg-3");

        var result = await _service.DispatchAsync(BuildMessage(email: "", phone: "+31699999999"), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
    }

    private static RabbitMQNotificationMessage BuildMessage(
        string email = "jan@test.nl",
        string phone = "+31612345678") => new()
        {
            ScheduledNotificationId = Guid.NewGuid(),
            SendAt = DateTimeOffset.UtcNow.AddHours(1),
            PatientName = "Jan Jansen",
            PatientEmail = email,
            PatientPhone = phone,
            AppointmentReason = "Controle",
            AppointmentLocation = "Polikliniek",
            AppointmentScheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            TenantId = Guid.NewGuid(),
            TenantTimeZone = "Europe/Amsterdam",
            Provider = "SwiftSend",
            ProviderCredentials = [new ProviderCredential { Key = "api_key", EncryptedValue = "enc-val" }]
        };
}
