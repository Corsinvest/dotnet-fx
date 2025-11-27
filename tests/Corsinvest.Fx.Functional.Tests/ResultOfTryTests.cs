namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfTryTests
{
    // ============================================
    // Try<T> (Exception)
    // ============================================

    [Fact]
    public void Try_WithSuccess_ReturnsOk()
    {
        var result = ResultOf.Try(() => int.Parse("42"));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public void Try_WithException_ReturnsError()
    {
        var result = ResultOf.Try(() => int.Parse("invalid"));

        Assert.True(result.IsFailure);
        Assert.True(result.TryGetFail(out var error));
        Assert.IsType<FormatException>(error.ErrorValue);
    }

    [Fact]
    public void Try_ExceptionMessage_IsPreserved()
    {
        var result = ResultOf.Try(() => int.Parse("invalid"));

        result.Match(
            ok => Assert.Fail("Should be error"),
            error => Assert.Contains("was not in a correct format", error.ErrorValue.Message)
        );
    }

    // ============================================
    // Try<T, E> (with error mapper)
    // ============================================

    [Fact]
    public void TryWithMapper_Success_ReturnsOk()
    {
        var result = ResultOf.Try(
            () => int.Parse("42"),
            ex => $"Parse error: {ex.Message}"
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public void TryWithMapper_Exception_MappsError()
    {
        var result = ResultOf.Try(
            () => int.Parse("invalid"),
            ex => $"Parse error: {ex.Message}"
        );

        Assert.True(result.IsFailure);
        Assert.True(result.TryGetFail(out var error));
        Assert.Contains("Parse error:", error.ErrorValue);
    }

    [Fact]
    public void TryWithMapper_CustomErrorType_Works()
    {
        var result = ResultOf.Try(
            () => int.Parse("invalid"),
            ex => ex is FormatException ? "FORMAT_ERROR" : "UNKNOWN_ERROR"
        );

        Assert.True(result.IsFailure);
        Assert.Equal("FORMAT_ERROR", result.Match(
            ok => "",
            error => error.ErrorValue
        ));
    }

    // ============================================
    // TryAsync<T> (Exception)
    // ============================================

    [Fact]
    public async Task TryAsync_WithSuccess_ReturnsOk()
    {
        var result = await ResultOf.TryAsync(async () =>
        {
            await Task.Delay(1);
            return 42;
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public async Task TryAsync_WithException_ReturnsError()
    {
        var result = await ResultOf.TryAsync(async () =>
        {
            await Task.Delay(1);
            return int.Parse("invalid");
        });

        Assert.True(result.IsFailure);
        Assert.True(result.TryGetFail(out var error));
        Assert.IsType<FormatException>(error.ErrorValue);
    }

    [Fact]
    public async Task TryAsync_ThrowsImmediately_CatchesException()
    {
        var result = await ResultOf.TryAsync<int>(async () =>
        {
            throw new InvalidOperationException("Immediate throw");
#pragma warning disable CS0162 // Unreachable code detected
            await Task.Delay(1);
            return 42;
#pragma warning restore CS0162
        });

        Assert.True(result.IsFailure);
        Assert.True(result.TryGetFail(out var error));
        Assert.IsType<InvalidOperationException>(error.ErrorValue);
        Assert.Equal("Immediate throw", error.ErrorValue.Message);
    }

    // ============================================
    // TryAsync<T, E> (with error mapper)
    // ============================================

    [Fact]
    public async Task TryAsyncWithMapper_Success_ReturnsOk()
    {
        var result = await ResultOf.TryAsync(
            async () =>
            {
                await Task.Delay(1);
                return 42;
            },
            ex => $"Error: {ex.Message}"
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public async Task TryAsyncWithMapper_Exception_MapsError()
    {
        var result = await ResultOf.TryAsync(
            async () =>
            {
                await Task.Delay(1);
                return int.Parse("invalid");
            },
            ex => $"Parse failed: {ex.Message}"
        );

        Assert.True(result.IsFailure);
        Assert.True(result.TryGetFail(out var error));
        Assert.Contains("Parse failed:", error.ErrorValue);
    }

    [Fact]
    public async Task TryAsyncWithMapper_CustomErrorEnum_Works()
    {
        var result = await ResultOf.TryAsync<int, ErrorType>(
            async () =>
            {
                await Task.Delay(1);
                throw new FormatException("Invalid format");
            },
            ex => ex is FormatException ? ErrorType.FormatError : ErrorType.Unknown
        );

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.FormatError, result.Match(
            ok => ErrorType.Unknown,
            error => error.ErrorValue
        ));
    }

    // ============================================
    // Integration with pipeline
    // ============================================

    [Fact]
    public void Try_WithMap_Chains()
    {
        var result = ResultOf.Try(() => int.Parse("42"))
            .Map(x => x * 2);

        Assert.Equal(84, result.GetValueOr(0));
    }

    [Fact]
    public void Try_WithBind_Chains()
    {
        var result = ResultOf.Try(() => int.Parse("42"))
            .Bind(x => x > 0
                ? ResultOf.Ok<int, Exception>(x * 2)
                : ResultOf.Fail<int, Exception>(new InvalidOperationException("negative")));

        Assert.Equal(84, result.GetValueOr(0));
    }

    [Fact]
    public async Task TryAsync_WithMapAsync_Chains()
    {
        var taskResult = ResultOf.TryAsync(async () =>
        {
            await Task.Delay(1);
            return 42;
        });

        var result = await taskResult.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(84, result.GetValueOr(0));
    }

    [Fact]
    public async Task TryAsync_WithBindAsync_Chains()
    {
        var taskResult = ResultOf.TryAsync(async () =>
        {
            await Task.Delay(1);
            return 42;
        });

        var result = await taskResult.BindAsync(async x =>
        {
            await Task.Delay(1);
            return ResultOf.Ok<int, Exception>(x * 2);
        });

        Assert.Equal(84, result.GetValueOr(0));
    }

    // Helper enum for tests
    private enum ErrorType
    {
        Unknown,
        FormatError,
        Overflow
    }
}
