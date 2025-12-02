namespace Corsinvest.Fx.Functional;

/// <summary>
/// LINQ and helper extensions for ResultOf&lt;T, E&gt;
/// </summary>
public static class ResultOfExtensions
{
    // ============================================
    // LINQ Query Syntax Support
    // ============================================

    /// <summary>
    /// LINQ Select (functor map)
    /// </summary>
    public static ResultOf<U, E> Select<T, U, E>(this ResultOf<T, E> result, Func<T, U> selector)
        => result.Match(
            ok => ResultOf.Ok<U, E>(selector(ok.Value)),
            error => ResultOf.Fail<U, E>(error.ErrorValue)
        );

    /// <summary>
    /// LINQ SelectMany (monadic bind)
    /// </summary>
    public static ResultOf<U, E> SelectMany<T, U, E>(this ResultOf<T, E> result, Func<T, ResultOf<U, E>> selector)
        => result.Match(
            ok => selector(ok.Value),
            error => ResultOf.Fail<U, E>(error.ErrorValue)
        );

    /// <summary>
    /// LINQ SelectMany with projection (monadic bind + map)
    /// </summary>
    public static ResultOf<V, E> SelectMany<T, U, V, E>(
        this ResultOf<T, E> result,
        Func<T, ResultOf<U, E>> selector,
        Func<T, U, V> projector)
        => result.Match(
            ok => selector(ok.Value).Match(
                okU => ResultOf.Ok<V, E>(projector(ok.Value, okU.Value)),
                error => ResultOf.Fail<V, E>(error.ErrorValue)
            ),
            error => ResultOf.Fail<V, E>(error.ErrorValue)
        );

    // ============================================
    // Functional Helpers
    // ============================================

    /// <summary>
    /// Monadic bind (flatMap in other languages)
    /// </summary>
    public static ResultOf<U, E> Bind<T, U, E>(this ResultOf<T, E> result, Func<T, ResultOf<U, E>> f)
        => result.SelectMany(f);

    /// <summary>
    /// Map over the success value
    /// </summary>
    public static ResultOf<U, E> Map<T, U, E>(this ResultOf<T, E> result, Func<T, U> f)
        => result.Select(f);

    /// <summary>
    /// Map over the error value
    /// </summary>
    public static ResultOf<T, EOut> MapError<T, EIn, EOut>(this ResultOf<T, EIn> result, Func<EIn, EOut> errorMapper)
        => result.Match(
            ok => ResultOf.Ok<T, EOut>(ok.Value),
            error => ResultOf.Fail<T, EOut>(errorMapper(error.ErrorValue))
        );

    /// <summary>
    /// Execute side effect if result is Ok (returns the original result)
    /// </summary>
    public static ResultOf<T, E> TapOk<T, E>(this ResultOf<T, E> result, Action<T> action)
    {
        if (result.TryGetOk(out var ok)) { action(ok.Value); }
        return result;
    }

    /// <summary>
    /// Execute side effect if result is Fail (returns the original result)
    /// </summary>
    public static ResultOf<T, E> TapError<T, E>(this ResultOf<T, E> result, Action<E> action)
    {
        if (result.TryGetFail(out var error)) { action(error.ErrorValue); }
        return result;
    }

    /// <summary>
    /// Alias for <see cref="TapOk{T, E}(ResultOf{T, E}, Action{T})"/>. Executes side effect if result is Ok (returns the original result).
    /// FluentResults-compatible naming. Use <c>TapOk</c> for concise code or <c>OnSuccess</c> for explicit FluentResults-style code.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the error value</typeparam>
    /// <param name="result">The result to tap</param>
    /// <param name="action">The action to execute on success</param>
    /// <returns>The original result, unchanged</returns>
    public static ResultOf<T, E> OnSuccess<T, E>(this ResultOf<T, E> result, Action<T> action) => result.TapOk(action);

