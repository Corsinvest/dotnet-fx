# Option<T> - Optional Values

**Type-safe representation of optional values**

## Overview

`Option<T>` is a type that represents an optional value - a value that may or may not be present. It's a type-safe alternative to `null` that forces you to explicitly handle both cases: when a value exists (`Some`) and when it doesn't (`None`).

## Why Option<T>?

### The Problem with Null

```csharp
// ❌ Traditional approach - prone to NullReferenceException
User? GetUser(int id) => _users.Find(id);

var user = GetUser(42);
Console.WriteLine(user.Name);  // 💥 NullReferenceException if not found!
```

### The Solution with Option<T>

```csharp
// ✅ Type-safe approach - forces handling
Option<User> GetUser(int id) =>
    _users.Find(id) is User user
        ? Option.Some(user)
        : Option.None<User>();

var user = GetUser(42);
user.Match(
    some => Console.WriteLine(some.Value.Name),
    none => Console.WriteLine("User not found")
);
```

## Basic Usage

### Creating Options

```csharp
using Corsinvest.Fx.Functional;

// Some - value is present
Option<int> someValue = Option.Some(42);
Option<string> someText = Option.Some("hello");

// None - value is absent
Option<int> noValue = Option.None<int>();
Option<string> noText = Option.None<string>();

// From nullable
int? nullableInt = 42;
Option<int> option = Option.FromNullable(nullableInt);

// Conditional
Option<User> user = condition
    ? Option.Some(new User("John"))
    : Option.None<User>();
```

### Pattern Matching

```csharp
Option<int> value = Option.Some(42);

// Match with return value
string result = value.Match(
    some => $"Value is {some.Value}",
    none => "No value"
);

// Match with side effects
value.Match(
    some => Console.WriteLine($"Found: {some.Value}"),
    none => Console.WriteLine("Not found")
);
```

## Transformations

### Map - Transform the Value

```csharp
Option<int> someNumber = Option.Some(5);

Option<int> doubled = someNumber.Map(x => x * 2);
// Some(10)

Option<string> text = someNumber.Map(x => x.ToString());
// Some("5")

Option<int> noNumber = Option.None<int>();
Option<int> stillNone = noNumber.Map(x => x * 2);
// None
```

### Bind - Chain Optional Operations

```csharp
Option<User> GetUser(int id) { /* ... */ }
Option<Address> GetAddress(User user) { /* ... */ }
Option<string> GetCity(Address address) { /* ... */ }

Option<string> city = GetUser(42)
    .Bind(GetAddress)
    .Bind(GetCity);

// Or with LINQ
Option<string> city =
    from user in GetUser(42)
    from address in GetAddress(user)
    from city in GetCity(address)
    select city;
```

### Filter - Conditional Filtering

```csharp
Option<int> value = Option.Some(42);

Option<int> evenOnly = value.Filter(x => x % 2 == 0);
// Some(42) - passes filter

Option<int> oddOnly = value.Filter(x => x % 2 != 0);
// None - doesn't pass filter
```

## Real-World Examples

### Configuration Lookup

```csharp
public class Configuration
{
    private readonly Dictionary<string, string> _settings;

    public Option<string> GetSetting(string key) =>
        _settings.TryGetValue(key, out var value)
            ? Option.Some(value)
            : Option.None<string>();

    public string GetSettingOrDefault(string key, string defaultValue) =>
        GetSetting(key).GetValueOr(defaultValue);
}

// Usage
var config = new Configuration();
var timeout = config.GetSetting("Timeout")
    .Map(int.Parse)
    .GetValueOr(30);
```

### Database Queries

```csharp
public class UserRepository
{
    public Option<User> FindById(int id)
    {
        var user = _dbContext.Users.Find(id);
        return user is not null
            ? Option.Some(user)
            : Option.None<User>();
    }

    public Option<User> FindByEmail(string email)
    {
        var user = _dbContext.Users
            .FirstOrDefault(u => u.Email == email);
        return Option.FromNullable(user);
    }
}

// Usage
var user = _userRepo.FindById(42)
    .Match(
        some => $"Found: {some.Value.Name}",
        none => "User not found"
    );
```

### Parsing Operations

```csharp
public static class Parser
{
    public static Option<int> ParseInt(string input) =>
        int.TryParse(input, out var result)
            ? Option.Some(result)
            : Option.None<int>();

    public static Option<DateTime> ParseDate(string input) =>
        DateTime.TryParse(input, out var result)
            ? Option.Some(result)
            : Option.None<DateTime>();
}

// Usage
var age = Parser.ParseInt("42")
    .Filter(x => x >= 18)
    .Match(
        some => $"Valid adult age: {some.Value}",
        none => "Invalid or underage"
    );

// Chaining
var result = Parser.ParseInt(userInput)
    .Bind(ValidateRange)
    .Map(CalculateDiscount)
    .GetValueOr(0);
```

