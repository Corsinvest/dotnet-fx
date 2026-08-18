# Corsinvest.Fx.Defer

Go-style `defer` statements for C#. Automatically execute cleanup code when scope exits.

## Why Defer?

Resource management in C# traditionally requires verbose `try/finally` blocks or careful `using` statement placement. Languages like **Go**, **Zig**, **Swift**, and **Rust** have recognized that cleanup code should be **declared next to acquisition** for better readability and maintainability.

### The Problem

```csharp
// Traditional C# - cleanup far from acquisition
void ProcessFile(string path)
{
    var file = File.Open(path);
    var lock = AcquireLock();
    var connection = new SqlConnection(connString);

    try
    {
        connection.Open();
        // ... complex logic ...
    }
    finally
    {
        connection?.Close();    // Far from acquisition
        ReleaseLock(lock);      // Easy to forget
        file?.Close();          // Wrong order?
    }
}
```

### The Solution

```csharp
// With defer - cleanup next to acquisition
void ProcessFile(string path)
{
    var file = File.Open(path);
    using var _1 = defer(file.Close);      // Cleanup declared here!

    var lock = AcquireLock();
    using var _2 = defer(() => ReleaseLock(lock));  // Next to acquisition

    var connection = new SqlConnection(connString);
    connection.Open();
    using var _3 = defer(connection.Close);  // Clear intent

    // ... complex logic ...
    // All cleanup happens automatically in reverse order
}
```

### Language Comparison

| Language  | Syntax                         | Description               |
| --------- | ------------------------------ | ------------------------- |
| **Go**    | `defer cleanup()`              | Built-in language feature |
| **Zig**   | `defer cleanup()`              | Built-in language feature |
| **Swift** | `defer { cleanup() }`          | Built-in language feature |
| **Rust**  | `Drop` trait                   | Automatic via RAII        |
| **C#**    | `using var _ = defer(cleanup)` | This library!             |

### Benefits

✅ **Locality** - Cleanup code next to acquisition
✅ **Safety** - No forgotten cleanup calls
✅ **Order** - Automatic LIFO execution
✅ **Exceptions** - Cleanup runs even on exceptions
✅ **Readability** - Clear intent, less nesting

## Installation

```bash
dotnet add package Corsinvest.Fx.Defer
```

## Quick Start

```csharp
// No imports needed! defer() is globally available
void ProcessFile(string path)
{
    var file = File.Open(path);
    using var _ = defer(() => file.Close());

    // file.Close() called automatically when method exits
}
```

## Features

✅ LIFO execution - Last defer executes first (like Go)
✅ Exception safe - Defers execute even on exception
✅ Zero allocation overhead - Class-based with cleanup safety
✅ **Auto-global availability** - No imports needed!
✅ **Compile-time safety** - Async defers MUST use `await using` (compiler enforced)
✅ Non-blocking async - No thread blocking with async cleanup
✅ Simple API - Two overloads: `defer(Action)` and `defer(Func<Task>)`
✅ MSBuild integration - Automatic GlobalUsings via package reference

## Usage Examples

### Basic Defer Actions

```csharp
void Example()
{
    var lock = AcquireLock();
    using var _ = defer(() => ReleaseLock(lock));

    // Logic...
}  // ReleaseLock called automatically
```

### Real-World: File Processing with Cleanup

```csharp
void ProcessDataFile(string inputPath)
{
    // Open input file
    var input = File.OpenRead(inputPath);
    using var _1 = defer(() => input.Close());

    // Create temp file for processing
    var tempPath = Path.GetTempFileName();
    using var _2 = defer(() => File.Delete(tempPath));

    // Open output
    var output = File.OpenWrite(tempPath);
    using var _3 = defer(() => output.Close());

    // Process data...
    // All cleanup happens automatically in reverse order:
    // 1. output.Close()
    // 2. File.Delete(tempPath)
    // 3. input.Close()
}
```

### Real-World: Database Transaction

```csharp
async Task ProcessOrderAsync(Order order)
{
    var conn = new SqlConnection(connString);
    await conn.OpenAsync();
    await using var _ = defer(async () => await conn.CloseAsync());

    var tx = await conn.BeginTransactionAsync();
    await using var __ = defer(async () => await tx.RollbackAsync());

    var lockId = await AcquireDistributedLockAsync(order.Id);
    await using var ___ = defer(async () => await ReleaseDistributedLockAsync(lockId));

    // Process order...
    await SaveOrderAsync(order, conn, tx);
    await UpdateInventoryAsync(order, conn, tx);

    // Commit if all succeeded
    await tx.CommitAsync();

    // Cleanup happens in LIFO order:
    // 1. Release distributed lock
    // 2. Rollback transaction (if not committed)
    // 3. Close connection
}
```

### Real-World: HTTP Client with Metrics