    /// <summary>
    /// Alias for <see cref="TapError{T, E}(ResultOf{T, E}, Action{E})"/>. Executes side effect if result is Fail (returns the original result).
    /// FluentResults-compatible naming. Use <c>TapError</c> for concise code or <c>OnFailure</c> for explicit FluentResults-style code.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the error value</typeparam>
    /// <param name="result">The result to tap</param>
    /// <param name="action">The action to execute on failure</param>
    /// <returns>The original result, unchanged</returns>
    public static ResultOf<T, E> OnFailure<T, E>(this ResultOf<T, E> result, Action<E> action) => TapError(result, action);

    /// <summary>
    /// Validate the success value with a predicate
    /// </summary>
    public static ResultOf<T, E> Ensure<T, E>(this ResultOf<T, E> result, Func<T, bool> predicate, E error)
        => result.Match(
            ok => predicate(ok.Value)
                ? result
                : ResultOf.Fail<T, E>(error),
            _ => result
        );

    // ============================================
    // Unwrapping / Default Values
    // ============================================

    /// <summary>
    /// Get the value or return a default
    /// </summary>
    public static T GetValueOr<T, E>(this ResultOf<T, E> result, T defaultValue)
        => result.Match(
            ok => ok.Value,
            _ => defaultValue
        );

    /// <summary>
    /// Get the value or compute a default from error
    /// </summary>
    public static T GetValueOr<T, E>(this ResultOf<T, E> result, Func<E, T> errorToValue)
        => result.Match(
            ok => ok.Value,
            error => errorToValue(error.ErrorValue)
        );

    /// <summary>
    /// Get the value or return the default value for type T (null for reference types, 0 for int, false for bool, etc.)
    /// </summary>
    public static T? GetValueOrDefault<T, E>(this ResultOf<T, E> result)
        => result.Match(
            ok => ok.Value,
            _ => default(T)
        );

    /// <summary>
    /// Get the value or throw an exception
    /// </summary>
    public static T GetValueOrThrow<T, E>(this ResultOf<T, E> result, Func<E, Exception>? exceptionFactory = null)
        => result.Match(
            ok => ok.Value,
            error => throw (exceptionFactory?.Invoke(error.ErrorValue)
                            ?? new InvalidOperationException($"Result is in error state: {error.ErrorValue}"))
        );

    // ============================================
    // Conversion Helpers
    // ============================================

    /// <summary>
    /// Try to get the value (similar to Dictionary.TryGetValue pattern)
    /// </summary>
    public static bool TryGetValue<T, E>(this ResultOf<T, E> result, out T value)
    {
        if (result.TryGetOk(out var ok))
        {
            value = ok.Value;
            return true;
        }
        value = default!;
        return false;
    }

    // ============================================
    // Error Collection & Accumulation
    // ============================================

    /// <summary>
    /// Adds an error to an existing failed result.
    /// This method only works on results that are already in the Error state.
    /// Use this for incrementally building up error lists.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <typeparam name="E">The type of the error</typeparam>
    /// <param name="result">The result to add an error to (must be in Error state)</param>
    /// <param name="error">The error to append to the existing error list</param>
    /// <returns>A new result with the error added to the existing error list</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this method is called on a result in the Ok state.
    /// Use <see cref="ResultOf.CollectErrors{T,E}"/> for validation scenarios instead.
    /// </exception>
    /// <example>
    /// <code>
    /// // Build up errors incrementally
    /// var result = ResultOf.Fail&lt;User, List&lt;string&gt;&gt;(["Initial error"])
    ///     .WithError("Second error")
    ///     .WithError("Third error");
    ///
    /// // result.ErrorValue = ["Initial error", "Second error", "Third error"]
    ///
    /// // ❌ This throws InvalidOperationException:
    /// var okResult = ResultOf.Ok&lt;User, List&lt;string&gt;&gt;(user)
    ///     .WithError("error");  // Throws!
    /// </code>
    /// </example>
    public static ResultOf<T, List<E>> WithError<T, E>(this ResultOf<T, List<E>> result, E error)
        => result.Match(
            ok => throw new InvalidOperationException("Cannot add error to a successful result. Use CollectErrors for validation."),
            errorResult => ResultOf.Fail<T, List<E>>([.. errorResult.ErrorValue, error])
        );