### API Response Handling

```csharp
public class ApiClient
{
    public async Task<Option<User>> GetUserAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/users/{id}");
            if (!response.IsSuccessStatusCode)
                return Option.None<User>();

            var user = await response.Content.ReadFromJsonAsync<User>();
            return Option.FromNullable(user);
        }
        catch
        {
            return Option.None<User>();
        }
    }
}

// Usage
var userName = await _apiClient.GetUserAsync(42)
    .Map(user => user.Name)
    .GetValueOr("Unknown");
```

### Dictionary Operations

```csharp
public static class DictionaryExtensions
{
    public static Option<TValue> TryGetValue<TKey, TValue>(
        this Dictionary<TKey, TValue> dict,
        TKey key) where TKey : notnull
    {
        return dict.TryGetValue(key, out var value)
            ? Option.Some(value)
            : Option.None<TValue>();
    }
}

// Usage
var users = new Dictionary<int, User>();

var user = users.TryGetValue(42)
    .Filter(u => u.IsActive)
    .Match(
        some => DisplayUser(some.Value),
        none => ShowUserNotFound()
    );
```

### First/Last Operations

```csharp
public static class EnumerableExtensions
{
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
            return Option.Some(item);
        return Option.None<T>();
    }

    public static Option<T> FirstOrNone<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate)
    {
        foreach (var item in source)
            if (predicate(item))
                return Option.Some(item);
        return Option.None<T>();
    }
}

// Usage
var firstAdmin = users
    .FirstOrNone(u => u.IsAdmin)
    .Match(
        some => $"Admin: {some.Value.Name}",
        none => "No admin found"
    );
```

## API Reference

### Construction

```csharp
// Create Some
Option<T> Option.Some<T>(T value)

// Create None
Option<T> Option.None<T>()

// From nullable
Option<T> Option.FromNullable<T>(T? value) where T : class
Option<T> Option.FromNullable<T>(T? value) where T : struct
```

### Pattern Matching

```csharp
// Match with return value
TResult Match<TResult>(
    Func<Some, TResult> onSome,
    Func<None, TResult> onNone)

// Match with side effects
void Match(
    Action<Some> onSome,
    Action<None> onNone)

// Async match
Task<TResult> MatchAsync<TResult>(
    Func<Some, Task<TResult>> onSome,
    Func<None, Task<TResult>> onNone)
```

### Transformations

```csharp
// Map - Transform value if present
Option<TOut> Map<TOut>(Func<T, TOut> mapper)
Task<Option<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> mapper)

// Bind - Chain optional operations
Option<TOut> Bind<TOut>(Func<T, Option<TOut>> binder)
Task<Option<TOut>> BindAsync<TOut>(Func<T, Task<Option<TOut>>> binder)

// Filter - Keep value only if predicate is true
Option<T> Filter(Func<T, bool> predicate)

// Flatten - Flatten nested Option
Option<T> Flatten<T>(Option<Option<T>> option)

// OrElse - Provide fallback option
Option<T> OrElse(Option<T> alternative)
Option<T> OrElse(Func<Option<T>> alternativeFactory)
```

### Unwrapping

```csharp
// Get value or default
T GetValueOr(T defaultValue)
T GetValueOr(Func<T> defaultFactory)
T? GetValueOrDefault()

// Get value or throw
T GetValueOrThrow()
T GetValueOrThrow(Exception exception)
T GetValueOrThrow(Func<Exception> exceptionFactory)

// Try get value
bool TryGetValue(out T value)

// To nullable
T? ToNullable() where T : struct
```

### Conversion

```csharp
// To ResultOf
ResultOf<T, E> ToResult<E>(E errorValue)
ResultOf<T, E> ToResult<E>(Func<E> errorFactory)

// From ResultOf
Option<T> ToOption<E>(this ResultOf<T, E> result)
```

### Queries

```csharp
// Check if value is present
bool IsSome { get; }
bool IsNone { get; }

// Conditional execution
Option<T> Where(Func<T, bool> predicate)  // Alias for Filter
```

### LINQ Support

```csharp
// Enables query syntax
Option<TResult> Select<TResult>(Func<T, TResult> selector)
Option<TResult> SelectMany<TNext, TResult>(
    Func<T, Option<TNext>> selector,
    Func<T, TNext, TResult> resultSelector)
Option<T> Where(Func<T, bool> predicate)
```

## Best Practices

### 1. Use Option for Optional Return Values

```csharp
// ✅ Good - explicit optionality
Option<User> FindUser(int id);

// ❌ Bad - implicit null
User? FindUser(int id);
```

### 2. Chain with Bind for Sequential Operations

