/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using static Corsinvest.Fx.Defer.Defer;

namespace Corsinvest.Fx.Defer.Tests;

public class DeferTests
{
    [Fact]
    public void Defer_SyncAction_ExecutesOnDispose()
    {
        // Arrange
        bool executed = false;

        // Act
        {
            using var _ = defer(() => executed = true);
            Assert.False(executed); // Not executed yet
        }

        // Assert
        Assert.True(executed); // Executed after dispose
    }

    [Fact]
    public async Task Defer_AsyncAction_ExecutesOnDisposeAsync()
    {
        // Arrange
        bool executed = false;

        // Act
        {
            await using var _ = defer(async () =>
            {
                await Task.Delay(10);
                executed = true;
            });
            Assert.False(executed); // Not executed yet
        }

        // Assert
        Assert.True(executed); // Executed after dispose
    }

    [Fact]
    public async Task Defer_AsyncAction_IsAwaitedBeforeScopeExits()
    {
        // The point of the IAsyncDisposable overload: the cleanup is awaited, not fired and
        // forgotten. A defer that merely started the work would leave `completed` false here.
        var completed = false;

        await using (defer(async () =>
        {
            await Task.Yield();
            await Task.Delay(20);
            completed = true;
        }))
        {
            Assert.False(completed);
        }

        Assert.True(completed);
    }

    [Fact]
    public void Defer_DisposedTwice_RunsTheActionOnce()
    {
        // Arrange
        var count = 0;
        var deferred = defer(() => count++);

        // Act - a using block plus an explicit Dispose is the shape that happens by accident
        deferred.Dispose();
        deferred.Dispose();

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Defer_AsyncDisposedTwice_RunsTheActionOnce()
    {
        // Arrange
        var count = 0;
        var deferred = defer(() => { count++; return Task.CompletedTask; });

        // Act
        await deferred.DisposeAsync();
        await deferred.DisposeAsync();

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public void Defer_DisposedConcurrently_RunsTheActionOnce()
    {
        // The reason Dispose uses Interlocked.Exchange rather than a null check: without it, two
        // threads arriving together both see a non-null field and both run the action.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var count = 0;
            var deferred = defer(() => Interlocked.Increment(ref count));

            using var barrier = new Barrier(2);
            var threads = new Thread[2];
            for (var i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    deferred.Dispose();
                });
                threads[i].Start();
            }

            foreach (var thread in threads) { thread.Join(); }

            Assert.Equal(1, count);
        }
    }

    [Fact]
    public void Defer_MultipleDefers_ExecuteInLIFOOrder()
    {
        // Arrange
        var executionOrder = new List<int>();

        // Act
        {
            using var _1 = defer(() => executionOrder.Add(1));
            using var _2 = defer(() => executionOrder.Add(2));
            using var _3 = defer(() => executionOrder.Add(3));
        }

        // Assert - LIFO order (Last In, First Out)
        Assert.Equal([3, 2, 1], executionOrder);
    }

    [Fact]
    public void Defer_WithException_StillExecutes()
    {
        // Arrange
        bool cleanupExecuted = false;

        // Act & Assert
        try
        {
            using var _ = defer(() => cleanupExecuted = true);
            throw new InvalidOperationException("Test exception");
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }

        Assert.True(cleanupExecuted); // Cleanup executed even with exception
    }

    [Fact]
    public void Defer_ExceptionInAction_Suppressed()
    {
        // Arrange
        bool secondCleanupExecuted = false;

        // Act - No exception should escape
        {
            Action throwAction = () => throw new InvalidOperationException("First cleanup throws");
            Action okAction = () => secondCleanupExecuted = true;

            using var _1 = defer(throwAction);
            using var _2 = defer(okAction);
        } // Dispose happens here (LIFO: _2 then _1)

        // Assert
        Assert.True(secondCleanupExecuted); // Second cleanup still executed
    }

    [Fact]
    public async Task Defer_ExceptionInAsyncAction_Suppressed()
    {
        var secondCleanupExecuted = false;

        await using (defer(() => { secondCleanupExecuted = true; return Task.CompletedTask; }))
        await using (defer(() => Task.FromException(new InvalidOperationException("throws"))))
        {
            // The inner defer throws on the way out; the outer one still runs.
        }

        Assert.True(secondCleanupExecuted);
    }

    [Fact]
    public void Defer_NullAction_Throws()
    {
        // A null action would mean the cleanup silently never happens - the one thing this package
        // exists to prevent. It is rejected at the call site, where the mistake is.
        Action? nullAction = null;

        var ex = Assert.Throws<ArgumentNullException>(() => defer(nullAction!));
        Assert.Equal("action", ex.ParamName);
    }

    [Fact]
    public void Defer_NullAsyncAction_Throws()
    {
        Func<Task>? nullAsyncAction = null;

        var ex = Assert.Throws<ArgumentNullException>(() => defer(nullAsyncAction!));
        Assert.Equal("asyncAction", ex.ParamName);
    }

    [Fact]
    public void Defer_WithState_PassesStateToAction()
    {
        // Arrange
        var captured = 0;

        // Act - a static lambda cannot read the enclosing scope, so the state has to arrive here
        {
            using var _ = defer(42, static state => Interlocked.Exchange(ref Sink, state));
        }
        captured = Sink;

        // Assert
        Assert.Equal(42, captured);
    }

    [Fact]
    public void Defer_WithStateNullAction_Throws()
    {
        Action<int>? nullAction = null;

        var ex = Assert.Throws<ArgumentNullException>(() => defer(1, nullAction!));
        Assert.Equal("action", ex.ParamName);
    }

    [Fact]
    public void Defer_WithState_DisposedTwice_RunsTheActionOnce()
    {
        var box = new int[1];
        var deferred = defer(box, static b => b[0]++);

        deferred.Dispose();
        deferred.Dispose();

        Assert.Equal(1, box[0]);
    }

    [Fact]
    public async Task Defer_WithStateAsync_PassesStateToAction()
    {
        var box = new int[1];

        await using (defer(box, static async b =>
        {
            await Task.Delay(10);
            b[0] = 7;
        }))
        {
            Assert.Equal(0, box[0]);
        }

        Assert.Equal(7, box[0]);
    }

    [Fact]
    public void Defer_WithStateAsyncNullAction_Throws()
    {
        Func<int, Task>? nullAsyncAction = null;

        var ex = Assert.Throws<ArgumentNullException>(() => defer(1, nullAsyncAction!));
        Assert.Equal("asyncAction", ex.ParamName);
    }

    [Fact]
    public void Defer_WithState_ExceptionInAction_Suppressed()
    {
        var box = new int[1];

        {
            using var _1 = defer(box, static b => b[0] = 1);
            // Typed explicitly: a lambda whose body is only a `throw` has no natural return type,
            // so overload resolution would otherwise pick the Func<TState, Task> overload.
            Action<int> throwAction = static _ => throw new InvalidOperationException("throws");
            using var _2 = defer(0, throwAction);
        }

        Assert.Equal(1, box[0]);
    }

    [Fact]
    public void Defer_RestoringState_TheCaseUsingCannotExpress()
    {
        // The package's actual niche: the thing being undone is a value, not a resource, so there
        // is no IDisposable to hand to `using`.
        var log = new List<string>();
        var level = "info";

        {
            var previous = level;
            level = "debug";
            using var _ = defer(() => level = previous);

            log.Add(level);
        }

        log.Add(level);
        Assert.Equal(["debug", "info"], log);
    }

    private static int Sink;
}
