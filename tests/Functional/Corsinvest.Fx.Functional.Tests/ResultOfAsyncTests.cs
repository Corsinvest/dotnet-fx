namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfAsyncTests
{
    // ============================================
    // Async Map
    // ============================================

    [Fact]
    public async Task MapAsync_TransformsSuccessValue()
    {
        var result = ResultOf.Ok<int, string>(42);

        var mapped = await result.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(84, mapped.GetValueOr(0));
    }

    [Fact]
    public async Task MapAsync_PreservesError()
    {
        var result = ResultOf.Fail<int, string>("error");

        var mapped = await result.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.True(mapped.IsFail);
    }

    [Fact]
    public async Task MapAsync_OnTask_Works()
    {
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var mapped = await resultTask.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(84, mapped.GetValueOr(0));
    }

    [Fact]
    public async Task Map_OnTask_WithSyncSelector_Works()
    {
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var mapped = await resultTask.Map(x => x * 2);

        Assert.Equal(84, mapped.GetValueOr(0));
    }

    // ============================================
    // Async Bind
    // ============================================

    [Fact]
    public async Task BindAsync_ChainsAsyncOperations()
    {
        var result = ResultOf.Ok<int, string>(42);

        var bound = await result.BindAsync(async x =>
        {
            await Task.Delay(1);
            return ResultOf.Ok<int, string>(x * 2);
        });

        Assert.Equal(84, bound.GetValueOr(0));
    }

    [Fact]
    public async Task BindAsync_ShortCircuitsOnError()
    {
        var result = ResultOf.Fail<int, string>("error");

        var bound = await result.BindAsync(async x =>
        {
            await Task.Delay(1);
            return ResultOf.Ok<int, string>(x * 2);
        });

        Assert.True(bound.IsFail);
    }

    [Fact]
    public async Task BindAsync_OnTask_Works()
    {
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var bound = await resultTask.BindAsync(async x =>
        {
            await Task.Delay(1);
            return ResultOf.Ok<int, string>(x * 2);
        });

        Assert.Equal(84, bound.GetValueOr(0));
    }

    // ============================================
    // Async Tap
    // ============================================

    [Fact]
    public async Task TapAsync_ExecutesSideEffect()
    {
        var sideEffect = 0;
        var result = ResultOf.Ok<int, string>(42);

        var tapped = await result.TapOkAsync(async x =>
        {
            await Task.Delay(1);
            sideEffect = x;
        });

        Assert.Equal(42, sideEffect);
        Assert.Equal(42, tapped.GetValueOr(0));
    }

    [Fact]
    public async Task TapAsync_OnTask_Works()
    {
        var sideEffect = 0;
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var tapped = await resultTask.TapOkAsync(async x =>
        {
            await Task.Delay(1);
            sideEffect = x;
        });

        Assert.Equal(42, sideEffect);
    }

    // ============================================
    // Async Match
    // ============================================

    [Fact]
    public async Task MatchAsync_ExecutesCorrectBranch()
    {
        var result = ResultOf.Ok<int, string>(42);

        var value = await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                return ok.Value * 2;
            },
            async error =>
            {
                await Task.Delay(1);
                return 0;
            }
        );

        Assert.Equal(84, value);
    }

    [Fact]
    public async Task MatchAsync_OnTask_Works()
    {
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var value = await resultTask.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                return ok.Value * 2;
            },
            async error =>
            {
                await Task.Delay(1);
                return 0;
            }
        );

        Assert.Equal(84, value);
    }

    // ============================================
    // Async Ensure
    // ============================================

    [Fact]
    public async Task EnsureAsync_WithAsyncPredicate_Works()
    {
        var result = ResultOf.Ok<int, string>(42);

        var ensured = await result.EnsureAsync(
            async x =>
            {
                await Task.Delay(1);
                return x > 0;
            },
            "must be positive"
        );

        Assert.True(ensured.IsOk);
    }

    [Fact]
    public async Task EnsureAsync_ConvertsToError_WhenPredicateFails()
    {
        var result = ResultOf.Ok<int, string>(42);

        var ensured = await result.EnsureAsync(
            async x =>
            {
                await Task.Delay(1);
                return x < 0;
            },
            "must be negative"
        );

        Assert.True(ensured.IsFail);
    }

    // ============================================
    // ToResult from Task
    // ============================================

    [Fact]
    public async Task ToResult_ConvertsSuccessfulTask()
    {
        var task = Task.FromResult(42);

        var result = await task.ToResult();

        Assert.True(result.IsOk);
        Assert.Equal(42, result.GetValueOr(0));
    }

    [Fact]
    public async Task ToResult_CapturesException()
    {
        var task = Task.FromException<int>(new InvalidOperationException("test error"));

        var result = await task.ToResult();

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("test error", error.ErrorValue.Message);
    }

    [Fact]
    public async Task ToResult_WithErrorMapper_MapsException()
    {
        var task = Task.FromException<int>(new InvalidOperationException("test error"));

        var result = await task.ToResult(ex => ex.Message);

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("test error", error.ErrorValue);
    }

    // ============================================
    // Real-World Example: Async Pipeline
    // ============================================

    [Fact]
    public async Task RealWorld_AsyncPipeline()
    {
        var result = await FetchUserAsync(1)
            .BindAsync(user => ValidateUserAsync(user))
            .Map(user => user.ToUpper())
            .TapOkAsync(async user =>
            {
                await Task.Delay(1);
                // Log or side effect
            });

        Assert.Equal("ALICE", result.GetValueOr(string.Empty));
    }

    [Fact]
    public async Task RealWorld_AsyncPipeline_WithError()
    {
        var result = await FetchUserAsync(999)
            .BindAsync(user => ValidateUserAsync(user))
            .Map<string, string, string>(user => user.ToUpper());

        Assert.True(result.IsFail);
        Assert.True(result.TryGetFail(out var error));
        Assert.Equal("user not found", error.ErrorValue);
    }

    // Helper methods
    private static Task<ResultOf<string, string>> FetchUserAsync(int id)
        => Task.FromResult(
            id == 1
                ? ResultOf.Ok<string, string>("Alice")
                : ResultOf.Fail<string, string>("user not found")
        );

    private static Task<ResultOf<string, string>> ValidateUserAsync(string user)
        => Task.FromResult(
            user.Length > 0
                ? ResultOf.Ok<string, string>(user)
                : ResultOf.Fail<string, string>("invalid user")
        );

    private static Task<ResultOf<int, string>> DivideAsync(int a, int b)
    {
        if (b == 0)
        {
            return Task.FromResult(ResultOf.Fail<int, string>("Division by zero"));
        }

        return Task.FromResult(ResultOf.Ok<int, string>(a / b));
    }

    // ============================================
    // Bind with sync selector on Task
    // ============================================

    [Fact]
    public async Task Bind_OnTask_WithSyncSelector_Works()
    {
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var bound = await resultTask.Bind(x => ResultOf.Ok<int, string>(x * 2));

        Assert.Equal(84, bound.GetValueOr(0));
    }

    // ============================================
    // Tap with sync action on Task
    // ============================================

    [Fact]
    public async Task Tap_OnTask_WithSyncAction_Works()
    {
        var sideEffect = 0;
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var tapped = await resultTask.TapOkAsync(x => sideEffect = x);

        Assert.Equal(42, sideEffect);
    }

    // ============================================
    // Match with sync handlers on Task
    // ============================================

    [Fact]
    public async Task Match_OnTask_WithSyncHandlers_Works()
    {
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var value = await resultTask.MatchAsync(
            ok => ok.Value * 2,
            error => 0
        );

        Assert.Equal(84, value);
    }

    // ============================================
    // EnsureAsync on Task
    // ============================================

    [Fact]
    public async Task EnsureAsync_OnTask_Works()
    {
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var ensured = await resultTask.EnsureAsync(
            async x =>
            {
                await Task.Delay(1);
                return x > 0;
            },
            "must be positive"
        );

        Assert.True(ensured.IsOk);
    }

    [Fact]
    public async Task EnsureAsync_OnTask_ConvertsToError_WhenPredicateFails()
    {
        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));

        var ensured = await resultTask.EnsureAsync(
            async x =>
            {
                await Task.Delay(1);
                return x < 0;
            },
            "must be negative"
        );

        Assert.True(ensured.IsFail);
    }

    // ============================================
    // TapAsync on Error (should not execute)
    // ============================================

    [Fact]
    public async Task TapAsync_OnError_DoesNotExecute()
    {
        var sideEffect = 0;
        var result = ResultOf.Fail<int, string>("error");

        var tapped = await result.TapOkAsync(async x =>
        {
            await Task.Delay(1);
            sideEffect = x;
        });

        Assert.Equal(0, sideEffect);
        Assert.True(tapped.IsFail);
    }

    // ============================================
    // MatchAsync (void, on Task) - missing coverage
    // ============================================

    [Fact]
    public async Task MatchAsync_OnTask_VoidVersion_ExecutesCorrectBranch()
    {
        var okExecuted = false;
        var errorExecuted = false;

        var resultTask = Task.FromResult(ResultOf.Ok<int, string>(42));
        await resultTask.MatchAsync(
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
    public async Task MatchAsync_OnTask_VoidVersion_ExecutesErrorBranch()
    {
        var okExecuted = false;
        var errorExecuted = false;

        var resultTask = Task.FromResult(ResultOf.Fail<int, string>("error"));
        await resultTask.MatchAsync(
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

    // ============================================
    // EnsureAsync error path - missing coverage
    // ============================================

    [Fact]
    public async Task EnsureAsync_OnError_PreservesError()
    {
        var result = ResultOf.Fail<int, string>("original error");

        var ensured = await result.EnsureAsync(
            async x =>
            {
                await Task.Delay(1);
                return x > 0;
            },
            "validation failed"
        );

        Assert.True(ensured.IsFail);
        Assert.True(ensured.TryGetFail(out var error));
        Assert.Equal("original error", error.ErrorValue);
    }
}
