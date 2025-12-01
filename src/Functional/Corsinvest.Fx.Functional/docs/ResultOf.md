# ResultOf<T, E> - Type-Safe Error Handling

**Railway-Oriented Programming for C#**

## Overview

`ResultOf<T, E>` is a discriminated union that represents the result of an operation that can either succeed with a value or fail with an error. It makes errors explicit in function signatures, forcing callers to handle both success and failure cases.

## Basic Usage

### Creating Results

```csharp
using Corsinvest.Fx.Functional;

enum ValidationError { InvalidEmail, TooShort, Required }

ResultOf<User, ValidationError> ValidateUser(string email, string name)
{
    if (string.IsNullOrEmpty(email))
        return ResultOf.Fail<User, ValidationError>(ValidationError.Required);

    if (!email.Contains("@"))
        return ResultOf.Fail<User, ValidationError>(ValidationError.InvalidEmail);

    return ResultOf.Ok<User, ValidationError>(new User(email, name));
}
```

### Pattern Matching

```csharp
var result = ValidateUser("test@example.com", "John");
result.Match(
    ok => Console.WriteLine($"User created: {ok.Value.Name}"),
    error => Console.WriteLine($"Validation failed: {error.ErrorValue}")
);
```

## Railway-Oriented Programming

Chain operations with automatic error propagation:

```csharp
// Using LINQ query syntax
var user = from validUser in ValidateUser(email, name)
           from savedUser in SaveToDatabase(validUser)
           from emailSent in SendWelcomeEmail(savedUser)
           select emailSent;

// Using Bind method
var result = ValidateUser(email, name)
    .Bind(SaveToDatabase)
    .Bind(SendWelcomeEmail);
```

If any step returns an error, the entire chain short-circuits and returns that error.

## Real-World Examples

### API Request Handling

```csharp
enum ApiError
{
    NetworkError,
    Unauthorized,
    NotFound,
    InvalidResponse
}

async Task<ResultOf<UserData, ApiError>> FetchUserAsync(int userId)
{
    try
    {
        var response = await _httpClient.GetAsync($"/api/users/{userId}");

        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ResultOf.Fail<UserData, ApiError>(ApiError.Unauthorized),
                HttpStatusCode.NotFound => ResultOf.Fail<UserData, ApiError>(ApiError.NotFound),
                _ => ResultOf.Fail<UserData, ApiError>(ApiError.InvalidResponse)
            };
        }

        var data = await response.Content.ReadFromJsonAsync<UserData>();
        return data is not null
            ? ResultOf.Ok<UserData, ApiError>(data)
            : ResultOf.Fail<UserData, ApiError>(ApiError.InvalidResponse);
    }
    catch
    {
        return ResultOf.Fail<UserData, ApiError>(ApiError.NetworkError);
    }
}

// Usage with LINQ
var result = await (
    from user in FetchUserAsync(123)
    from profile in FetchProfileAsync(user.Id)
    from settings in FetchSettingsAsync(user.Id)
    select new { user, profile, settings }
);
```

### Database Operations

```csharp
enum DbError
{
    NotFound,
    ConnectionLost,
    DuplicateKey,
    ValidationFailed
}

ResultOf<Order, DbError> CreateOrder(OrderRequest request)
{
    return from validated in ValidateOrderRequest(request)
           from customer in GetCustomer(validated.CustomerId)
           from inventory in CheckInventory(validated.Items)
           from order in SaveOrder(validated, customer)
           from notification in SendOrderConfirmation(order)
           select order;
}
```

### File Processing

```csharp
enum FileError
{
    NotFound,
    AccessDenied,
    InvalidFormat,
    ProcessingFailed
}

ResultOf<ProcessedData, FileError> ProcessFile(string path)
{
    return ReadFile(path)
        .Bind(ValidateFormat)
        .Bind(ParseContent)
        .Bind(TransformData)
        .Map(data => new ProcessedData(data));
}

ResultOf<string, FileError> ReadFile(string path)
{
    if (!File.Exists(path))
        return ResultOf.Fail<string, FileError>(FileError.NotFound);

    try
    {
        var content = File.ReadAllText(path);
        return ResultOf.Ok<string, FileError>(content);
    }
    catch (UnauthorizedAccessException)
    {
        return ResultOf.Fail<string, FileError>(FileError.AccessDenied);
    }
}
```