    // ============================================
    // Async Map
    // ============================================

    /// <summary>
    /// Map with async selector
    /// </summary>
    public static async Task<ResultOf<U, E>> MapAsync<T, U, E>(
        this ResultOf<T, E> result,
        Func<T, Task<U>> selector)
        => await result.Match(
            async ok => ResultOf.Ok<U, E>(await selector(ok.Value)),
            error => Task.FromResult(ResultOf.Fail<U, E>(error.ErrorValue))
        );

    /// <summary>
    /// Map over Task&lt;ResultOf&gt; with async selector
    /// </summary>
    public static async Task<ResultOf<U, E>> MapAsync<T, U, E>(
        this Task<ResultOf<T, E>> resultTask,
        Func<T, Task<U>> selector)
    {
        var result = await resultTask;
        return await result.MapAsync(selector);
    }

    /// <summary>
    /// Map over Task&lt;ResultOf&gt; with sync selector
    /// </summary>
    public static async Task<ResultOf<U, E>> Map<T, U, E>(
        this Task<ResultOf<T, E>> resultTask,
        Func<T, U> selector)
    {
        var result = await resultTask;
        return result.Map(selector);
    }

    // ============================================
    // Async Bind
    // ============================================

    /// <summary>
    /// Bind with async selector
    /// </summary>
    public static async Task<ResultOf<U, E>> BindAsync<T, U, E>(
        this ResultOf<T, E> result,
        Func<T, Task<ResultOf<U, E>>> selector)
        => await result.Match(
            async ok => await selector(ok.Value),
            error => Task.FromResult(ResultOf.Fail<U, E>(error.ErrorValue))
        );

    /// <summary>
    /// Bind over Task&lt;ResultOf&gt; with async selector
    /// </summary>
    public static async Task<ResultOf<U, E>> BindAsync<T, U, E>(
        this Task<ResultOf<T, E>> resultTask,
        Func<T, Task<ResultOf<U, E>>> selector)
    {
        var result = await resultTask;
        return await result.BindAsync(selector);
    }

    /// <summary>
    /// Bind over Task&lt;ResultOf&gt; with sync selector
    /// </summary>
    public static async Task<ResultOf<U, E>> Bind<T, U, E>(
        this Task<ResultOf<T, E>> resultTask,
        Func<T, ResultOf<U, E>> selector)
    {
        var result = await resultTask;
        return result.Bind(selector);
    }

    // ============================================
    // Async Tap
    // ============================================

    /// <summary>
    /// Execute side effect if result is Ok with async action (returns the original result)
    /// </summary>
    public static async Task<ResultOf<T, E>> TapOkAsync<T, E>(
        this ResultOf<T, E> result,
        Func<T, Task> action)
    {
        if (result.TryGetOk(out var ok)) { await action(ok.Value); }
        return result;
    }

    /// <summary>
    /// Execute side effect if result is Ok over Task&lt;ResultOf&gt; with async action
    /// </summary>
    public static async Task<ResultOf<T, E>> TapOkAsync<T, E>(
        this Task<ResultOf<T, E>> resultTask,
        Func<T, Task> action)
    {
        var result = await resultTask;
        return await result.TapOkAsync(action);
    }

    /// <summary>
    /// Execute side effect if result is Ok over Task&lt;ResultOf&gt; with sync action
    /// </summary>
    public static async Task<ResultOf<T, E>> TapOkAsync<T, E>(
        this Task<ResultOf<T, E>> resultTask,
        Action<T> action)
    {
        var result = await resultTask;
        return result.TapOk(action);
    }

    /// <summary>
    /// Alias for <see cref="TapOkAsync{T, E}(ResultOf{T, E}, Func{T, Task})"/>. Executes side effect if result is Ok with async action.
    /// FluentResults-compatible naming.
    /// </summary>
    public static async Task<ResultOf<T, E>> OnSuccessAsync<T, E>(
        this ResultOf<T, E> result,
        Func<T, Task> action)
        => await result.TapOkAsync(action);

