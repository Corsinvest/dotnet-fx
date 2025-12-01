namespace Corsinvest.Fx.Functional;

/// <summary>
/// Extension methods for safely executing operations that might throw exceptions,
/// converting them to ResultOf for explicit error handling.
/// </summary>
/// <remarks>
/// These extensions provide a fluent way to convert exception-throwing operations
/// into type-safe ResultOf values, enabling functional error handling patterns.
///
/// Example:
/// <code>
/// var age = userInput
///     .Try(int.Parse)
///     .Ensure(x => x >= 0, "Age must be positive")
///     .GetValueOr(0);
/// </code>
/// </remarks>
public static class TryExtensions
{
    // ============================================
    // Synchronous Try
    // ============================================

    /// <summary>
    /// Executes a function on the input value and catches any exceptions,
    /// converting them to a ResultOf with Exception as the error type.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The input value</param>
    /// <param name="func">The function to execute that might throw an exception</param>
    /// <returns>
    /// A ResultOf containing either the function's return value on success,
    /// or the caught exception on failure
    /// </returns>
    /// <example>
    /// <code>
    /// // Safe parsing
    /// var result = "42".Try(int.Parse);
    /// // result: ResultOf&lt;int, Exception&gt;
    ///
    /// var invalid = "abc".Try(int.Parse);
    /// // invalid: ResultOf&lt;int, Exception&gt; with FormatException
    ///
    /// // In pipeline
    /// var age = userInput
    ///     .Pipe(s => s.Trim())
    ///     .Try(int.Parse)
    ///     .GetValueOr(0);
    /// </code>
    /// </example>
    public static ResultOf<TOut, Exception> Try<TIn, TOut>(
        this TIn value,
        Func<TIn, TOut> func)
    {
        try
        {
            return ResultOf.Ok<TOut, Exception>(func(value));
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, Exception>(ex);
        }
    }

    /// <summary>
    /// Executes a function on the input value and catches any exceptions,
    /// mapping them to a custom error type.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <typeparam name="E">The custom error type</typeparam>
    /// <param name="value">The input value</param>
    /// <param name="func">The function to execute that might throw an exception</param>
    /// <param name="errorMapper">Function to map the caught exception to the error type</param>
    /// <returns>
    /// A ResultOf containing either the function's return value on success,
    /// or the mapped error on failure
    /// </returns>
    /// <example>
    /// <code>
    /// // Map to string error
    /// var result = "42".Try(
    ///     int.Parse,
    ///     ex => $"Parse failed: {ex.Message}"
    /// );
    ///
    /// // Map to custom error enum
    /// enum ParseError { InvalidFormat, Overflow }
    ///
    /// var result = "999999999999".Try(
    ///     int.Parse,
    ///     ex => ex is FormatException
    ///         ? ParseError.InvalidFormat
    ///         : ParseError.Overflow
    /// );
    ///
    /// // File operations
    /// var content = "config.json".Try(
    ///     File.ReadAllText,
    ///     ex => $"File error: {ex.Message}"
    /// );
    /// </code>
    /// </example>
    public static ResultOf<TOut, E> Try<TIn, TOut, E>(
        this TIn value,
        Func<TIn, TOut> func,
        Func<Exception, E> errorMapper)
    {
        try
        {
            return ResultOf.Ok<TOut, E>(func(value));
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, E>(errorMapper(ex));
        }
    }

    // ============================================
    // Asynchronous Try
    // ============================================

    /// <summary>
    /// Executes an async function on the input value and catches any exceptions,
    /// converting them to a ResultOf with Exception as the error type.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The input value</param>
    /// <param name="func">The async function to execute that might throw an exception</param>
    /// <returns>
    /// A task that resolves to a ResultOf containing either the function's return value on success,
    /// or the caught exception on failure
    /// </returns>
    /// <example>
    /// <code>
    /// // Async HTTP request
    /// var data = await url.TryAsync(async u =>
    /// {
    ///     var response = await httpClient.GetAsync(u);
    ///     response.EnsureSuccessStatusCode();
    ///     return await response.Content.ReadAsStringAsync();
    /// });
    ///
    /// // Database query
    /// var user = await userId.TryAsync(async id =>
    ///     await dbContext.Users.FindAsync(id)
    /// );
    /// </code>
    /// </example>
    public static async Task<ResultOf<TOut, Exception>> TryAsync<TIn, TOut>(
        this TIn value,
        Func<TIn, Task<TOut>> func)
    {
        try
        {
            return ResultOf.Ok<TOut, Exception>(await func(value));
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, Exception>(ex);
        }
    }

