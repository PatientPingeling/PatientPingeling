using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Services;
using NotificationService.Domain;
using NotificationService.Domain.Entities;

namespace NotificationService.Tests;

[TestClass]
public sealed class TenantServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    private Mock<ITenantRepository> _tenantRepo = null!;
    private Mock<IHashingService> _hashingService = null!;
    private TenantService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _tenantRepo = new Mock<ITenantRepository>();
        _hashingService = new Mock<IHashingService>();
        _service = new TenantService(
            _tenantRepo.Object,
            _hashingService.Object,
            Mock.Of<ILogger<TenantService>>()
        );
    }

    [TestMethod]
    public async Task ValidateApiKey_EmptyTenantId_ReturnsValidationFailure()
    {
        var result = await _service.ValidateApiKeyAsync(Guid.Empty, "valid-key", TestContext.CancellationToken);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    [TestMethod]
    public async Task ValidateApiKey_EmptyApiKey_ReturnsValidationFailure()
    {
        var result = await _service.ValidateApiKeyAsync(Guid.NewGuid(), "", TestContext.CancellationToken);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    [TestMethod]
    public async Task ValidateApiKey_WhitespaceApiKey_ReturnsValidationFailure()
    {
        var result = await _service.ValidateApiKeyAsync(Guid.NewGuid(), "   ", TestContext.CancellationToken);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    [TestMethod]
    public async Task ValidateApiKey_DbThrows_ReturnsFailure()
    {
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var result = await _service.ValidateApiKeyAsync(Guid.NewGuid(), "key", TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Failure, result.Error.Type);
    }

    [TestMethod]
    public async Task ValidateApiKey_TenantNotFound_ReturnsNotFoundFailure()
    {
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var result = await _service.ValidateApiKeyAsync(Guid.NewGuid(), "key", TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.NotFound, result.Error.Type);
    }

    [TestMethod]
    public async Task ValidateApiKey_KeyMismatch_ReturnsUnauthorizedFailure()
    {
        var tenant = new Tenant { ApiKeyHash = "stored-hash" };
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _hashingService.Setup(h => h.Validate("stored-hash", "wrong-key")).Returns(false);

        var result = await _service.ValidateApiKeyAsync(Guid.NewGuid(), "wrong-key", TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorType.Unauthorized, result.Error.Type);
    }

    [TestMethod]
    public async Task ValidateApiKey_CorrectKey_ReturnsSuccess()
    {
        var tenant = new Tenant { ApiKeyHash = "stored-hash" };
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _hashingService.Setup(h => h.Validate("stored-hash", "correct-key")).Returns(true);

        var result = await _service.ValidateApiKeyAsync(Guid.NewGuid(), "correct-key", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
    }
}
