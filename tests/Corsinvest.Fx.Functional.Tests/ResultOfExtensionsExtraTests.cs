namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfExtensionsExtraTests
{
    // ============================================
    // OnSuccess / OnFailure
    // ============================================

    [Fact]
    public void OnSuccess_ExecutesSideEffectOnOk()
    {
        var sideEffect = 0;
        var result = ResultOf.Ok<int, string>(42);

        var returned = result.OnSuccess(x => sideEffect = x);

        Assert.Equal(42, sideEffect);
        Assert.True(returned.IsOk);
        Assert.Equal(42, returned.GetValueOr(0));
    }

    [Fact]
    public void OnSuccess_DoesNotExecuteOnError()
    {
        var sideEffect = 0;
        var result = ResultOf.Fail<int, string>("error");

        result.OnSuccess(x => sideEffect = x);

        Assert.Equal(0, sideEffect);
    }

    [Fact]
    public void OnFailure_ExecutesSideEffectOnError()
    {
        string? sideEffect = null;
        var result = ResultOf.Fail<int, string>("error");

        var returned = result.OnFailure(e => sideEffect = e);

        Assert.Equal("error", sideEffect);
        Assert.True(returned.IsFail);
    }

    [Fact]
    public void OnFailure_DoesNotExecuteOnOk()
    {
        string? sideEffect = null;
        var result = ResultOf.Ok<int, string>(42);

        result.OnFailure(e => sideEffect = e);

        Assert.Null(sideEffect);
    }

    // ============================================
    // Match without return value
    // ============================================

    [Fact]
    public void Match_VoidVersion_ExecutesCorrectBranch()
    {
        var okExecuted = false;
        var errorExecuted = false;

        var okResult = ResultOf.Ok<int, string>(42);
        okResult.Match(
            ok => okExecuted = true,
            error => errorExecuted = true
        );

        Assert.True(okExecuted);
        Assert.False(errorExecuted);
    }

    [Fact]
    public void Match_VoidVersion_ExecutesErrorBranch()
    {
        var okExecuted = false;
        var errorExecuted = false;

        var errorResult = ResultOf.Fail<int, string>("error");
        errorResult.Match(
            ok => okExecuted = true,
            error => errorExecuted = true
        );

        Assert.False(okExecuted);
        Assert.True(errorExecuted);
    }

    // ============================================
    // Ensure with Error already
    // ============================================

    [Fact]
    public void Ensure_OnError_KeepsError()
    {
        var result = ResultOf.Fail<int, string>("original error");

        var ensured = result.Ensure(x => x > 0, "must be positive");

        Assert.True(ensured.IsFail);
        Assert.True(ensured.TryGetFail(out var error));
        Assert.Equal("original error", error.ErrorValue);
    }

    // ============================================
    // TryGetOk/TryGetFail on opposite states
    // ============================================

    [Fact]
    public void TryGetOk_OnError_ReturnsFalse()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.False(result.TryGetOk(out var ok));
        Assert.Null(ok);
    }

    [Fact]
    public void TryGetFail_OnOk_ReturnsFalse()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.False(result.TryGetFail(out var error));
        Assert.Null(error);
    }

    // ============================================
    // IsSuccess / IsFailure properties
    // ============================================

    [Fact]
    public void IsSuccess_OnOk_ReturnsTrue()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void IsFailure_OnError_ReturnsTrue()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
    }

    // ============================================
    // Select (LINQ) on Error
    // ============================================

    [Fact]
    public void Select_OnError_PreservesError()
    {
        var result = ResultOf.Fail<int, string>("error");

        var selected = result.Select(x => x * 2);

        Assert.True(selected.IsFail);
        Assert.True(selected.TryGetFail(out var error));
        Assert.Equal("error", error.ErrorValue);
    }

    // ============================================
    // SelectMany with projection
    // ============================================

    [Fact]
    public void SelectMany_WithProjection_Works()
    {
        var result1 = ResultOf.Ok<int, string>(10);
        var result2 = ResultOf.Ok<int, string>(32);

        var combined = result1.SelectMany(
            x => result2,
            (x, y) => x + y
        );

        Assert.True(combined.IsOk);
        Assert.Equal(42, combined.GetValueOr(0));
    }

    [Fact]
    public void SelectMany_WithProjection_ShortCircuitsOnFirstError()
    {
        var result1 = ResultOf.Fail<int, string>("error1");
        var result2 = ResultOf.Ok<int, string>(32);

        var combined = result1.SelectMany(
            x => result2,
            (x, y) => x + y
        );

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Equal("error1", error.ErrorValue);
    }

    [Fact]
    public void SelectMany_WithProjection_ShortCircuitsOnSecondError()
    {
        var result1 = ResultOf.Ok<int, string>(10);
        var result2 = ResultOf.Fail<int, string>("error2");

        var combined = result1.SelectMany(
            x => result2,
            (x, y) => x + y
        );

        Assert.True(combined.IsFail);
        Assert.True(combined.TryGetFail(out var error));
        Assert.Equal("error2", error.ErrorValue);
    }

    // ============================================
    // TryGetValue edge cases
    // ============================================

    [Fact]
    public void TryGetValue_WithReferenceType_WorksCorrectly()
    {
        var result = ResultOf.Ok<string, int>("hello");

        Assert.True(result.TryGetValue(out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void TryGetValue_OnError_SetsDefaultValue()
    {
        var result = ResultOf.Fail<string, int>(404);

        Assert.False(result.TryGetValue(out var value));
        Assert.Null(value);  // default(string) = null
    }

    // ============================================
    // Chaining multiple operations
    // ============================================

    [Fact]
    public void Chain_MapBindTapEnsure_Works()
    {
        var sideEffect = 0;
        var result = ResultOf.Ok<int, string>(10)
            .Map(x => x * 2)
            .Bind(x => ResultOf.Ok<int, string>(x + 2))
            .TapOk(x => sideEffect = x)
            .Ensure(x => x > 0, "must be positive");

        Assert.Equal(22, sideEffect);
        Assert.True(result.IsOk);
        Assert.Equal(22, result.GetValueOr(0));
    }

    [Fact]
    public void Chain_FailsAtSecondStep_StopsExecution()
    {
        var tapExecuted = false;
        var result = ResultOf.Ok<int, string>(10)
            .Map(x => x * 2)
            .Bind(x => ResultOf.Fail<int, string>("error at bind"))
            .TapOk(x => tapExecuted = true)
            .Ensure(x => x > 0, "must be positive");

        Assert.False(tapExecuted);
        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("error at bind", error.ErrorValue);
    }
}
