# Corsinvest.Fx.Functional

**Functional programming toolkit for C# - Type-safe error handling, discriminated unions, and pipeline composition**

[![NuGet](https://img.shields.io/nuget/v/Corsinvest.Fx.Functional)](https://www.nuget.org/packages/Corsinvest.Fx.Functional/)
[![Downloads](https://img.shields.io/nuget/dt/Corsinvest.Fx.Functional)](https://www.nuget.org/packages/Corsinvest.Fx.Functional/)

## Overview

Brings powerful functional programming patterns to C#: type-safe error handling with `ResultOf<T, E>`, discriminated unions via the `IUnion<T1..T8>` marker interface, and universal pipeline composition with `Pipe`.

## Installation

```bash
dotnet add package Corsinvest.Fx.Functional
```

## Quick Examples

### ResultOf - Type-Safe Error Handling

```csharp
enum ValidationError { InvalidEmail, Required }

ResultOf<User, ValidationError> ValidateUser(string email) =>
    string.IsNullOrEmpty(email)
        ? ResultOf.Fail<User, ValidationError>(ValidationError.Required)
        : !email.Contains("@")
            ? ResultOf.Fail<User, ValidationError>(ValidationError.InvalidEmail)
            : ResultOf.Ok<User, ValidationError>(new User(email));

// Pattern matching
result.Match(
    ok => Console.WriteLine($"✓ Created: {ok.Value.Name}"),
    error => Console.WriteLine($"✗ Failed: {error.ErrorValue}")
);
```

📖 **[Read the complete ResultOf guide →](docs/ResultOf.md)**

### Union Types - Discriminated Unions

Case types are ordinary, independently declared types - the `IUnion<T1..T8>` marker interface just
names the closed set, and the generator emits one sealed wrapper per case:

```csharp
public record Circle(double Radius);
public record Rectangle(double Width, double Height);

public abstract partial record Shape : IUnion<Circle, Rectangle>;

double CalculateArea(Shape shape) => shape.Match(
    circle => Math.PI * circle.Radius * circle.Radius,
    rectangle => rectangle.Width * rectangle.Height
);
```

Or with a native `switch` - no discard arm needed, and a missing case is reported by name:

```csharp
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle(var circle) => Math.PI * circle.Radius * circle.Radius,
    Shape.Rectangle(var rectangle) => rectangle.Width * rectangle.Height
    // remove an arm and you get:
    // warning UNION004: Switch on union 'Shape' does not handle variant 'Rectangle'
};
```

Two analyzers make that work, and they install with the package - no extra reference, no setup:

| | |
| --- | --- |
| **UNION004** | Names every case a `switch` misses, with a **code fix** that adds them (<kbd>Ctrl</kbd>+<kbd>.</kbd> → *"Add missing union cases"*, Fix All supported) |
| **UNION005/006/007** | Suppress `CS8509`, `IDE0010` and `IDE0072`, so nothing nags you toward the `_` arm that would hide the next case you add |

Both work on `switch` **statements** too, which the compiler never checks for exhaustiveness.

The same works for `Option<T>` and `ResultOf<T, E>`, which are unions themselves.

Because the case types are ordinary declarations, the same type can take part in more than one
union, and - the shape an attribute-based design could never reach - a case can close over the
union root's own type parameter, which is exactly how `Option<T>` and `ResultOf<T, E>` are built:

```csharp
public record CreditCard(string Number);
public record PayPal(string Email);

public abstract partial record PaymentMethod : IUnion<CreditCard, PayPal>;

PaymentMethod method = new CreditCard("4111-1111-1111-1111"); // implicit conversion
```

📖 **[Read the complete Union Types guide →](docs/Union.md)**

### Pipe - Universal Pipeline Pattern

```csharp
var result = 5.0
    .Pipe(Power, 2.0)         // 25
    .Pipe(x => x + 10)        // 35
    .Pipe(Clamp, 0.0, 30.0);  // 30

var user = await userId
    .PipeAsync(FetchUserAsync)
    .TapAsync(u => LogAsync($"User: {u.Name}"))
    .PipeAsync(SaveToCacheAsync);
```

📖 **[Read the complete Pipe guide →](docs/Pipe.md)**

## Documentation

- **[ResultOf<T, E>](docs/ResultOf.md)** - Type-safe error handling with Railway-Oriented Programming
- **[Try Functions](docs/Try.md)** - Safely execute code and convert exceptions to ResultOf
- **[Union Types](docs/Union.md)** - Custom discriminated unions with source generators
- **[Pipe Extensions](docs/Pipe.md)** - Universal pipeline pattern for any type
- **[Option<T>](docs/Option.md)** - Optional values
- **[Unit](docs/Unit.md)** - The stand-in for `void` where a type is required

## 🔧 Troubleshooting

### Error: "ResultOf<T, E> does not contain a definition for 'Bind'" or other extension methods

**Cause:** The necessary using Corsinvest.Fx.Functional; directive is missing.

**Solution:** Add the using statement at the top of your file.

```csharp
using Corsinvest.Fx.Functional; // Add this
```

### Error: "The type or namespace name 'IUnion<>' could not be found"

**Cause:** Missing using Corsinvest.Fx.Functional; directive.

**Solution:**

```csharp
using Corsinvest.Fx.Functional; // Add this
```

### Error: "Union not generating code"

**Cause:** This usually happens if the source generator is not running correctly or there's an IDE cache issue.

**Solutions:**

1.  **Clean and Rebuild:** Run dotnet clean && dotnet build. This often resolves source generator issues.
2.  **Restart IDE:** Restarting Visual Studio or Rider can clear cached source generator outputs.
3.  **Check Definition:** Ensure your union root is declared `partial`. The generator needs to add members to it.

```csharp
public record Circle(double Radius);

public abstract partial record Shape : IUnion<Circle>; // ✅ Correct

public abstract record Shape2 : IUnion<Circle>; // ❌ Incorrect - missing 'partial'
```

### No UNION004 warning on a switch that is missing a case

**Cause:** The analyzer ships inside the package and is on by default, so the usual reason it stays
quiet is that the rule was turned off, the IDE is holding a stale copy, or the switch is not
actually missing a case.

**Solutions:**

1.  **Check `.editorconfig`** has not silenced it - this is the one setting that reliably makes it
    disappear:

```ini
dotnet_diagnostic.UNION004.severity = none      # silences it entirely
dotnet_diagnostic.UNION004.severity = warning   # the default
```

2.  **Confirm from the command line**, which bypasses any IDE caching:

```bash
dotnet build /warnaserror:UNION004
```

3.  **Restart the IDE.** Visual Studio and Rider both cache analyzer assemblies, so a freshly
    restored package sometimes needs one restart before its rules load.

4.  **Check the pattern really covers the case.** A guarded arm (`when`) or one matching a subset
    (`Shape.Circle { Value.Radius: 5 }`) does not count as handling that case - and conversely, a
    discard arm (`_ =>`) handles *everything*, so it silences UNION004 by design. See
    [what counts as handling a case](docs/Union.md#what-counts-as-handling-a-case).

### Warning: CS8602 "Dereference of a possibly null reference" on Option<T>.Value

**Cause:** You are trying to access the .Value of an Option<T> directly without first checking if it IsSome. This is unsafe and defeats the purpose of Option.

**Solution:** Use pattern matching (Match) or GetValueOr to safely access the value.

```csharp
// ❌ Wrong - can throw at runtime
var value = option.Value;

// ✅ Correct - safe pattern matching
var value = option.Match(
    some => some.Value,
    none => "default value"
);

// ? Also correct - explicit default
var value = option.GetValueOr("default value");
```

### Error: "Cannot implicitly convert type 'T' to 'ResultOf<T, E>'"

**Cause:** You are returning a plain value from a function that is declared to return a ResultOf<T, E>.

**Solution:** Explicitly wrap your return value in ResultOf.Ok() or ResultOf.Fail(). If you have enabled global usings for the library (default), you can just use Ok() and Fail().

```csharp
ResultOf<User, string> CreateUser(string email)
{
    // ❌ Wrong
    return new User(email);

    // ✅ Correct
    return Ok(new User(email)); // or ResultOf.Ok(...)
}
```

### Performance: "Using ResultOf/Option is slower than exceptions"

**This is not true.** In fact, ResultOf and Option are designed to be significantly **faster** than exception-based control flow for expected errors.

-   **No Stack Trace:** Exceptions are slow primarily because they need to capture and unwind the entire call stack. ResultOf and Option are simple struct returns.
-   **Predictable Control Flow:** The CPU's branch predictor works better with the predictable checks of IsOk or Match than with the high cost of a 	hrow.
-   **No Boxing:** The types are struct-based discriminated unions, avoiding heap allocations for the wrapper itself.

Exceptions should be reserved for truly **exceptional**, unrecoverable situations, not for predictable business logic failures like "user not found" or "invalid input".
