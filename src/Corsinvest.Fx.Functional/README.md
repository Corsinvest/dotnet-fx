# Corsinvest.Fx.Functional

**Functional programming toolkit for C# - Type-safe error handling, discriminated unions, and pipeline composition**

[![NuGet](https://img.shields.io/nuget/v/Corsinvest.Fx.Functional)](https://www.nuget.org/packages/Corsinvest.Fx.Functional/)
[![Downloads](https://img.shields.io/nuget/dt/Corsinvest.Fx.Functional)](https://www.nuget.org/packages/Corsinvest.Fx.Functional/)

## Overview

Brings powerful functional programming patterns to C#: type-safe error handling with `ResultOf<T, E>`, discriminated unions via `[Union]` attribute, and universal pipeline composition with `Pipe`.

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

```csharp
[Union]
public partial record Shape
{
    public partial record Circle(double Radius);
    public partial record Rectangle(double Width, double Height);
}

double CalculateArea(Shape shape) => shape.Match(
    circle => Math.PI * circle.Radius * circle.Radius,
    rectangle => rectangle.Width * rectangle.Height
);
```

📖 **[Read the complete Union Types guide →](docs/Union.md)**

### Pipe - Universal Pipeline Pattern

```csharp
var result = 5.0
    .Pipe(Power, 2)           // 25
    .Pipe(x => x + 10)        // 35
    .Pipe(Clamp, 0, 30);      // 30

var user = await userId
    .PipeAsync(FetchUserAsync)
    .PipeTapAsync(u => LogAsync($"User: {u.Name}"))
    .PipeAsync(SaveToCacheAsync);
```

📖 **[Read the complete Pipe guide →](docs/Pipe.md)**

## Documentation

- **[ResultOf<T, E>](docs/ResultOf.md)** - Type-safe error handling with Railway-Oriented Programming
- **[Try Functions](docs/Try.md)** - Safely execute code and convert exceptions to ResultOf
- **[Union Types](docs/Union.md)** - Custom discriminated unions with source generators
- **[Pipe Extensions](docs/Pipe.md)** - Universal pipeline pattern for any type
- **[Option<T>](docs/Option.md)** - Optional values *(planned)*

## ?? Troubleshooting

### Error: "ResultOf<T, E> does not contain a definition for 'Bind'" or other extension methods

**Cause:** The necessary using Corsinvest.Fx.Functional; directive is missing.

**Solution:** Add the using statement at the top of your file.

`csharp
using Corsinvest.Fx.Functional; // Add this
`

### Error: "The type or namespace name 'UnionAttribute' could not be found"

**Cause:** Missing using Corsinvest.Fx.Functional; directive.

**Solution:**

`csharp
using Corsinvest.Fx.Functional; // Add this
`

### Error: "Union attribute not generating code"

**Cause:** This usually happens if the source generator is not running correctly or there's an IDE cache issue.

**Solutions:**

1.  **Clean and Rebuild:** Run dotnet clean && dotnet build. This often resolves source generator issues.
2.  **Restart IDE:** Restarting Visual Studio or Rider can clear cached source generator outputs.
3.  **Check Definition:** Ensure your union type is a public partial record or public partial class. The [Union] attribute requires partial.

    `csharp
    [Union] // ? Correct
    public partial record Shape
    {
        public partial record Circle(double Radius);
    }

    [Union] // ? Incorrect - missing 'partial'
    public record Shape { ... }
    `

### Warning: CS8602 "Dereference of a possibly null reference" on Option<T>.Value

**Cause:** You are trying to access the .Value of an Option<T> directly without first checking if it IsSome. This is unsafe and defeats the purpose of Option.

**Solution:** Use pattern matching (Match) or GetValueOr to safely access the value.

`csharp
// ? Wrong - can throw at runtime
var value = option.Value;

// ? Correct - safe pattern matching
var value = option.Match(
    some => some.Value,
    none => "default value"
);

// ? Also correct - explicit default
var value = option.GetValueOr("default value");
`

### Error: "Cannot implicitly convert type 'T' to 'ResultOf<T, E>'"

**Cause:** You are returning a plain value from a function that is declared to return a ResultOf<T, E>.

**Solution:** Explicitly wrap your return value in ResultOf.Ok() or ResultOf.Fail(). If you have enabled global usings for the library (default), you can just use Ok() and Fail().

`csharp
ResultOf<User, string> CreateUser(string email)
{
    // ? Wrong
    return new User(email);

    // ? Correct
    return Ok(new User(email)); // or ResultOf.Ok(...)
}
`

### Performance: "Using ResultOf/Option is slower than exceptions"

**This is not true.** In fact, ResultOf and Option are designed to be significantly **faster** than exception-based control flow for expected errors.

-   **No Stack Trace:** Exceptions are slow primarily because they need to capture and unwind the entire call stack. ResultOf and Option are simple struct returns.
-   **Predictable Control Flow:** The CPU's branch predictor works better with the predictable checks of IsOk or Match than with the high cost of a 	hrow.
-   **No Boxing:** The types are struct-based discriminated unions, avoiding heap allocations for the wrapper itself.

Exceptions should be reserved for truly **exceptional**, unrecoverable situations, not for predictable business logic failures like "user not found" or "invalid input".

## Key Features

✅ **Type-safe** - Errors explicit in function signatures
✅ **Composable** - Chain operations with LINQ or pipe syntax
✅ **Exhaustive** - Pattern matching forces handling all cases
✅ **Fast** - Zero overhead, source generators, no reflection
✅ **Async-friendly** - First-class async/await support