    /// <summary>
    /// Executes an async function on the input value and catches any exceptions,
    /// mapping them to a custom error type.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <typeparam name="E">The custom error type</typeparam>
    /// <param name="value">The input value</param>
    /// <param name="func">The async function to execute that might throw an exception</param>
    /// <param name="errorMapper">Function to map the caught exception to the error type</param>
    /// <returns>
    /// A task that resolves to a ResultOf containing either the function's return value on success,
    /// or the mapped error on failure
    /// </returns>
    /// <example>
    /// <code>
    /// enum ApiError { NetworkError, Unauthorized, NotFound }
    ///
    /// var result = await userId.TryAsync(
    ///     async id => await FetchUserAsync(id),
    ///     ex => ex is HttpRequestException
    ///         ? ApiError.NetworkError
    ///         : ApiError.NotFound
    /// );
    ///
    /// // In pipeline
    /// var user = await url
    ///     .TryAsync(FetchDataAsync, ex => "Network error")
    ///     .Bind(ParseJson)
    ///     .Map(ExtractUser);
    /// </code>
    /// </example>
    public static async Task<ResultOf<TOut, E>> TryAsync<TIn, TOut, E>(
        this TIn value,
        Func<TIn, Task<TOut>> func,
        Func<Exception, E> errorMapper)
    {
        try
        {
            return ResultOf.Ok<TOut, E>(await func(value));
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, E>(errorMapper(ex));
        }
    }

    /// <summary>
    /// Executes an async function on a Task input value and catches any exceptions,
    /// converting them to a ResultOf with Exception as the error type.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="valueTask">The task containing the input value</param>
    /// <param name="func">The async function to execute that might throw an exception</param>
    /// <returns>
    /// A task that resolves to a ResultOf containing either the function's return value on success,
    /// or the caught exception on failure
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await GetDataAsync()
    ///     .TryAsync(ProcessAsync)
    ///     .Bind(ValidateAsync);
    /// </code>
    /// </example>
    public static async Task<ResultOf<TOut, Exception>> TryAsync<TIn, TOut>(
        this Task<TIn> valueTask,
        Func<TIn, Task<TOut>> func)
    {
        try
        {
            var value = await valueTask;
            return ResultOf.Ok<TOut, Exception>(await func(value));
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, Exception>(ex);
        }
    }

    /// <summary>
    /// Executes an async function on a Task input value and catches any exceptions,
    /// mapping them to a custom error type.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <typeparam name="E">The custom error type</typeparam>
    /// <param name="valueTask">The task containing the input value</param>
    /// <param name="func">The async function to execute that might throw an exception</param>
    /// <param name="errorMapper">Function to map the caught exception to the error type</param>
    /// <returns>
    /// A task that resolves to a ResultOf containing either the function's return value on success,
    /// or the mapped error on failure
    /// </returns>
    public static async Task<ResultOf<TOut, E>> TryAsync<TIn, TOut, E>(
        this Task<TIn> valueTask,
        Func<TIn, Task<TOut>> func,
        Func<Exception, E> errorMapper)
    {
        try
        {
            var value = await valueTask;
            return ResultOf.Ok<TOut, E>(await func(value));
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, E>(errorMapper(ex));
        }
    }

    /// <summary>
    /// Executes a sync function on a Task input value and catches any exceptions,
    /// converting them to a ResultOf with Exception as the error type.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="valueTask">The task containing the input value</param>
    /// <param name="func">The function to execute that might throw an exception</param>
    /// <returns>
    /// A task that resolves to a ResultOf containing either the function's return value on success,
    /// or the caught exception on failure
    /// </returns>
    public static async Task<ResultOf<TOut, Exception>> Try<TIn, TOut>(
        this Task<TIn> valueTask,
        Func<TIn, TOut> func)
    {
        try
        {
            var value = await valueTask;
            return ResultOf.Ok<TOut, Exception>(func(value));
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, Exception>(ex);
        }
    }

    /// <summary>
    /// Executes a sync function on a Task input value and catches any exceptions,
    /// mapping them to a custom error type.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <typeparam name="E">The custom error type</typeparam>
    /// <param name="valueTask">The task containing the input value</param>
    /// <param name="func">The function to execute that might throw an exception</param>
    /// <param name="errorMapper">Function to map the caught exception to the error type</param>
    /// <returns>
    /// A task that resolves to a ResultOf containing either the function's return value on success,
    /// or the mapped error on failure
    /// </returns>
    public static async Task<ResultOf<TOut, E>> Try<TIn, TOut, E>(
        this Task<TIn> valueTask,
        Func<TIn, TOut> func,
        Func<Exception, E> errorMapper)
    {
        try
        {
            var value = await valueTask;
            return ResultOf.Ok<TOut, E>(func(value));
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, E>(errorMapper(ex));
        }
    }
}
