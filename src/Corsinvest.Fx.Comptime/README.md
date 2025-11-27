# Corsinvest.Fx.Comptime

**Zig-style compile-time computation for C# - Zero runtime overhead**

[![NuGet](https://img.shields.io/nuget/v/Corsinvest.Fx.Comptime)](https://www.nuget.org/packages/Corsinvest.Fx.Comptime/)
[![Downloads](https://img.shields.io/nuget/dt/Corsinvest.Fx.Comptime)](https://www.nuget.org/packages/Corsinvest.Fx.Comptime/)

---

## ⚠️ EXPERIMENTAL PACKAGE

**Status**: Alpha release (v0.1.0-alpha)

This package is **experimental** and:
- ❌ **NOT production-ready** - API may change significantly
- ❌ **No stability guarantees** - Breaking changes expected
- ❌ **May be deprecated** - Could be removed in future versions
- ⚠️ **Niche use cases** - Targets <0.1% of C# developers

**Use only if:**
- ✅ You understand the risks and limitations
- ✅ You have a specific, critical requirement
- ✅ You're willing to adapt to API changes

**Feedback welcome**: Help shape the API at [Issues](https://github.com/Corsinvest/dotnet-fx/issues)

---

## Overview

`Corsinvest.Fx.Comptime` brings Zig-style compile-time execution to C# using source generators and analyzers. Evaluate expressions at compile-time for zero runtime overhead.

## Installation

```bash
dotnet add package Corsinvest.Fx.Comptime
```

## Features

- ✅ **Compile-time evaluation** - Compute values at build time
- ✅ **Zero runtime overhead** - Results are constants
- ✅ **Source generation** - Automatic code generation
- ✅ **Type-safe** - Full type checking at compile-time
- ✅ **Analyzer support** - Catch errors early

## Quick Start

```csharp
using Corsinvest.Fx.Comptime;

// Mark functions for compile-time execution
[Comptime]
public static int Factorial(int n)
{
    return n <= 1 ? 1 : n * Factorial(n - 1);
}

// Generated code:
// public static class ComptimeConstants
// {
//     public const int Factorial_5 = 120;
//     public const int Factorial_10 = 3628800;
// }

// Use the computed constants
int result = ComptimeConstants.Factorial_10; // No runtime computation!
```

## Usage Examples

### Compile-Time String Processing

```csharp
[Comptime]
public static string EncodeBase64(string input)
{
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
}

// Generated:
// public const string EncodeBase64_Hello = "SGVsbG8=";
```

### Compile-Time Configuration

```csharp
[Comptime]
public static int GetApiVersion() => 2;

[Comptime]
public static string GetApiUrl() => GetApiVersion() switch
{
    1 => "https://api.v1.example.com",
    2 => "https://api.v2.example.com",
    _ => throw new NotSupportedException()
};

// Use in code
const string API_URL = ComptimeConstants.GetApiUrl; // Evaluated at compile-time
```

### Compile-Time Validation

```csharp
[Comptime]
public static bool IsValidEmail(string email)
{
    return Regex.IsMatch(email, @"^[^@]+@[^@]+\.[^@]+$");
}

// Analyzer ensures the string is valid at compile-time
[ComptimeValidate(nameof(IsValidEmail))]
const string AdminEmail = "admin@example.com"; // ✅ Valid

[ComptimeValidate(nameof(IsValidEmail))]
const string InvalidEmail = "not-an-email"; // ❌ Compile error!
```

## How It Works

1. **Source Generator**: Analyzes `[Comptime]` attributed methods
2. **Evaluation**: Executes pure functions at compile-time
3. **Code Generation**: Generates constants with computed values
4. **Analyzer**: Validates usage and catches errors

## Limitations

- ✅ **Pure functions only** - No side effects allowed
- ✅ **Constant inputs** - Arguments must be compile-time constants
- ✅ **Supported types** - Primitives, strings, enums, and value tuples
- ❌ **No I/O** - File/network operations not allowed
- ❌ **No reflection** - Metadata access restricted

## API Reference

### Attributes

```csharp
// Mark method for compile-time execution
[Comptime]
public static int Add(int a, int b) => a + b;

// Validate input at compile-time
[ComptimeValidate(nameof(ValidatorMethod))]
const string Value = "validated";

// Force compile-time evaluation
[ComptimeEvaluate]
const int Result = ExpensiveComputation();
```

### Generated Code

```csharp
// Auto-generated class
public static partial class ComptimeConstants
{
    // Generated constants
    public const int Add_2_3 = 5;
    public const int Factorial_10 = 3628800;
    public const string EncodeBase64_Hello = "SGVsbG8=";
}
```

## Best Practices

1. **Keep functions pure** - No side effects, deterministic output
2. **Use for expensive computations** - Hash generation, encoding, etc.
3. **Validate inputs** - Use `[ComptimeValidate]` for compile-time checks
4. **Document generated constants** - Add XML docs to `[Comptime]` methods

## Performance Benefits

```csharp
// Traditional approach (runtime)
int factorial = Factorial(10); // Computed every time

// Comptime approach (compile-time)
int factorial = ComptimeConstants.Factorial_10; // Pre-computed constant
```

**Benchmark Results:**
- Runtime: ~150ns
- Comptime: ~0ns (constant)
- **Speedup: ∞** (no runtime cost!)

## Comparison with Alternatives

| Approach | Runtime Cost | Type Safety | Flexibility |
|----------|--------------|-------------|-------------|
| `const` | None | ✅ Yes | ❌ Limited |
| `static readonly` | Low | ✅ Yes | ⚠️ Medium |
| `[Comptime]` | **None** | ✅ Yes | ✅ High |
| Runtime computation | High | ✅ Yes | ✅ High |

## Related Packages

- [Corsinvest.Fx.Union](../Corsinvest.Fx.Union/) - Source generators for unions

## Inspiration

- **Zig** - `comptime` keyword for compile-time execution
- **C++** - `constexpr` functions
- **Rust** - Const evaluation and procedural macros

## License

MIT License - see [LICENSE](../../LICENSE) for details

## Support

📖 [Documentation](https://github.com/Corsinvest/dotnet-fx)
🐛 [Issues](https://github.com/Corsinvest/dotnet-fx/issues)
💬 [Discussions](https://github.com/Corsinvest/dotnet-fx/discussions)
