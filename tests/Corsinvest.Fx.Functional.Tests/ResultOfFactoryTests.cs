namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfFactoryTests
{
    // ============================================
    // Factory Methods with String Errors (convenience overloads)
    // ============================================

    [Fact]
    public void Ok_WithStringError_CreatesSuccessResult()
    {
        var result = ResultOf.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public void Fail_WithStringError_CreatesErrorResult()
    {
        var result = ResultOf.Fail<int>("error message");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("error message", error.ErrorValue);
    }

    [Fact]
    public void Ok_WithTypedError_CreatesSuccessResult()
    {
        var result = ResultOf.Ok<int, TestError>(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public void Fail_WithTypedError_CreatesErrorResult()
    {
        var result = ResultOf.Fail<int, TestError>(TestError.NotFound);

        Assert.True(result.IsFailure);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal(TestError.NotFound, error.ErrorValue);
    }

    // ============================================
    // Property Access
    // ============================================

    [Fact]
    public void IsSuccess_OnOkResult_ReturnsTrue()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void IsFailure_OnOkResult_ReturnsFalse()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.False(result.IsFailure);
    }

    [Fact]
    public void IsSuccess_OnErrorResult_ReturnsFalse()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void IsFailure_OnErrorResult_ReturnsTrue()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.True(result.IsFailure);
    }

    // ============================================
    // IsOk / IsFail (generated properties)
    // ============================================

    [Fact]
    public void IsOk_OnSuccessResult_ReturnsTrue()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.True(result.IsOk);
        Assert.False(result.IsFail);
    }

    [Fact]
    public void IsFail_OnFailureResult_ReturnsTrue()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.False(result.IsOk);
        Assert.True(result.IsFail);
    }

    private enum TestError
    {
        NotFound,
        InvalidInput,
        Timeout
    }
}
