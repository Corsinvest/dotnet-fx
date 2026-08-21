# Try Functions - Handling Exceptions

**Safely execute code and convert exceptions into `ResultOf` values**

## Overview

The `Try` pattern provides a functional approach to exception handling. Instead of wrapping code in `try-catch` blocks, you can use `Try` helper functions and extension methods to execute operations that might throw exceptions. If an exception occurs, it is caught and returned as the `Fail` case of a `ResultOf<T, E>` object. If the operation succeeds, the result is wrapped in the `Ok` case.

This allows you to treat exceptions as data and handle them using the same functional patterns you use for any other `ResultOf` value, such as `Map`, `Bind`, and `Match`.

## Why Use Try?

### The Problem with Traditional `try-catch`

Traditional `try-catch` blocks are imperative and can make code verbose and harder to chain together.

```csharp
// ❌ Traditional approach - verbose and breaks fluent chains
int ParseAndValidate(string input)
{
    try
    {
        var number = int.Parse(input);
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException("Number cannot be negative.");
        }
        return number;
    }
    catch (FormatException ex)
    {
        // Handle or log format error
        return -1;
    }
    catch (ArgumentOutOfRangeException ex)
    {
        // Handle or log validation error
        return -1;
    }
}
```

### The Solution with Try and ResultOf

The `Try` extensions integrate exception handling directly into a fluent pipeline, returning a `ResultOf` that encapsulates either the success value or the error.

```csharp
// ✅ Functional approach - clean, composable, and explicit
ResultOf<int, string> ParseAndValidate(string input) =>
    input.Try(int.Parse, ex => ex.Message) // Returns ResultOf<int, string>
         .Ensure(n => n >= 0, "Number cannot be negative.");

// Usage
var result = ParseAndValidate("-123");
result.Match(
    ok => Console.WriteLine($"Success: {ok.Value}"),
    fail => Console.WriteLine($"Error: {fail.ErrorValue}") // "Error: Number cannot be negative."
);
```

## Basic Usage

There are two ways to use `Try`: the static `ResultOf.Try` methods and the `.Try()` extension methods.

### 1. `ResultOf.Try` for Standalone Functions

Use `ResultOf.Try` to wrap a function or action that stands on its own.

```csharp
using Corsinvest.Fx.Functional;

// For functions that return a value
ResultOf<int, Exception> result = ResultOf.Try(() => int.Parse("42"));
// Ok(42)

ResultOf<int, Exception> failedResult = ResultOf.Try(() => int.Parse("abc"));
// Fail(FormatException)

// For actions that don't return a value
ResultOf<Unit, Exception> actionResult = ResultOf.Try(() => Console.WriteLine("Hello"));
// Ok(Unit.Value)
```

An `Action` produces nothing, but `ResultOf<void, E>` is not a thing you can write - `void` is not
a type in C#. [`Unit`](./Unit.md) stands in for it: one value, no information, so the result carries
only whether the action threw.

### 2. `.Try()` Extension Method for Pipelines

Use the `.Try()` extension method to integrate exception-throwing operations into a processing pipeline.

```csharp
using Corsinvest.Fx.Functional;

var result = "42"
    .Pipe(s => s.Trim())
    .Try(int.Parse) // Catches FormatException if parsing fails
    .Map(n => n * 2);

// result is Ok(84)

var failedResult = "abc"
    .Try(int.Parse)
    .Map(n => n * 2);

// failedResult is Fail(FormatException)
```

## What gets caught

`Try` catches `Exception` - every exception the delegate can throw, without distinction. That is
what makes it composable, and it is also its sharpest edge:

```csharp
using var cts = new CancellationTokenSource();
cts.Cancel();

var result = await ResultOf.TryAsync(async () =>
{
    await Task.Delay(1000, cts.Token);
    return 42;
});

result.IsOk;   // false - the cancellation came back as Fail(TaskCanceledException)
```

A cancellation becomes an ordinary failure rather than propagating, so a caller awaiting the token
never sees it. Where cancellation has to stay cancellation, keep the `await` outside `Try` and wrap
only the part that can genuinely fail - or map it back:

```csharp
var result = await ResultOf.TryAsync(work, ex => ex switch
{
    OperationCanceledException => throw ex,   // let cancellation through
    _ => MyError.From(ex)
});
```

The same applies to any exception your program is not meant to swallow. `Try` is for the errors you
intend to handle as data; it does not decide which ones those are.

## Custom Error Handling

By default, `Try` captures the raw `Exception`. You can provide an `errorMapper` function to convert the exception into a more specific error type, like a string, an enum, or a custom error record.