## API Reference

### Construction

```csharp
// Create successful result
ResultOf<T, E> ResultOf.Ok<T, E>(T value)
ResultOf<T, string> ResultOf.Ok<T>(T value)  // String error shorthand

// Create failed result
ResultOf<T, E> ResultOf.Fail<T, E>(E error)
ResultOf<T, string> ResultOf.Fail<T>(string error)  // String error shorthand
```

### Pattern Matching

```csharp
// Match with return value
TResult Match<TResult>(
    Func<Ok, TResult> onOk,
    Func<Fail, TResult> onFail)

// Match with side effects
void Match(
    Action<Ok> onOk,
    Action<Fail> onFail)

// Async match
Task<TResult> MatchAsync<TResult>(
    Func<Ok, Task<TResult>> onOk,
    Func<Fail, Task<TResult>> onFail)
```

### Transformations

```csharp
// Map - Transform success value
ResultOf<TNext, E> Map<TNext>(Func<T, TNext> mapper)
Task<ResultOf<TNext, E>> MapAsync<TNext>(Func<T, Task<TNext>> mapper)

// Bind - Chain operations that return ResultOf
ResultOf<TNext, E> Bind<TNext>(Func<T, ResultOf<TNext, E>> binder)
Task<ResultOf<TNext, E>> BindAsync<TNext>(Func<T, Task<ResultOf<TNext, E>>> binder)

// MapError - Transform error value
ResultOf<T, ENext> MapError<ENext>(Func<E, ENext> errorMapper)

// Ensure - Validate with predicate
ResultOf<T, E> Ensure(Func<T, bool> predicate, E error)
```

### Side Effects

```csharp
// Tap - Execute action on success (returns original result)
ResultOf<T, E> Tap(Action<T> action)
Task<ResultOf<T, E>> TapAsync(Func<T, Task> action)

// TapError - Execute action on failure
ResultOf<T, E> TapError(Action<E> action)
Task<ResultOf<T, E>> TapErrorAsync(Func<E, Task> action)

// FluentResults-compatible aliases (use for explicit code or FluentResults migration)
ResultOf<T, E> OnSuccess(Action<T> action)   // Alias for Tap
ResultOf<T, E> OnFailure(Action<E> action)   // Alias for TapError

// Example: Concise style
var result = ValidateUser(email, name)
    .Tap(user => _logger.LogInfo($"User validated: {user.Email}"))
    .Bind(SaveToDatabase)
    .Tap(user => _metrics.Increment("users.created"));

// Example: FluentResults style (same behavior, explicit naming)
var result2 = ValidateUser(email, name)
    .OnSuccess(user => _logger.LogInfo($"User validated: {user.Email}"))
    .Bind(SaveToDatabase)
    .OnSuccess(user => _metrics.Increment("users.created"))
    .OnFailure(error => _logger.LogError($"Validation failed: {error}"));
```

### Unwrapping

```csharp
// Get value or default
T GetValueOr(T defaultValue)
T GetValueOr(Func<E, T> errorToValue)
T? GetValueOrDefault()

// Get value or throw
T GetValueOrThrow(Func<E, Exception>? exceptionFactory = null)

// Try get value (Dictionary pattern)
bool TryGetValue(out T value)

// Recover - Convert error to value with inspection (unwraps ResultOf)
T Recover(Func<E, T> recovery)
Task<T> RecoverAsync(Func<E, Task<T>> recovery)
Task<T> RecoverAsync(this Task<ResultOf<T, E>> resultTask, Func<E, Task<T>> recovery)
Task<T> Recover(this Task<ResultOf<T, E>> resultTask, Func<E, T> recovery)
```

**Recover vs GetValueOr**: Use `Recover` when you need to inspect the error to decide the fallback value. Use `GetValueOr` when you have a static default value.

