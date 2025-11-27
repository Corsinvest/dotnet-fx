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
- **[Union Types](docs/Union.md)** - Custom discriminated unions with source generators
- **[Pipe Extensions](docs/Pipe.md)** - Universal pipeline pattern for any type
- **[Option<T>](docs/Option.md)** - Optional values *(planned)*

## Key Features

✅ **Type-safe** - Errors explicit in function signatures
✅ **Composable** - Chain operations with LINQ or pipe syntax
✅ **Exhaustive** - Pattern matching forces handling all cases
✅ **Fast** - Zero overhead, source generators, no reflection
✅ **Async-friendly** - First-class async/await support

## License

MIT License - see [LICENSE](../../LICENSE) for details

## Support

📖 [Documentation](https://github.com/Corsinvest/dotnet-fx)
🐛 [Issues](https://github.com/Corsinvest/dotnet-fx/issues)
💬 [Discussions](https://github.com/Corsinvest/dotnet-fx/discussions)
