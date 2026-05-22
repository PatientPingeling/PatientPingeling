using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotificationService.Domain;

namespace NotificationService.Tests;

[TestClass]
public sealed class ResultTests
{
    // ── Result (non-generic) ───────────────────────────────────────────────────

    [TestMethod]
    public void Success_IsSuccessTrue_IsFailureFalse()
    {
        var result = Result.Success();
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.IsFailure);
    }

    [TestMethod]
    public void Failure_IsSuccessFalse_IsFailureTrue()
    {
        var result = Result.Failure(new Error("code", "message", ErrorType.NotFound));
        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Failure_ErrorPropertiesAreSet()
    {
        var error = new Error("x", "Something went wrong", ErrorType.Validation);
        var result = Result.Failure(error);
        Assert.AreEqual("x", result.Error.Code);
        Assert.AreEqual("Something went wrong", result.Error.Message);
        Assert.AreEqual(ErrorType.Validation, result.Error.Type);
    }

    [TestMethod]
    public void Success_WithNonNoneError_ThrowsInvalidOperationException()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => new ExposedResult(true, new Error("x", "y")));
    }

    [TestMethod]
    public void Failure_WithNoneError_ThrowsInvalidOperationException()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => new ExposedResult(false, Error.None));
    }

    // ── Result<T> (generic) ────────────────────────────────────────────────────

    [TestMethod]
    public void GenericSuccess_ValueIsAccessible()
    {
        var result = Result<int>.Success(42);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.Value);
    }

    [TestMethod]
    public void GenericFailure_AccessingValueThrows()
    {
        var result = Result<int>.Failure(new Error("e", "m"));
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = result.Value);
    }

    [TestMethod]
    public void GenericFailure_ErrorIsSet()
    {
        var error = new Error("auth.failed", "Unauthorized", ErrorType.Unauthorized);
        var result = Result<string>.Failure(error);
        Assert.AreEqual(ErrorType.Unauthorized, result.Error.Type);
    }

    // ── Error record ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ErrorNone_HasEmptyCodeAndMessage()
    {
        Assert.AreEqual(string.Empty, Error.None.Code);
        Assert.AreEqual(string.Empty, Error.None.Message);
    }

    [TestMethod]
    public void Error_DefaultType_IsFailure()
    {
        var error = new Error("x", "y");
        Assert.AreEqual(ErrorType.Failure, error.Type);
    }

    // ── Subclass to expose protected constructor ───────────────────────────────

    private sealed class ExposedResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