```csharp
async Task<string> FetchDataAsync(string url)
{
    var timer = Stopwatch.StartNew();
    using var _1 = defer(() =>
    {
        timer.Stop();
        LogMetric("fetch_duration", timer.ElapsedMilliseconds);
    });

    var client = new HttpClient();
    using var _2 = defer(() => client.Dispose());

    var response = await client.GetAsync(url);
    return await response.Content.ReadAsStringAsync();

    // Cleanup:
    // 1. Dispose HttpClient
    // 2. Log metrics with elapsed time
}
```

### Real-World: Parallel Resource Management

```csharp
void ProcessMultipleFiles(string[] paths)
{
    var semaphore = new SemaphoreSlim(1);
    using var _1 = defer(() => semaphore.Dispose());

    var files = new List<FileStream>();
    using var _2 = defer(() => files.ForEach(f => f.Close()));

    foreach (var path in paths)
    {
        var file = File.OpenRead(path);
        files.Add(file);
    }

    // Process all files...

    // Cleanup in reverse:
    // 1. Close all files
    // 2. Dispose semaphore
}
```


## Comparison with Go

**Go:**

```go
defer cleanup()
```

**C# with Corsinvest.Fx.Defer:**

```csharp
using var _ = defer(() => cleanup());
```

Only `using var _ = ` prefix needed!

## Exception Handling

Exceptions in deferred actions are automatically suppressed to allow other defers to execute:

```csharp
void Example()
{
    using var _1 = defer(new Action(() => throw new Exception()));  // Caught, does not propagate
    using var _2 = defer(() => Console.WriteLine("OK"));            // Still executes
}
// Output: "OK"
```

The `new Action(...)` there is not decoration: a lambda whose body is only a `throw` has no natural
return type, so overload resolution picks `defer(Func<Task>)` and plain `using` then rejects the
`IAsyncDisposable` it returns (CS8418). Typing the lambda picks the synchronous overload.

**This suppression is deliberate but total.** A cleanup that fails does so silently - nothing is
logged, nothing is rethrown, and the caller cannot tell it happened. That is what lets the
remaining defers run, and it is the same trade `finally` makes when its own body throws. Where a
failing cleanup matters, handle it inside the action:

```csharp
using var _ = defer(() =>
{
    try { connection.Close(); }
    catch (Exception ex) { _logger.LogError(ex, "Failed to close connection"); }
});
```

## Configuration

### Auto Global Usings (Default: Enabled)

By default, `defer()` is automatically available globally when you install the package. To disable:

```xml
<PropertyGroup>
  <EnableDeferGlobalUsings>false</EnableDeferGlobalUsings>
</PropertyGroup>
```

Then you'll need to manually add:

```csharp
using static Corsinvest.Fx.Defer.Defer;
```

## Performance

`defer` is not free, and the honest comparison is against the `try/finally` it replaces. Measured
on a scope wrapping one call, per invocation:

| | allocation | 20M calls |
| --- | --- | --- |
| `try/finally` | 0 B | 79 ms |
| `defer` | 24 B | 303 ms |

The 24 bytes are the `DeferredAction` holding your delegate, and a lambda that captures adds its
own closure on top. In a hot loop, use `finally`. Everywhere else - which is most code - 24 bytes
buys the cleanup sitting next to the acquisition rather than pages below it.

What you get for that:

✅ No reflection - direct delegate calls only
✅ Non-blocking async - `await`ed, never `.Result` or `.Wait()`
✅ Compile-time enforcement - an async defer cannot be consumed by plain `using`
✅ Exception safety - cleanup runs on the exception path too
✅ Idempotent - `Interlocked.Exchange` means a double `Dispose()` runs the action once

## API Reference

### Static Methods

```csharp
// Simple API - only two methods needed!
IDisposable defer(Action action)              // Sync cleanup
IAsyncDisposable defer(Func<Task> asyncAction) // Async cleanup (requires 'await using')
```

### Usage Patterns

```csharp
// Synchronous cleanup
using var _1 = defer(() => Cleanup());

// Asynchronous cleanup (non-blocking)
await using var _2 = defer(async () => await CleanupAsync());

// Method groups supported
using var _3 = defer(SomeMethod);
await using var _4 = defer(SomeAsyncMethod);
```
## 🔧 Troubleshooting

### Error: "'DeferredAsyncAction' is inaccessible due to its protection level"

**Cause:** Attempting to use `new DeferredAsyncAction()` or `new DeferredAction()` directly instead of the `defer()` factory function. The constructors are internal to ensure the correct disposal pattern is used.

**Solution:** Always use the `defer()` function to create a deferred action.

```csharp
// ❌ Wrong
var deferred = new DeferredAsyncAction(async () => await CleanupAsync());

// ✅ Correct - for async cleanup
await using var _1 = defer(async () => await CleanupAsync());

// ✅ Correct - for sync cleanup
using var _2 = defer(() => Cleanup());
```


