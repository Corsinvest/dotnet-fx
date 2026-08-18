/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

namespace Corsinvest.Fx.Defer;

#nullable enable

/// <summary>
/// Go-style defer for C#.
/// Automatically executes cleanup actions when scope exits (LIFO order).
/// </summary>
/// <remarks>
/// <para>
/// Supports both synchronous and asynchronous cleanup actions. The async overload returns
/// <see cref="IAsyncDisposable"/> rather than <see cref="IDisposable"/>, so <c>await using</c> is
/// the only way to consume it - a plain <c>using</c> is a compile error, which is what keeps an
/// async cleanup from being blocked on by accident.
/// </para>
/// <para>
/// A deferred action that throws is swallowed, so the remaining defers still run - the same trade
/// a <c>finally</c> makes. Nothing is logged and nothing is rethrown, so a cleanup whose failure
/// matters has to handle it itself.
/// </para>
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
/// Asynchronous defer:
/// <code>
/// async Task ExampleAsync()
/// {
///     var connection = new DbConnection();
///     await using var _ = defer(async () =&gt; await connection.CloseAsync());
///     // CloseAsync() awaited on scope exit - never blocked on
/// }
/// </code>
///
/// Multiple defers (LIFO order):
/// <code>
/// void Example()
/// {
///     using var _1 = defer(() =&gt; Console.WriteLine("First"));
///     using var _2 = defer(() =&gt; Console.WriteLine("Second"));
///     using var _3 = defer(() =&gt; Console.WriteLine("Third"));
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
#pragma warning disable IDE1006 // Stili di denominazione
    public static IDisposable defer(Action action) => new DeferredAction(action);
#pragma warning restore IDE1006 // Stili di denominazione

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
#pragma warning disable IDE1006 // Stili di denominazione
    public static IAsyncDisposable defer(Func<Task> asyncAction) => new DeferredAsyncAction(asyncAction);
#pragma warning restore IDE1006 // Stili di denominazione
}