    /// <summary>
    /// Alias for <see cref="TapErrorAsync{T, E}(ResultOf{T, E}, Func{E, Task})"/>. Executes side effect if result is Fail with async action.
    /// FluentResults-compatible naming.
    /// </summary>
    public static async Task<ResultOf<T, E>> OnFailureAsync<T, E>(
        this ResultOf<T, E> result,
        Func<E, Task> action)
        => await result.TapErrorAsync(action);

    // ============================================
    // Async Ensure
    // ============================================

    /// <summary>
    /// Ensure with async predicate
    /// </summary>
    public static async Task<ResultOf<T, E>> EnsureAsync<T, E>(
        this ResultOf<T, E> result,
        Func<T, Task<bool>> predicate,
        E error)
        => await result.Match(
            async ok => await predicate(ok.Value)
                ? result
                : ResultOf.Fail<T, E>(error),
            _ => Task.FromResult(result)
        );

    /// <summary>
    /// Ensure over Task&lt;ResultOf&gt; with async predicate
    /// </summary>
    public static async Task<ResultOf<T, E>> EnsureAsync<T, E>(
        this Task<ResultOf<T, E>> resultTask,
        Func<T, Task<bool>> predicate,
        E error)
    {
        var result = await resultTask;
        return await result.EnsureAsync(predicate, error);
    }

    // ============================================
    // Conversion from/to Task
    // ============================================

    /// <summary>
    /// Converts an existing Task to a ResultOf by catching any exceptions.
    /// Use this extension method when you have a Task from an external API or library
    /// that you want to convert to ResultOf for safe error handling.
    /// </summary>
    /// <typeparam name="T">The type of the task result</typeparam>
    /// <param name="task">The task to convert</param>
    /// <returns>
    /// A result containing either the task's return value on success,
    /// or the caught exception on failure
    /// </returns>
    /// <remarks>
    /// This is an extension method on Task, useful for converting existing Tasks.
    /// If you're wrapping a new async operation, consider using <see cref="ResultOf.TryAsync{T}(Func{Task{T}})"/> instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Convert existing Task from external API
    /// var result = await httpClient.GetStringAsync(url).ToResult();
    ///
    /// // Chain with other operations
    /// var processedResult = await FetchDataAsync()
    ///     .ToResult()
    ///     .MapAsync(async data => await ProcessAsync(data));
    ///
    /// result.Match(
    ///     ok => Console.WriteLine($"Success: {ok.Value}"),
    ///     error => Console.WriteLine($"Error: {error.ErrorValue.Message}")
    /// );
    /// </code>
    /// </example>
    public static async Task<ResultOf<T, Exception>> ToResult<T>(
        this Task<T> task)
    {
        try
        {
            return ResultOf.Ok<T, Exception>(await task);
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<T, Exception>(ex);
        }
    }

    /// <summary>
    /// Converts an existing Task to a ResultOf with custom error mapping.
    /// Use this extension method when you want to transform exceptions from an external API
    /// into your own error type.
    /// </summary>
    /// <typeparam name="T">The type of the task result</typeparam>
    /// <typeparam name="E">The type of the custom error</typeparam>
    /// <param name="task">The task to convert</param>
    /// <param name="errorMapper">Function to map the caught exception to your error type</param>
    /// <returns>
    /// A result containing either the task's return value on success,
    /// or the mapped error on failure
    /// </returns>
    /// <remarks>
    /// This is an extension method on Task, useful for converting existing Tasks with custom error handling.
    /// If you're wrapping a new async operation, consider using <see cref="ResultOf.TryAsync{T,E}(Func{Task{T}}, Func{Exception, E})"/> instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Convert Task with custom error mapping
    /// var result = await httpClient.GetAsync(url)
    ///     .ToResult(ex => $"HTTP request failed: {ex.Message}");
    ///
    /// // Map to custom error enum
    /// enum ApiError { NetworkError, Timeout, ServerError }
    ///
    /// var apiResult = await apiClient.CallAsync()
    ///     .ToResult(ex => ex is TimeoutException
    ///         ? ApiError.Timeout
    ///         : ApiError.NetworkError);
    ///
    /// // Use in complex pipeline
    /// var finalResult = await ExternalApiCallAsync()
    ///     .ToResult(ex => $"API error: {ex.Message}")
    ///     .BindAsync(async data => await ProcessDataAsync(data))
    ///     .MapAsync(async result => await FormatResultAsync(result));
    /// </code>
    /// </example>
    public static async Task<ResultOf<T, E>> ToResult<T, E>(
        this Task<T> task,
        Func<Exception, E> errorMapper)
    {
        try
        {
            return ResultOf.Ok<T, E>(await task);
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<T, E>(errorMapper(ex));
        }
    }

