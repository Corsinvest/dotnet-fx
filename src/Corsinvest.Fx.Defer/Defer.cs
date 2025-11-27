namespace Corsinvest.Fx.Defer;

#nullable enable

/// <summary>
/// Go-style defer for C#.
/// Automatically executes cleanup actions when scope exits (LIFO order).
/// </summary>
/// <remarks>
/// Supports both synchronous and asynchronous cleanup actions.
/// For async actions, prefer 'await using' pattern to avoid blocking.
/// </remarks>
/// <example>
/// Synchronous defer:
/// <code>
/// void Example()
/// {
///     var file = File.Open("test.txt");
///     using var _ = defer(() => file.Close());
///     // file.Close() called automatically on scope exit
/// }
/// </code>
///
/// Asynchronous defer (blocking):
/// <code>
/// void Example()
/// {
///     var connection = new DbConnection();
///     using var _ = defer(async () => await connection.CloseAsync());
///     // CloseAsync() called synchronously (blocks) on scope exit
/// }
/// </code>
///
/// Asynchronous defer (preferred - non-blocking):
/// <code>
/// async Task ExampleAsync()
/// {
///     var connection = new DbConnection();
///     await using var _ = defer(async () => await connection.CloseAsync());
///     // CloseAsync() called asynchronously (non-blocking) on scope exit
/// }
/// </code>
///
/// Multiple defers (LIFO order):
/// <code>
/// void Example()
/// {
///     using var _1 = defer(() => Console.WriteLine("First"));
///     using var _2 = defer(() => Console.WriteLine("Second"));
///     using var _3 = defer(() => Console.WriteLine("Third"));
///     Console.WriteLine("Main");
///     // Output: Main, Third, Second, First
/// }
/// </code>
/// </example>
public static class Defer
{
    /// <summary>
    /// Defers synchronous action execution to end of scope (LIFO order).
    /// </summary>
    /// <param name="action">Action to execute on disposal</param>
    /// <returns>Disposable that executes the action when disposed</returns>
    /// <example>
    /// <code>
    /// using var _ = defer(() => Console.WriteLine("Cleanup"));
    /// </code>
    /// </example>
    public static IDisposable defer(Action action) => new DeferredAction(action);

    /// <summary>
    /// Defers asynchronous action execution to end of scope (LIFO order).
    /// IMPORTANT: Must be used with 'await using', NOT with plain 'using'.
    /// </summary>
    /// <param name="asyncAction">Async action to execute on disposal</param>
    /// <returns>IAsyncDisposable that executes the async action when disposed</returns>
    /// <remarks>
    /// <para>
    /// This method returns <see cref="IAsyncDisposable"/> (not <see cref="IDisposable"/>),
    /// so it MUST be used with 'await using' pattern:
    /// </para>
    /// <code>
    /// await using var _ = defer(async () => await CleanupAsync());  // ✅ Correct
    /// </code>
    ///
    /// <para>
    /// Using plain 'using' will result in a compiler error:
    /// </para>
    /// <code>
    /// using var _ = defer(async () => await CleanupAsync());  // ❌ Compiler error!
    /// </code>
    ///
    /// <para>
    /// This design prevents accidental thread blocking at compile-time.
    /// </para>
    /// </remarks>
    /// <example>
    /// Async cleanup with database connection:
    /// <code>
    /// async Task ProcessDataAsync()
    /// {
    ///     var connection = new SqlConnection(connString);
    ///     await connection.OpenAsync();
    ///     await using var _ = defer(async () => await connection.CloseAsync());
    ///
    ///     // Use connection...
    ///
    ///     // connection.CloseAsync() called automatically (non-blocking)
    /// }
    /// </code>
    /// </example>
    public static IAsyncDisposable defer(Func<Task> asyncAction) => new DeferredAsyncAction(asyncAction);
}
