namespace Corsinvest.Fx.Functional;

/// <summary>
/// Helper class to safely execute synchronous and asynchronous operations,
/// converting exceptions into a ResultOf type for functional error handling.
/// </summary>
public static class TryHelper
{
    // ==========================
    // Synchronous Methods
    // ==========================

    /// <summary>
    /// Executes a function that returns a value, capturing any exception.
    /// </summary>
    /// <typeparam name="TOut">The type returned by the function</typeparam>
    /// <param name="func">The function to execute</param>
    /// <returns>A ResultOf containing either the value or an Exception</returns>
    public static ResultOf<TOut, Exception> Try<TOut>(Func<TOut> func)
        => Try(func, ex => ex);

    /// <summary>
    /// Executes a function that returns a value, capturing exceptions and mapping them to a custom error type.
    /// </summary>
    /// <typeparam name="TOut">The type returned by the function</typeparam>
    /// <typeparam name="E">The type of the custom error</typeparam>
    /// <param name="func">The function to execute</param>
    /// <param name="errorMapper">Function that converts Exception to a custom error type</param>
    /// <returns>A ResultOf containing either the value or the mapped error</returns>
    public static ResultOf<TOut, E> Try<TOut, E>(Func<TOut> func, Func<Exception, E> errorMapper)
    {
        try
        {
            return ResultOf.Ok<TOut, E>(func());
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, E>(errorMapper(ex));
        }
    }

    /// <summary>
    /// Executes an action that does not return a value, capturing any exception.
    /// Uses Unit as a placeholder return value.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>A ResultOf containing either Unit or an Exception</returns>
    public static ResultOf<Unit, Exception> Try(Action action)
        => Try(() => { action(); return Unit.Value; });

    /// <summary>
    /// Executes an action that does not return a value, capturing exceptions and mapping them to a custom error type.
    /// Uses Unit as a placeholder return value.
    /// </summary>
    /// <typeparam name="E">The type of the custom error</typeparam>
    /// <param name="action">The action to execute</param>
    /// <param name="errorMapper">Function that converts Exception to a custom error type</param>
    /// <returns>A ResultOf containing either Unit or the mapped error</returns>
    public static ResultOf<Unit, E> Try<E>(Action action, Func<Exception, E> errorMapper)
        => Try(() => { action(); return Unit.Value; }, errorMapper);

    // ==========================
    // Asynchronous Methods
    // ==========================

    /// <summary>
    /// Executes an async function that returns a value, capturing any exception.
    /// </summary>
    /// <typeparam name="TOut">The type returned by the task</typeparam>
    /// <param name="func">The async function to execute</param>
    /// <returns>A task resolving to a ResultOf containing either the value or an Exception</returns>
    public static async Task<ResultOf<TOut, Exception>> TryAsync<TOut>(Func<Task<TOut>> func)
        => await TryAsync(func, ex => ex);

    /// <summary>
    /// Executes an async function that returns a value, capturing exceptions and mapping them to a custom error type.
    /// </summary>
    /// <typeparam name="TOut">The type returned by the task</typeparam>
    /// <typeparam name="E">The type of the custom error</typeparam>
    /// <param name="func">The async function to execute</param>
    /// <param name="errorMapper">Function that converts Exception to a custom error type</param>
    /// <returns>A task resolving to a ResultOf containing either the value or the mapped error</returns>
    public static async Task<ResultOf<TOut, E>> TryAsync<TOut, E>(Func<Task<TOut>> func, Func<Exception, E> errorMapper)
    {
        try
        {
            return ResultOf.Ok<TOut, E>(await func());
        }
        catch (Exception ex)
        {
            return ResultOf.Fail<TOut, E>(errorMapper(ex));
        }
    }

    /// <summary>
    /// Executes an async action that does not return a value, capturing any exception.
    /// Uses Unit as a placeholder return value.
    /// </summary>
    /// <param name="action">The async action to execute</param>
    /// <returns>A task resolving to a ResultOf containing either Unit or an Exception</returns>
    public static async Task<ResultOf<Unit, Exception>> TryAsync(Func<Task> action)
        => await TryAsync(async () => { await action(); return Unit.Value; });

    /// <summary>
    /// Executes an async action that does not return a value, capturing exceptions and mapping them to a custom error type.
    /// Uses Unit as a placeholder return value.
    /// </summary>
    /// <typeparam name="E">The type of the custom error</typeparam>
    /// <param name="action">The async action to execute</param>
    /// <param name="errorMapper">Function that converts Exception to a custom error type</param>
    /// <returns>A task resolving to a ResultOf containing either Unit or the mapped error</returns>
    public static async Task<ResultOf<Unit, E>> TryAsync<E>(Func<Task> action, Func<Exception, E> errorMapper)
        => await TryAsync(async () => { await action(); return Unit.Value; }, errorMapper);
}