    // ============================================
    // Async MapError
    // ============================================

    /// <summary>
    /// Map over the error value with async mapper
    /// </summary>
    public static async Task<ResultOf<T, EOut>> MapErrorAsync<T, EIn, EOut>(
        this ResultOf<T, EIn> result,
        Func<EIn, Task<EOut>> errorMapper)
        => await result.Match(
            ok => Task.FromResult(ResultOf.Ok<T, EOut>(ok.Value)),
            async error => ResultOf.Fail<T, EOut>(await errorMapper(error.ErrorValue))
        );

    /// <summary>
    /// Map over error value of Task&lt;ResultOf&gt; with sync mapper
    /// </summary>
    public static async Task<ResultOf<T, EOut>> MapError<T, EIn, EOut>(
        this Task<ResultOf<T, EIn>> resultTask,
        Func<EIn, EOut> errorMapper)
    {
        var result = await resultTask;
        return result.MapError(errorMapper);
    }

    // ============================================
    // Async TapError
    // ============================================

    /// <summary>
    /// Execute side effect on error with async action
    /// </summary>
    public static async Task<ResultOf<T, E>> TapErrorAsync<T, E>(
        this ResultOf<T, E> result,
        Func<E, Task> action)
    {
        if (result.TryGetFail(out var error))
        {
            await action(error.ErrorValue);
        }
        return result;
    }

    // /// <summary>
    // /// Match with async void handlers
    // /// </summary>
    // public static async Task MatchAsync<T, E>(
    //     this ResultOf<T, E> result,
    //     Func<ResultOf<T, E>.Ok, Task> onOk,
    //     Func<ResultOf<T, E>.Error, Task> onError)
    // {
    //     switch (result)
    //     {
    //         case ResultOf<T, E>.Ok ok:
    //             await onOk(ok);
    //             break;
    //         case ResultOf<T, E>.Error error:
    //             await onError(error);
    //             break;
    //     }
    // }

    /// <summary>
    /// Converts a ResultOf&lt;T, E&gt; into an Option&lt;T&gt;.
    /// If the result is Ok and the value is not null, returns Some(value); otherwise, returns None.
    /// </summary>
    public static Option<T> ToOption<T, E>(this ResultOf<T, E> result)
            => result.TryGetValue(out var value)
            ? Option.Some(value)
            : Option.None<T>();

    // ============================================
    // Recover - Error Recovery
    // ============================================