```csharp
// GetValueOr - static default
var value1 = result.GetValueOr(-1);

// Recover - inspect error for context-aware recovery
var value2 = result.Recover(error => error switch
{
    DbError.NotFound => -1,
    DbError.ConnectionLost => -2,
    _ => throw new InvalidOperationException($"Unexpected: {error}")
});

// Real-world: Try cache, then database, then default
User user = TryGetFromCache(userId)
    .Recover(cacheError =>
        TryGetFromDatabase(userId)
            .Recover(dbError => User.Default)
    );
```

### Checking State

```csharp
// Auto-generated by [Union] attribute (concise names)
bool IsOk        // True if result is successful
bool IsFail      // True if result is failure

// FluentResults-compatible aliases (explicit names)
bool IsSuccess   // Alias for IsOk
bool IsFailure   // Alias for IsFail

// Examples
if (result.IsOk) { /* ... */ }          // Concise style
if (result.IsSuccess) { /* ... */ }     // Explicit style
```

### Exception Handling

ResultOf provides **three ways** to convert exceptions into Results, giving you flexibility for different coding styles:

#### 1. Global Helper Functions (Recommended - Zero Boilerplate)

**Globally available by default** with `EnableTryGlobalUsings=true` (default):

```csharp
// No using statement needed! Try() is globally available
var result = Try(() => int.Parse("42"));
// result.IsSuccess = true, result.Value = 42

var failed = Try(() => int.Parse("invalid"));
// failed.IsFailure = true, failed.ErrorValue = FormatException

// Map exception to custom error type
enum ParseError { InvalidFormat, Overflow }
var result2 = Try(
    () => int.Parse("999999999999"),
    ex => ex is FormatException ? ParseError.InvalidFormat : ParseError.Overflow
);

// Async variants
var asyncResult = await TryAsync(async () =>
{
    var response = await httpClient.GetAsync("https://api.example.com/data");
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync();
});

var asyncMapped = await TryAsync(
    async () => await FetchUserAsync(42),
    ex => ex is HttpRequestException ? "Network error" : "Unknown error"
);
```

**Design Philosophy**: `TryHelper` provides standalone static methods for functional-style programming, similar to F#/Haskell. This gives you:
- ✅ **Zero boilerplate** - No `using static` needed with global usings
- ✅ **Consistency** - Same pattern as `Ok()`, `Fail()`, `Some()`, `None()`
- ✅ **Clean syntax** - Looks like a built-in language feature

**Disable if needed** (opt-out):
```xml
<PropertyGroup>
  <EnableTryGlobalUsings>false</EnableTryGlobalUsings>
</PropertyGroup>
```

#### Extension Methods (fluent style)

```csharp
// Convert any value's operation to Result
var result = "42".Try(x => int.Parse(x));
// result.IsSuccess = true, result.Value = 42

var piped = "invalid"
    .Try(x => int.Parse(x))
    .Match(
        ok => $"Parsed: {ok.Value}",
        fail => $"Error: {fail.ErrorValue.Message}"
    );

// Async extension
var user = await userId
    .TryAsync(async id => await _httpClient.GetFromJsonAsync<User>($"/api/users/{id}"))
    .MapAsync(async user => await EnrichUserDataAsync(user));
```

#### ResultOf Static Methods

```csharp
// Wrap function that might throw
ResultOf<T, Exception> ResultOf.Try(Func<T> func)
ResultOf<T, E> ResultOf.Try(Func<T> func, Func<Exception, E> errorMapper)

// Async variants
Task<ResultOf<T, Exception>> ResultOf.TryAsync(Func<Task<T>> func)
Task<ResultOf<T, E>> ResultOf.TryAsync(Func<Task<T>> func, Func<Exception, E> errorMapper)

// Convert Task to Result
Task<ResultOf<T, Exception>> ToResult(this Task<T> task)
Task<ResultOf<T, E>> ToResult(this Task<T> task, Func<Exception, E> errorMapper)
```

#### Real-World Exception Handling Example