```csharp
public enum FileError { NotFound, AccessDenied, Unknown }

ResultOf<string, FileError> ReadFile(string path) =>
    path.Try(File.ReadAllText, ex => ex switch
    {
        FileNotFoundException => FileError.NotFound,
        UnauthorizedAccessException => FileError.AccessDenied,
        _ => FileError.Unknown
    });

// Usage
var content = ReadFile("invalid/path.txt");

content.Match(
    ok => Console.WriteLine("File content loaded."),
    fail => Console.WriteLine($"Error: {fail.ErrorValue}") // "Error: NotFound"
);
```

## Asynchronous Operations

The `Try` pattern works seamlessly with `async/await` using `TryAsync` helpers and extensions.

### 1. `ResultOf.TryAsync`

```csharp
public async Task<ResultOf<string, Exception>> FetchDataAsync(string url) =>
    await ResultOf.TryAsync(async () =>
    {
        using var client = new HttpClient();
        var response = await client.GetStringAsync(url);
        return response;
    });

// Call it
var result = await FetchDataAsync("https://example.com");
```

### 2. `.TryAsync()` Extension Method

The `.TryAsync()` extension is perfect for chaining asynchronous operations.

```csharp
using Corsinvest.Fx.Functional;

public async Task<ResultOf<JObject, string>> GetJsonAsync(string url) =>
    await url.TryAsync(
        async u => await new HttpClient().GetStringAsync(u),
        ex => "Network request failed"
    )
    .BindAsync(jsonString =>
        jsonString.Try(JObject.Parse, ex => "Invalid JSON format")
    );


// Call it
var result = await GetJsonAsync("https://api.example.com/data");
result.Match(
    ok => Console.WriteLine("JSON loaded."),
    fail => Console.WriteLine($"Error: {fail.ErrorValue}")
);
```

## API Reference

### `ResultOf` static methods

```csharp
// Synchronous execution
ResultOf<TOut, E> Try<TOut, E>(Func<TOut> func, Func<Exception, E> errorMapper)
ResultOf<Unit, E> Try<E>(Action action, Func<Exception, E> errorMapper)

// Asynchronous execution
Task<ResultOf<TOut, E>> TryAsync<TOut, E>(Func<Task<TOut>> func, Func<Exception, E> errorMapper)
Task<ResultOf<Unit, E>> TryAsync<E>(Func<Task> action, Func<Exception, E> errorMapper)
```

*Versions without `errorMapper` default to `Exception` as the error type.*

### `.Try()` Extension Methods

```csharp
// Synchronous extension
ResultOf<TOut, E> Try<TIn, TOut, E>(this TIn value, Func<TIn, TOut> func, Func<Exception, E> errorMapper)

// Asynchronous extensions
Task<ResultOf<TOut, E>> TryAsync<TIn, TOut, E>(this TIn value, Func<TIn, Task<TOut>> func, Func<Exception, E> errorMapper)
Task<ResultOf<TOut, E>> TryAsync<TIn, TOut, E>(this Task<TIn> valueTask, Func<TIn, Task<TOut>> func, Func<Exception, E> errorMapper)
Task<ResultOf<TOut, E>> Try<TIn, TOut, E>(this Task<TIn> valueTask, Func<TIn, TOut> func, Func<Exception, E> errorMapper)
```

*Versions without `errorMapper` default to `Exception` as the error type.*

## Relationship with `ResultOf<T, E>`

The `Try` pattern is a **factory for `ResultOf` instances**. It is the primary bridge between exception-based error handling and the functional, explicit error handling provided by `ResultOf`.

- **`Try` creates the `ResultOf`**: It executes unsafe code and wraps the outcome.
- **`ResultOf` methods consume it**: Once you have a `ResultOf` from `Try`, you can use all the standard `ResultOf` extensions (`Map`, `Bind`, `Recover`, `Ensure`, etc.) to process the result or the error.

```csharp
// Step 1: `Try` creates the ResultOf
ResultOf<int, string> result = "123".Try(int.Parse, ex => ex.Message);

// Step 2: `ResultOf` extensions consume and transform it
string message = result
    .Ensure(n => n > 0, "Must be positive")
    .Map(n => $"The number is {n}")
    .Recover(error => $"Failed to parse: {error}") // Handles both parse and ensure errors
    .GetValue(); // Unwraps the final string
```

---

## See also

- **[Unit](./Unit.md)** - the stand-in for `void` that `Try(Action)` returns
- **[ResultOf<T, E>](./ResultOf.md)** - what every `Try` hands back
- **[Pipe](./Pipe.md)** - chaining a `Try` into a pipeline
