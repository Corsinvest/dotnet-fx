namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfAsyncExtensionsTests
{
    // ============================================
    // MapErrorAsync
    // ============================================

    [Fact]
    public async Task MapErrorAsync_TransformsError()
    {
        var result = ResultOf.Fail<int, int>(404);

        var mapped = await result.MapErrorAsync(async code =>
        {
            await Task.Delay(1);
            return $"Error {code}";
        });

        Assert.True(mapped.IsFail);
        Assert.True(mapped.TryGetFail(out var error));
        Assert.Equal("Error 404", error.ErrorValue);
    }

    [Fact]
    public async Task MapErrorAsync_PreservesOk()
    {
        var result = ResultOf.Ok<int, int>(42);

        var mapped = await result.MapErrorAsync(async code =>
        {
            await Task.Delay(1);
            return $"Error {code}";
        });

        Assert.True(mapped.IsOk);
        Assert.Equal(42, mapped.GetValueOr(0));
    }

    // ============================================
    // TapErrorAsync
    // ============================================

    [Fact]
    public async Task TapErrorAsync_ExecutesSideEffectOnError()
    {
        var sideEffect = "";
        var result = ResultOf.Fail<int, string>("error");

        var tapped = await result.TapErrorAsync(async e =>
        {
            await Task.Delay(1);
            sideEffect = e;
        });

        Assert.Equal("error", sideEffect);
        Assert.True(tapped.IsFail);
    }

    [Fact]
    public async Task TapErrorAsync_DoesNotExecuteOnOk()
    {
        var sideEffect = "";
        var result = ResultOf.Ok<int, string>(42);

        var tapped = await result.TapErrorAsync(async e =>
        {
            await Task.Delay(1);
            sideEffect = e;
        });

        Assert.Equal("", sideEffect);
        Assert.True(tapped.IsOk);
    }

    // ============================================
    // MapError (sync on Task)
    // ============================================

    [Fact]
    public async Task MapError_OnTask_TransformsError()
    {
        var resultTask = Task.FromResult(ResultOf.Fail<int, int>(404));

        var mapped = await resultTask.MapError(code => $"Error {code}");

        Assert.True(mapped.IsFail);
        Assert.True(mapped.TryGetFail(out var error));
        Assert.Equal("Error 404", error.ErrorValue);
    }

    // ============================================
    // ToResult edge cases
    // ============================================

    [Fact]
    public async Task ToResult_WithCompletedTask_ReturnsOk()
    {
        var task = Task.FromResult(42);

        var result = await task.ToResult();

        Assert.True(result.IsOk);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public async Task ToResult_WithFaultedTask_ReturnsError()
    {
        var task = Task.FromException<int>(new InvalidOperationException("test error"));

        var result = await task.ToResult();

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.IsType<InvalidOperationException>(error.ErrorValue);
        Assert.Equal("test error", error.ErrorValue.Message);
    }

    [Fact]
    public async Task ToResult_WithCancelledTask_CapturesException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var task = Task.FromCanceled<int>(cts.Token);

        var result = await task.ToResult();

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.IsType<TaskCanceledException>(error.ErrorValue);
    }

    [Fact]
    public async Task ToResult_WithErrorMapper_AndSuccess_ReturnsOk()
    {
        var task = Task.FromResult(42);

        var result = await task.ToResult(ex => ex.Message);

        Assert.True(result.IsOk);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public async Task ToResult_WithErrorMapper_AndException_MappsError()
    {
        var task = Task.FromException<int>(new InvalidOperationException("test error"));

        var result = await task.ToResult(ex => $"Mapped: {ex.Message}");

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("Mapped: test error", error.ErrorValue);
    }

    [Fact]
    public async Task ToResult_WithEnumMapper_MapsCorrectly()
    {
        var task = Task.FromException<int>(new TimeoutException("timeout"));

        var result = await task.ToResult(ex => ex is TimeoutException
            ? ErrorType.Timeout
            : ErrorType.Unknown);

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal(ErrorType.Timeout, error.ErrorValue);
    }

    // ============================================
    // Real-World Integration
    // ============================================

    [Fact]
    public async Task ToResult_WithHttpClient_SimulatedError()
    {
        var task = Task.FromException<string>(new HttpRequestException("Network error"));

        var result = await task.ToResult(ex => $"HTTP Error: {ex.Message}");

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("HTTP Error: Network error", error.ErrorValue);
    }

    [Fact]
    public async Task Pipeline_ToResult_Map_Bind()
    {
        var task = Task.FromResult(42);

        var result = await task
            .ToResult()
            .Map(x => x * 2)
            .BindAsync(async x =>
            {
                await Task.Delay(1);
                return ResultOf.Ok<int, Exception>(x + 10);
            });

        Assert.Equal(94, result.GetValueOr(0));
    }

    [Fact]
    public async Task Pipeline_ToResult_WithError_ShortCircuits()
    {
        var task = Task.FromException<int>(new InvalidOperationException("error"));

        var mapExecuted = false;
        var result = await task
            .ToResult()
            .MapAsync(async x =>
            {
                mapExecuted = true;
                await Task.Delay(1);
                return x * 2;
            });

        Assert.False(mapExecuted);
        Assert.True(result.IsFail);
    }

    // ============================================
    // MatchAsync void version
    // ============================================

    [Fact]
    public async Task MatchAsync_VoidVersion_ExecutesCorrectBranch()
    {
        var okExecuted = false;
        var errorExecuted = false;

        var result = ResultOf.Ok<int, string>(42);
        await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                okExecuted = true;
            },
            async error =>
            {
                await Task.Delay(1);
                errorExecuted = true;
            }
        );

        Assert.True(okExecuted);
        Assert.False(errorExecuted);
    }

    [Fact]
    public async Task MatchAsync_VoidVersion_ExecutesErrorBranch()
    {
        var okExecuted = false;
        var errorExecuted = false;

        var result = ResultOf.Fail<int, string>("error");
        await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                okExecuted = true;
            },
            async error =>
            {
                await Task.Delay(1);
                errorExecuted = true;
            }
        );

        Assert.False(okExecuted);
        Assert.True(errorExecuted);
    }

    private enum ErrorType
    {
        Unknown,
        Timeout,
        NetworkError
    }
}