    /// <summary>
    /// Recovers from a failure by converting the error into a success value.
    /// If the result is Ok, returns the value unchanged. If the result is Fail,
    /// applies the recovery function to the error to produce a fallback value.
    /// This unwraps the ResultOf and always returns a T.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the error value</typeparam>
    /// <param name="result">The result to recover from</param>
    /// <param name="recovery">Function to convert an error into a fallback value</param>
    /// <returns>
    /// The success value if Ok, or the fallback value produced by the recovery function if Fail
    /// </returns>
    /// <remarks>
    /// <para>
    /// <c>Recover</c> is useful when you want to provide a fallback value based on the error.
    /// Unlike <see cref="GetValueOr{T,E}(ResultOf{T,E},T)"/> which takes a static default value,
    /// <c>Recover</c> lets you inspect the error to decide the fallback.
    /// </para>
    /// <para>
    /// This method always unwraps the ResultOf and returns a plain T value.
    /// If you want to stay in the ResultOf context, use Map or Bind instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// Basic recovery with default value:
    /// <code>
    /// ResultOf&lt;int, string&gt; result = ResultOf.Fail&lt;int&gt;("Not found");
    /// int value = result.Recover(error => -1);  // value = -1
    /// </code>
    ///
    /// Recovery based on error type:
    /// <code>
    /// enum DbError { NotFound, ConnectionLost, PermissionDenied }
    ///
    /// ResultOf&lt;User, DbError&gt; GetUser(int id) { ... }
    ///
    /// User user = GetUser(42).Recover(error => error switch
    /// {
    ///     DbError.NotFound => User.Anonymous,
    ///     DbError.ConnectionLost => User.Cached,
    ///     DbError.PermissionDenied => User.Guest,
    ///     _ => throw new InvalidOperationException($"Unexpected error: {error}")
    /// });
    /// </code>
    ///
    /// Fallback chain (try cache, then database, then default):
    /// <code>
    /// User user = TryGetFromCache(userId)
    ///     .Recover(cacheError => TryGetFromDatabase(userId)
    ///         .Recover(dbError => User.Default));
    /// </code>
    ///
    /// Error logging during recovery:
    /// <code>
    /// int value = ComputeValue()
    ///     .Recover(error =>
    ///     {
    ///         _logger.LogError($"Computation failed: {error}");
    ///         return 0;  // Return default value
    ///     });
    /// </code>
    /// </example>
    public static T Recover<T, E>(this ResultOf<T, E> result, Func<E, T> recovery)
        => result.Match(
            ok => ok.Value,
            error => recovery(error.ErrorValue)
        );

    /// <summary>
    /// Async version of Recover. Recovers from a failure using an async recovery function.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the error value</typeparam>
    /// <param name="result">The result to recover from</param>
    /// <param name="recovery">Async function to convert an error into a fallback value</param>
    /// <returns>
    /// A task that resolves to the success value if Ok, or the fallback value produced by the recovery function if Fail
    /// </returns>
    /// <example>
    /// <code>
    /// User user = await GetUserAsync(id)
    ///     .RecoverAsync(async error =>
    ///     {
    ///         await LogErrorAsync(error);
    ///         return await GetDefaultUserAsync();
    ///     });
    /// </code>
    /// </example>
    public static async Task<T> RecoverAsync<T, E>(
        this ResultOf<T, E> result,
        Func<E, Task<T>> recovery)
        => await result.Match(
            ok => Task.FromResult(ok.Value),
            async error => await recovery(error.ErrorValue)
        );

    /// <summary>
    /// Recovers from a failure in a Task&lt;ResultOf&gt; using an async recovery function.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the error value</typeparam>
    /// <param name="resultTask">The task containing the result to recover from</param>
    /// <param name="recovery">Async function to convert an error into a fallback value</param>
    /// <returns>
    /// A task that resolves to the success value if Ok, or the fallback value produced by the recovery function if Fail
    /// </returns>
    /// <example>
    /// <code>
    /// User user = await FetchUserAsync(id)
    ///     .RecoverAsync(async error => await GetCachedUserAsync(id));
    /// </code>
    /// </example>
    public static async Task<T> RecoverAsync<T, E>(
        this Task<ResultOf<T, E>> resultTask,
        Func<E, Task<T>> recovery)
    {
        var result = await resultTask;
        return await result.RecoverAsync(recovery);
    }

    /// <summary>
    /// Recovers from a failure in a Task&lt;ResultOf&gt; using a sync recovery function.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the error value</typeparam>
    /// <param name="resultTask">The task containing the result to recover from</param>
    /// <param name="recovery">Function to convert an error into a fallback value</param>
    /// <returns>
    /// A task that resolves to the success value if Ok, or the fallback value produced by the recovery function if Fail
    /// </returns>
    /// <example>
    /// <code>
    /// User user = await FetchUserAsync(id)
    ///     .Recover(error => User.Anonymous);
    /// </code>
    /// </example>
    public static async Task<T> Recover<T, E>(
        this Task<ResultOf<T, E>> resultTask,
        Func<E, T> recovery)
    {
        var result = await resultTask;
        return result.Recover(recovery);
    }
}