```csharp
// API call with exception handling
enum ApiError { Network, Unauthorized, NotFound, ServerError }

var result = await TryAsync(
    async () =>
    {
        var response = await httpClient.GetAsync($"/api/users/{userId}");
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await response.Content.ReadFromJsonAsync<User>(),
            HttpStatusCode.Unauthorized => throw new UnauthorizedAccessException(),
            HttpStatusCode.NotFound => throw new KeyNotFoundException(),
            _ => throw new HttpRequestException("Server error")
        };
    },
    ex => ex switch
    {
        UnauthorizedAccessException => ApiError.Unauthorized,
        KeyNotFoundException => ApiError.NotFound,
        HttpRequestException => ApiError.ServerError,
        _ => ApiError.Network
    }
);

// Chain with other operations
var enrichedUser = result
    .Bind(user => ValidateUser(user))
    .Bind(user => EnrichUserData(user))
    .Tap(user => _logger.LogInfo($"User loaded: {user.Id}"))
    .Match(
        ok => ok.Value,
        fail => DefaultUser
    );
```

### Combining Results

```csharp
// Combine multiple results (collects all errors)
ResultOf<T[], List<E>> Combine(params ResultOf<T, E>[] results)

// Combine different types
ResultOf<(T1, T2), List<E>> Combine(ResultOf<T1, E> r1, ResultOf<T2, E> r2)
ResultOf<(T1, T2, T3), List<E>> Combine(ResultOf<T1, E> r1, ResultOf<T2, E> r2, ResultOf<T3, E> r3)
// ... up to 4 results
```

### Validation

```csharp
// Collect validation errors
ResultOf<T, List<E>> CollectErrors(T value, params Func<T, ResultOf<T, E>>[] validators)

// Validate with predicates
ResultOf<T, List<E>> Validate(T value, params (Func<T, bool> Predicate, E Error)[] validations)

// Example
var result = ResultOf.Validate(user,
    (u => u.Age >= 18, "Must be 18 or older"),
    (u => !string.IsNullOrEmpty(u.Email), "Email is required"),
    (u => u.Name.Length > 2, "Name must be longer than 2 characters")
);
```

### LINQ Support

```csharp
// Enables query syntax
ResultOf<TResult, E> Select<TResult>(Func<T, TResult> selector)
ResultOf<TResult, E> SelectMany<TNext, TResult>(
    Func<T, ResultOf<TNext, E>> selector,
    Func<T, TNext, TResult> resultSelector)

// Example
var result = from a in GetA()
             from b in GetB(a)
             where b > 0
             select a + b;
```

## Naming Conventions and Aliases

ResultOf provides **dual naming** to support different coding styles and library migrations:

### Union Cases

```csharp
ResultOf<int, string>.Ok(42)     // Success case
ResultOf<int, string>.Fail("error")  // Failure case
```

### Boolean Properties

| Generated (Concise) | Manual Alias (Explicit) | Purpose |
|---------------------|-------------------------|---------|
| `IsOk` | `IsSuccess` | Check if result succeeded |
| `IsFail` | `IsFailure` | Check if result failed |

**Usage:**
```csharp
// Functional programming style (concise)
if (result.IsOk) ProcessUser(result);

// Enterprise/FluentResults style (explicit)
if (result.IsSuccess) ProcessUser(result);

// Both compile to identical IL code - zero overhead!
```

### Side Effect Methods

| Primary Method | FluentResults Alias | Purpose |
|----------------|---------------------|---------|
| `Tap(action)` | `OnSuccess(action)` | Execute action on success |
| `TapError(action)` | `OnFailure(action)` | Execute action on failure |

**Comparison:**
```csharp
// Functional programming style (concise)
result
    .Tap(user => Log(user))
    .Bind(SaveToDb)
    .TapError(err => LogError(err));

// FluentResults style (explicit)
result
    .OnSuccess(user => Log(user))
    .Bind(SaveToDb)
    .OnFailure(err => LogError(err));
```

### Why Two Naming Styles?

1. **Concise names** (`IsOk`, `Tap`) - Preferred by functional programmers, matches F#/Rust conventions
2. **Explicit names** (`IsSuccess`, `OnSuccess`) - Preferred in enterprise contexts, FluentResults-compatible
3. **Zero cost** - All aliases are inline methods, identical performance
4. **Migration friendly** - Easier to migrate from FluentResults or CSharpFunctionalExtensions

### FluentResults Migration Guide

When migrating from FluentResults to ResultOf:

