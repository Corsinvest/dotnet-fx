# Corsinvest.Fx.Defer

Go-style `defer` for C#: run a cleanup action when the scope exits, written next to the thing it
undoes.

## When to use it

C# already handles one kind of cleanup well. `using` disposes an `IDisposable`, in reverse order,
on every exit path, with no allocation. **If the resource implements `IDisposable` or
`IAsyncDisposable`, use `using`** - it is shorter, faster and safer than anything this package
offers.

What `using` cannot express is an arbitrary action at scope exit. That is the gap `defer` fills:

| The cleanup is... | Use |
| --- | --- |
| `Dispose()` on an `IDisposable` - files, streams, connections, `HttpClient`, locks | `using` |
| restoring a value you changed | `defer` |
| decrementing a counter, leaving a logical section | `defer` |
| the other half of a `Begin`/`End` or `Acquire`/`Release` pair on an API that never implemented `IDisposable` | `defer` |
| deleting a temp file, flushing a diagnostic buffer, recording elapsed time | `defer` |

The common thread: in the second group **there is no object to dispose**, only an action to run.
Without `defer` you either write a bespoke struct wrapper for each case, or a `try/finally` that
puts the cleanup pages below the thing it belongs to.

```csharp
// The niche, in one example: the thing being undone is a value, not a resource.
var previous = Console.ForegroundColor;
Console.ForegroundColor = ConsoleColor.Red;
using var _ = defer(() => Console.ForegroundColor = previous);
```

## Installation

```bash
dotnet add package Corsinvest.Fx.Defer
```

`defer()` is globally available on install - no `using` directive needed. See
[Configuration](#configuration) to opt out.

## Usage

### Restoring state

The most common case. Save, change, put back:

```csharp
void WithInvariantCulture()
{
    var previous = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    using var _ = defer(() => CultureInfo.CurrentCulture = previous);

    // ... every exit path from here restores the culture, including exceptions.
}
```

### Balancing a counter

The decrement sits next to the increment, so an early `return` added later cannot skip it:

```csharp
void Walk(Node node)
{
    _depth++;
    using var _ = defer(() => _depth--);

    if (_depth > MaxDepth) { return; }   // still decremented
    foreach (var child in node.Children) { Walk(child); }
}
```

### Closing a `Begin`/`End` pair

Interop and older APIs are full of paired calls that never became `IDisposable`:

```csharp
native.BeginBatch();
using var _ = defer(() => native.EndBatch());
```

### Temporary files and side effects

```csharp
var tempPath = Path.GetTempFileName();
using var _ = defer(() => File.Delete(tempPath));
```

```csharp
var timer = Stopwatch.StartNew();
using var _ = defer(() => LogMetric("elapsed_ms", timer.ElapsedMilliseconds));
```

### LIFO order

Defers run last-registered-first, like nested `using` blocks:

```csharp
using var _1 = defer(() => Console.WriteLine("First"));
using var _2 = defer(() => Console.WriteLine("Second"));
Console.WriteLine("Main");
// Output: Main, Second, First
```

## Async cleanup

The async overload returns `IAsyncDisposable`, **not** `IDisposable`, so it can only be consumed
with `await using`:

```csharp
native.BeginBatch();
await using var _ = defer(async () => await native.EndBatchAsync());
```

Plain `using` on an async defer is a compile error:

```csharp
using var _ = defer(async () => await CleanupAsync());  // error CS8418
```

That is the point. The alternative design - returning `IDisposable` and blocking on the task
inside `Dispose()` - deadlocks under a synchronization context and starves the thread pool under
load. Here the mistake cannot reach runtime, and the cleanup is always awaited, never fired and
forgotten.

## Exception handling

An action that throws is swallowed so the remaining defers still run - the same trade `finally`
makes when its own body throws:

```csharp
using var _1 = defer(new Action(() => throw new Exception()));  // caught, does not propagate
using var _2 = defer(() => Console.WriteLine("OK"));            // still runs
// Output: OK
```

The `new Action(...)` is not decoration: a lambda whose body is only a `throw` has no natural
return type, so overload resolution picks `defer(Func<Task>)` and plain `using` then rejects the
`IAsyncDisposable` it returns (CS8418). Typing the lambda picks the synchronous overload.

**The suppression is deliberate but total.** A cleanup that fails does so silently - nothing is
logged, nothing is rethrown, and the caller cannot tell. Where that matters, handle it inside the
action:

```csharp
using var _ = defer(() =>
{
    try { native.EndBatch(); }
    catch (Exception ex) { _logger.LogError(ex, "EndBatch failed"); }
});
```

A `null` action is **not** covered by that trade. It is rejected with `ArgumentNullException` at
the call to `defer`, on the line where the mistake is, because silently running no cleanup would
break the only promise this package makes.

## Performance

`defer` is not free, and the honest comparison is against the `try/finally` it replaces. Allocations
per scope on .NET 8, restoring one value:

| | allocated |
| --- | --- |
| `try/finally` | 0 B |
| `defer(() => ...)` | 112 B |
| `defer(state, static ...)` | 32 B |
| `defer(methodGroup)` | 24 B |

```bash
dotnet run -c Release --project benchmarks/Corsinvest.Fx.Benchmarks -- --filter '*Defer*'
```

The 112 bytes are a display class plus a delegate for the capturing lambda, plus the object holding
it. Passing the state explicitly lets the lambda be `static`, which the compiler caches - leaving
only the 32-byte object:

```csharp
var previous = Console.ForegroundColor;
using var _ = defer(previous, static c => Console.ForegroundColor = c);
```

Use the plain form for readability; reach for the state-passing form where a measurement says it
matters. In a genuinely hot loop, use `finally`.

Disposal is **idempotent and thread-safe**: `Interlocked.Exchange` hands the action to exactly one
caller, so a `using` block plus a stray explicit `Dispose()` runs it once, and two threads racing
to dispose cannot both run it.

## Configuration

`defer()` is available globally by default. To opt out:

```xml
<PropertyGroup>
  <EnableDeferGlobalUsings>false</EnableDeferGlobalUsings>
</PropertyGroup>
```

Then import it where needed:

```csharp
using static Corsinvest.Fx.Defer.Defer;
```

## API Reference

```csharp
// Sync cleanup
IDisposable defer(Action action);
IDisposable defer<TState>(TState state, Action<TState> action);

// Async cleanup - requires 'await using'
IAsyncDisposable defer(Func<Task> asyncAction);
IAsyncDisposable defer<TState>(TState state, Func<TState, Task> asyncAction);
```

All four throw `ArgumentNullException` if the action is `null`. Method groups work as arguments:

```csharp
using var _1 = defer(native.EndBatch);
await using var _2 = defer(native.EndBatchAsync);
```

## Troubleshooting

### `error CS8418: 'IAsyncDisposable' ... Did you mean 'await using'?`

An async defer was consumed with plain `using`. Add `await`:

```csharp
await using var _ = defer(async () => await CleanupAsync());
```

If the lambda body is only a `throw`, this error means overload resolution picked the async
overload - type the lambda as `Action` to select the synchronous one.

### `error CS0121: the call is ambiguous`

A lambda that matches both `Action<TState>` and `Func<TState, Task>`. Declare the delegate type
explicitly:

```csharp
Action<int> cleanup = static _ => throw new InvalidOperationException();
using var _ = defer(0, cleanup);
```

### `'DeferredAction' is inaccessible due to its protection level`

The implementation types are internal by design; the `defer()` factory is the only entry point.

```csharp
var d = new DeferredAction(() => Cleanup());   // wrong
using var _ = defer(() => Cleanup());          // correct
```
