namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfTests
{
    // ============================================
    // Basic Construction & Pattern Matching
    // ============================================

    [Fact]
    public void Ok_CreatesSuccessResult()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.True(result.IsOk);
        Assert.False(result.IsFail);
    }

    [Fact]
    public void Fail_CreatesErrorResult()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.False(result.IsOk);
        Assert.True(result.IsFail);
    }

    [Fact]
    public void Result_Ok_CreatesSuccessResultWithStringError()
    {
        var result = ResultOf.Ok<int, string>(42);
        Assert.True(result.IsOk);
        Assert.False(result.IsFail);
    }

    [Fact]
    public void Result_Fail_CreatesErrorResultWithStringError()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.False(result.IsOk);
        Assert.True(result.IsFail);
    }

    [Fact]
    public void Match_ExecutesCorrectBranch()
    {
        var okResult = ResultOf.Ok<int, string>(42);
        var errorResult = ResultOf.Fail<int, string>("error");

        var okValue = okResult.Match(
            ok => ok.Value,
            error => 0
        );
        Assert.Equal(42, okValue);

        var errorValue = errorResult.Match(
            ok => ok.Value,
            error => -1
        );
        Assert.Equal(-1, errorValue);
    }

    [Fact]
    public void TryGetOk_ReturnsCorrectValue()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.True(result.TryGetOk(out var ok));
        Assert.Equal(42, ok.Value);
    }

    [Fact]
    public void TryGetFail_ReturnsCorrectValue()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("error", error.ErrorValue);
    }

    // ============================================
    // LINQ / Functor / Monad
    // ============================================

    [Fact]
    public void Map_TransformsSuccessValue()
    {
        var result = ResultOf.Ok<int, string>(42);

        var mapped = result.Map(x => x * 2);

        Assert.True(mapped.IsOk);
        Assert.Equal(84, mapped.GetValueOr(0));
    }

    [Fact]
    public void Map_PreservesError()
    {
        var result = ResultOf.Fail<int, string>("error");

        var mapped = result.Map(x => x * 2);

        Assert.True(mapped.IsFail);
    }

    [Fact]
    public void Select_SupportsLinqSyntax()
    {
        var result = ResultOf.Ok<int, string>(42);

        var query = from x in result
                    select x * 2;

        Assert.Equal(84, query.GetValueOr(0));
    }

    [Fact]
    public void Bind_ChainsOperations()
    {
        var result = ResultOf.Ok<int, string>(42);

        var bound = result.Bind(x => ResultOf.Ok<int, string>(x * 2));

        Assert.Equal(84, bound.GetValueOr(0));
    }

    [Fact]
    public void Bind_ShortCircuitsOnError()
    {
        var result = ResultOf.Fail<int, string>("error");

        var bound = result.Bind(x => ResultOf.Ok<int, string>(x * 2));

        Assert.True(bound.IsFail);
    }

    [Fact]
    public void SelectMany_SupportsLinqSyntax()
    {
        var result1 = ResultOf.Ok<int, string>(10);
        var result2 = ResultOf.Ok<int, string>(32);

        var query = from x in result1
                    from y in result2
                    select x + y;

        Assert.Equal(42, query.GetValueOr(0));
    }

    [Fact]
    public void SelectMany_ShortCircuitsOnFirstError()
    {
        var result1 = ResultOf.Fail<int, string>("error1");
        var result2 = ResultOf.Ok<int, string>(32);

        var query = from x in result1
                    from y in result2
                    select x + y;

        Assert.True(query.IsFail);
        Assert.True(query.TryGetFail(out var error));
        Assert.Equal("error1", error.ErrorValue);
    }

    // ============================================
    // MapError
    // ============================================

    [Fact]
    public void MapError_TransformsError()
    {
        var result = ResultOf.Fail<int, int>(404);

        var mapped = result.MapError(code => $"Error {code}");

        Assert.True(mapped.IsFail);
        Assert.True(mapped.TryGetFail(out var error));
        Assert.Equal("Error 404", error.ErrorValue);
    }

    [Fact]
    public void MapError_PreservesOk()
    {
        var result = ResultOf.Ok<int, int>(42);

        var mapped = result.MapError(code => $"Error {code}");

        Assert.True(mapped.IsOk);
        Assert.Equal(42, mapped.GetValueOr(0));
    }

    // ============================================
    // Tap
    // ============================================

    [Fact]
    public void Tap_ExecutesSideEffectOnOk()
    {
        var sideEffect = 0;
        var result = ResultOf.Ok<int, string>(42);

        var tapped = result.TapOk(x => sideEffect = x);

        Assert.Equal(42, sideEffect);
        Assert.Equal(42, tapped.GetValueOr(0));
    }

    [Fact]
    public void Tap_DoesNotExecuteOnError()
    {
        var sideEffect = 0;
        var result = ResultOf.Fail<int, string>("error");

        result.TapOk(x => sideEffect = x);

        Assert.Equal(0, sideEffect);
    }

    [Fact]
    public void TapError_ExecutesSideEffectOnError()
    {
        string? sideEffect = null;
        var result = ResultOf.Fail<int, string>("error");

        var tapped = result.TapError(e => sideEffect = e);

        Assert.Equal("error", sideEffect);
        Assert.True(tapped.IsFail);
    }

    // ============================================
    // Ensure
    // ============================================

    [Fact]
    public void Ensure_KeepsOkWhenPredicateTrue()
    {
        var result = ResultOf.Ok<int, string>(42);

        var ensured = result.Ensure(x => x > 0, "must be positive");

        Assert.True(ensured.IsOk);
    }

    [Fact]
    public void Ensure_ConvertsToErrorWhenPredicateFalse()
    {
        var result = ResultOf.Ok<int, string>(42);

        var ensured = result.Ensure(x => x < 0, "must be negative");

        Assert.True(ensured.IsFail);
        Assert.True(ensured.TryGetFail(out var error));
        Assert.Equal("must be negative", error.ErrorValue);
    }

    // ============================================
    // GetValueOr
    // ============================================

    [Fact]
    public void GetValueOr_ReturnsValueOnOk()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public void GetValueOr_ReturnsDefaultOnError()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.Equal(0, result.GetValueOr(0));
    }

    [Fact]
    public void GetValueOr_WithFactory_ComputesDefault()
    {
        var result = ResultOf.Fail<int, string>("error");

        var value = result.GetValueOr(error => error.Length);

        Assert.Equal(5, value);
    }

    // ============================================
    // GetValueOrDefault
    // ============================================

    [Fact]
    public void GetValueOrDefault_ReturnsValueOnOk()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.Equal(42, result.GetValueOrDefault());
    }

    [Fact]
    public void GetValueOrDefault_ReturnsDefaultOnError_Int()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.Equal(0, result.GetValueOrDefault());  // default(int) = 0
    }

    [Fact]
    public void GetValueOrDefault_ReturnsDefaultOnError_String()
    {
        var result = ResultOf.Fail<string, int>(404);

        Assert.Null(result.GetValueOrDefault());  // default(string) = null
    }

    [Fact]
    public void GetValueOrDefault_ReturnsDefaultOnError_Bool()
    {
        var result = ResultOf.Fail<bool, string>("error");

        Assert.False(result.GetValueOrDefault());  // default(bool) = false
    }

    [Fact]
    public void GetValueOrDefault_ReturnsDefaultOnError_Struct()
    {
        var result = ResultOf.Fail<DateTime, string>("error");

        Assert.Equal(default(DateTime), result.GetValueOrDefault());  // default(DateTime) = DateTime.MinValue
    }

    // ============================================
    // GetValueOrThrow
    // ============================================

    [Fact]
    public void GetValueOrThrow_ReturnsValueOnOk()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public void GetValueOrThrow_ThrowsOnError()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
    }

    [Fact]
    public void GetValueOrThrow_UsesCustomException()
    {
        var result = ResultOf.Fail<int, string>("error");

        var ex = Assert.Throws<ArgumentException>(() =>
            result.GetValueOrThrow(e => new ArgumentException(e))
        );
        Assert.Equal("error", ex.Message);
    }

    // ============================================
    // TryGetValue / TryGetFail
    // ============================================

    [Fact]
    public void TryGetValue_ReturnsTrueOnOk()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_ReturnsFalseOnError()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.False(result.TryGetValue(out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetFail_ReturnsTrueOnError()
    {
        var result = ResultOf.Fail<int, string>("error");

        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("error", error.ErrorValue);
    }

    [Fact]
    public void TryGetFail_ReturnsFalseOnOk()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.False(result.TryGetFail(out var error));
        Assert.Null(error);
    }

    // ============================================
    // Real-World Example: Validation Chain
    // ============================================

    [Fact]
    public void RealWorld_ValidationChain()
    {
        var result = Divide(10, 2)
            .Ensure(x => x > 0, "result must be positive")
            .Map(x => x * 10);

        Assert.Equal(50, result.GetValueOr(0));
    }

    [Fact]
    public void RealWorld_ValidationChain_Fails()
    {
        var result = Divide(10, 0)
            .Ensure(x => x > 0, "result must be positive")
            .Map(x => x * 10);

        Assert.True(result.IsFail);
    }

    private static ResultOf<int, string> Divide(int a, int b)
        => b == 0
            ? ResultOf.Fail<int>("division by zero")
            : ResultOf.Ok(a / b);
}