| FluentResults | ResultOf Equivalent | Notes |
|---------------|---------------------|-------|
| `Result<T>` | `ResultOf<T, string>` | String error type |
| `Result<T>.Success(value)` | `ResultOf.Ok<T>(value)` | Factory method |
| `Result<T>.Fail(error)` | `ResultOf.Fail<T>(error)` | Factory method |
| `result.IsSuccess` | `result.IsSuccess` or `result.IsOk` | ✅ Direct compatibility |
| `result.IsFailed` | `result.IsFailure` or `result.IsFail` | Similar naming |
| `result.Value` | `result.GetValueOrThrow()` | More explicit |
| `.OnSuccess(action)` | `.OnSuccess(action)` or `.Tap(action)` | ✅ Direct compatibility |
| `.OnFailure(action)` | `.OnFailure(action)` or `.TapError(action)` | ✅ Direct compatibility |

**Example migration:**
```csharp
// FluentResults code
Result<User> ValidateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Result.Fail<User>("Email is required");

    return Result.Ok(new User(email));
}

var result = ValidateUser(email)
    .OnSuccess(user => Log(user))
    .OnFailure(error => LogError(error));

if (result.IsSuccess)
    ProcessUser(result.Value);

// ResultOf equivalent (minimal changes)
ResultOf<User, string> ValidateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return ResultOf.Fail<User>("Email is required");

    return ResultOf.Ok(new User(email));
}

var result = ValidateUser(email)
    .OnSuccess(user => Log(user))
    .OnFailure(error => LogError(error));

if (result.IsSuccess)
    ProcessUser(result.GetValueOrThrow());
```

## Best Practices

### 1. Use Specific Error Types

```csharp
// ✅ Good - specific error enum
enum ValidationError { Required, TooShort, InvalidFormat }
ResultOf<Email, ValidationError> ValidateEmail(string input);

// ❌ Bad - generic string errors (loses type safety)
ResultOf<Email, string> ValidateEmail(string input);
```

### 2. Chain with Railway-Oriented Programming

```csharp
// ✅ Good - linear flow with LINQ
var result = from a in StepA()
             from b in StepB(a)
             from c in StepC(b)
             select c;

// ❌ Bad - nested pattern matching
StepA().Match(
    a => StepB(a).Match(
        b => StepC(b).Match(
            c => ...,
            error => ...),
        error => ...),
    error => ...);
```

### 3. Handle Both Cases

```csharp
// ✅ Good - explicit handling of both cases
result.Match(
    ok => ProcessSuccess(ok.Value),
    error => LogError(error.ErrorValue)
);

// ❌ Bad - throwing away error information
var value = result.Match(ok => ok.Value, error => default);
```

### 4. Use Tap for Side Effects

```csharp
// ✅ Good - side effects don't break the chain
var result = ValidateUser(email, name)
    .Tap(user => _logger.LogInfo($"User validated: {user.Email}"))
    .Bind(SaveToDatabase)
    .Tap(user => _metrics.Increment("users.created"))
    .Bind(SendWelcomeEmail);
```

## Comparison with Alternatives

| Feature         | ResultOf      | Exceptions                | Nullable                 |
| --------------- | ------------- | ------------------------- | ------------------------ |
| Type-safe       | ✅ Yes        | ❌ No                     | ⚠️ Limited               |
| Explicit errors | ✅ Yes        | ❌ No                     | ❌ No                    |
| Force handling  | ✅ Yes        | ❌ No                     | ⚠️ With nullable enabled |
| Performance     | ✅ Fast       | ❌ Slow (stack unwinding) | ✅ Fast                  |
| Async-friendly  | ✅ Yes        | ⚠️ Complex                | ✅ Yes                   |
| Composable      | ✅ Yes (LINQ) | ❌ No                     | ⚠️ Limited               |

## Performance

- ✅ **Zero allocations** - Discriminated union compiled to efficient code
- ✅ **No exceptions** - Faster than exception-based error handling (no stack unwinding)
- ✅ **Inline-friendly** - JIT can optimize method calls
- ✅ **Source generators** - Match methods generated at compile-time

## See Also

- [Option<T>](Option.md) - For optional values
- [Union Types](Union.md) - Create custom discriminated unions
- [Pipe Extensions](Pipe.md) - Universal pipeline pattern
- [Examples](Examples.md) - More real-world examples