```csharp
// ✅ Good - clear chain
var result = GetUser(id)
    .Bind(GetProfile)
    .Bind(GetSettings)
    .Map(FormatDisplay);

// ❌ Bad - nested checks
var user = GetUser(id);
if (user.IsSome)
{
    var profile = GetProfile(user.Value);
    if (profile.IsSome)
    {
        var settings = GetSettings(profile.Value);
        // ...
    }
}
```

### 3. Use GetValueOr for Defaults

```csharp
// ✅ Good - explicit default
var timeout = config.GetSetting("Timeout")
    .Map(int.Parse)
    .GetValueOr(30);

// ❌ Bad - manual check
var timeoutOpt = config.GetSetting("Timeout");
int timeout = timeoutOpt.IsSome
    ? int.Parse(timeoutOpt.Value)
    : 30;
```

### 4. Use Flatten for Nested Options

```csharp
// ✅ Good - use Flatten to simplify
var result = TryParse("42")
    .Map(ValidatePositive)
    .Flatten();  // Option<int>

// ❌ Bad - nested option
var result = TryParse("42")
    .Map(ValidatePositive);  // Option<Option<int>>

// ✅ Or use Bind directly
var result = TryParse("42")
    .Bind(ValidatePositive);  // Option<int>
```

### 5. Use OrElse for Fallback Chains

```csharp
// ✅ Good - explicit fallback chain
var user = GetFromCache(id)
    .OrElse(() => GetFromDatabase(id))
    .OrElse(() => GetDefaultUser());

// ❌ Bad - manual checking
var user = GetFromCache(id);
if (user.IsNone)
{
    user = GetFromDatabase(id);
    if (user.IsNone)
    {
        user = GetDefaultUser();
    }
}
```

### 6. Combine with ResultOf for Error Handling

```csharp
// ✅ Good - explicit errors
ResultOf<User, UserError> GetUser(int id)
{
    return FindUser(id)
        .ToResult(UserError.NotFound);
}

// ❌ Bad - generic None
Option<User> GetUser(int id)  // Why None? Not found? Error? Permission denied?
```

## Comparison with Alternatives

| Feature           | Option<T>       | T? (nullable)            | ResultOf<T, E>           |
| ----------------- | --------------- | ------------------------ | ------------------------ |
| Type-safe         | ✅ Yes          | ⚠️ With nullable enabled | ✅ Yes                   |
| Explicit absence  | ✅ Yes          | ⚠️ Implicit              | ✅ Yes (with error)      |
| Force handling    | ✅ Yes          | ⚠️ With warnings         | ✅ Yes                   |
| Error information | ❌ No           | ❌ No                    | ✅ Yes                   |
| Composable        | ✅ Yes (LINQ)   | ⚠️ Limited               | ✅ Yes (LINQ)            |
| Use case          | Optional values | Nullable values          | Operations that can fail |

### When to Use Each

**Use `Option<T>` when:**

- A value may or may not be present
- Absence of value is **not an error** (e.g., optional config, user preference)
- You don't need to explain **why** the value is absent

**Use `ResultOf<T, E>` when:**

- An operation can **fail** with specific errors
- You need to communicate **why** something failed
- Errors need to be handled differently

**Use `T?` (nullable) when:**

- Working with legacy code or APIs
- Simple scenarios where Option overhead isn't justified
- Nullable reference types are enabled and enforced

## Examples: Option vs ResultOf

```csharp
// Option - value may be absent, but it's not an error
Option<string> GetUserPreference(string key);
Option<User> FindOptionalMentor(int userId);
Option<CachedData> GetFromCache(string key);

// ResultOf - operation can fail with specific errors
ResultOf<User, DbError> LoadUser(int id);
ResultOf<Order, ValidationError> ValidateOrder(OrderRequest request);
ResultOf<PaymentResult, PaymentError> ProcessPayment(Payment payment);

// Combining both
ResultOf<Option<string>, ConfigError> GetOptionalSetting(string key);
// ^ May fail to load config (error), but setting itself is optional (option)
```

## Performance

- ✅ **Zero overhead** - Compiled to discriminated union
- ✅ **No boxing** - Value types remain unboxed
- ✅ **Inline-friendly** - JIT can optimize simple operations
- ✅ **No exceptions** - Faster than try-catch patterns

## See Also

- [ResultOf<T, E>](ResultOf.md) - For operations that can fail with errors
- [Union Types](Union.md) - Create custom discriminated unions
- [Pipe Extensions](Pipe.md) - Chain transformations on Option values
- [Examples](Examples.md) - More real-world usage patterns

## Implementation Note

Option<T> can be implemented as a discriminated union:

```csharp
[Union]
public partial record Option<T>
{
    public partial record Some(T Value);
    public partial record None();
}
```

This gives you exhaustive pattern matching and type safety with zero runtime overhead.
