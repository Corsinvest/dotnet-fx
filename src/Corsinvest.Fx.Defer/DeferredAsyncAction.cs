/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

namespace Corsinvest.Fx.Defer;

internal sealed class DeferredAsyncAction(Func<Task> asyncAction) : IAsyncDisposable
{
    private Func<Task>? _asyncAction = asyncAction;

    public async ValueTask DisposeAsync()
    {
        // Thread-safe: Exchange ensures only one thread executes the async action
        var asyncAction = Interlocked.Exchange(ref _asyncAction, null);
        if (asyncAction == null) { return; }

        try
        {
            await asyncAction().ConfigureAwait(false);
        }
        catch
        {
            // Suppress exceptions to allow other defers to execute
        }
    }
}

/// <summary>
/// The state-passing counterpart of <see cref="DeferredAsyncAction"/>. See
/// <see cref="DeferredAction{TState}"/> for why the state is carried rather than captured.
/// </summary>
internal sealed class DeferredAsyncAction<TState>(TState state, Func<TState, Task> asyncAction) : IAsyncDisposable
{
    private Func<TState, Task>? _asyncAction = asyncAction;

    public async ValueTask DisposeAsync()
    {
        var asyncAction = Interlocked.Exchange(ref _asyncAction, null);
        if (asyncAction == null) { return; }

        try
        {
            await asyncAction(state).ConfigureAwait(false);
        }
        catch
        {
            // Suppress exceptions to allow other defers to execute
        }
    }
}
