/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

namespace Corsinvest.Fx.Defer;

internal sealed class DeferredAction(Action action) : IDisposable
{
    private Action? _action = action;

    public void Dispose()
    {
        // Thread-safe: Exchange ensures only one thread executes the action
        var action = Interlocked.Exchange(ref _action, null);
        if (action == null) { return; }

        try
        {
            action();
        }
        catch
        {
            // Suppress exceptions to allow other defers to execute
        }
    }
}

/// <summary>
/// Carries the state its action needs, so the caller can pass a <c>static</c> lambda instead of
/// one that captures. A capturing lambda allocates a display class plus a delegate on every call;
/// a static one is cached by the compiler, leaving this object as the only allocation.
/// </summary>
internal sealed class DeferredAction<TState>(TState state, Action<TState> action) : IDisposable
{
    private Action<TState>? _action = action;

    public void Dispose()
    {
        var action = Interlocked.Exchange(ref _action, null);
        if (action == null) { return; }

        try
        {
            action(state);
        }
        catch
        {
            // Suppress exceptions to allow other defers to execute
        }
    }
}
