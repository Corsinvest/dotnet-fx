# Corsinvest.Fx.CompileTime

**Zig-style compile-time computation for C# - Zero runtime overhead**

[![NuGet](https://img.shields.io/nuget/v/Corsinvest.Fx.CompileTime)](https://www.nuget.org/packages/Corsinvest.Fx.CompileTime/)
[![Downloads](https://img.shields.io/nuget/dt/Corsinvest.Fx.CompileTime)](https://www.nuget.org/packages/Corsinvest.Fx.CompileTime/)

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

`Corsinvest.Fx.CompileTime` brings Zig-style compile-time execution to C# using source generators and analyzers. Evaluate expressions at compile-time for zero runtime overhead.

## Installation

```bash
dotnet add package Corsinvest.Fx.CompileTime
```

## Features

- ✅ **Compile-time evaluation** - Compute values at build time
- ✅ **Zero runtime overhead** - Results are constants
- ✅ **Source generation** - Automatic code generation
- ✅ **Type-safe** - Full type checking at compile-time
- ✅ **Analyzer support** - Catch errors early

## Quick Start

```csharp
using Corsinvest.Fx.CompileTime;

// Mark functions for compile-time execution
[CompileTime]
public static int Factorial(int n)
{
    return n <= 1 ? 1 : n * Factorial(n - 1);
}

// Generated code:
// public static class CompileTimeConstants
// {
//     public const int Factorial_5 = 120;
//     public const int Factorial_10 = 3628800;
// }

// Use the computed constants
int result = CompileTimeConstants.Factorial_10; // No runtime computation!
```

## Usage Examples

### Compile-Time String Processing

```csharp
[CompileTime]
public static string EncodeBase64(string input)
{
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
}

// Generated:
// public const string EncodeBase64_Hello = "SGVsbG8=";
```

### Compile-Time Configuration

```csharp
[CompileTime]
public static int GetApiVersion() => 2;

[CompileTime]
public static string GetApiUrl() => GetApiVersion() switch
{
    1 => "https://api.v1.example.com",
    2 => "https://api.v2.example.com",
    _ => throw new NotSupportedException()
};

// Use in code
const string API_URL = CompileTimeConstants.GetApiUrl; // Evaluated at compile-time
```

### Compile-Time Validation

```csharp
[CompileTime]
public static bool IsValidEmail(string email)
{
    return Regex.IsMatch(email, @"^[^@]+@[^@]+\.[^@]+$");
}

// Analyzer ensures the string is valid at compile-time
[CompileTimeValidate(nameof(IsValidEmail))]
const string AdminEmail = "admin@example.com"; // ✅ Valid

[CompileTimeValidate(nameof(IsValidEmail))]
const string InvalidEmail = "not-an-email"; // ❌ Compile error!
```

## How It Works

1. **Source Generator**: Analyzes `[CompileTime]` attributed methods
2. **Evaluation**: Executes functions at compile-time with access to .NET BCL and same-class methods
3. **Code Generation**: Generates constants with computed values
4. **Analyzer**: Validates usage and catches errors

### Important: Access Scope
- ✅ **Accessible**: All .NET Base Class Library types (System.Security.Cryptography, System.Linq, etc.) and common namespaces (System, System.Collections.Generic, System.Threading.Tasks, etc.)
- ✅ **Accessible**: All methods and members within the same class as the `[CompileTime]` method
- ✅ **Accessible**: Additional namespaces based on project type (Microsoft.AspNetCore.*, System.Windows.*, etc.)
- ❌ **Not Accessible**: Methods or classes from other classes in the same project

## ⚠️ Limitations

CompileTime has specific requirements and limitations to ensure compile-time execution works correctly.

### ✅ Supported Features
- **Generic return types** - `Task<int>`, `List<string>`, `IEnumerable<T>` work perfectly
- **Async methods** - `async Task<T>` methods are fully supported
- **Recursion** - Methods can call themselves
- **LINQ and collections** - Full .NET BCL support
- **Multiple parameters** - Any compile-time constant parameters
- **.NET BCL classes** - All .NET Base Class Library types are accessible (e.g., SHA256, Encoding, etc.)

### ❌ Not Supported
- **Generic methods** - Methods with type parameters like `Method<T>()` are not supported
- **Instance methods** - Only `static` methods can use `[CompileTime]`
- **Non-class types** - Records, structs, and interfaces are not supported for `[CompileTime]` methods
- **Cross-class dependencies** - Only code within the same class is available to the Generator (other classes in the same project may not be accessible)
- **File I/O, network, database** - Pure functions only (no side effects)

### 📝 Examples

#### ✅ Supported: Generic Return Types
```csharp
[CompileTime]
public static Task<int> CalculateAsync() => Task.FromResult(42);

[CompileTime]
public static List<string> GetNames() => new() { "Alice", "Bob" };
```

#### ❌ Not Supported: Generic Methods
```csharp
// ❌ ERROR: Generic methods not supported
[CompileTime]
public static T Identity<T>(T value) => value;

// ✅ OK: Non-generic method with generic return type
[CompileTime]
public static List<int> GetNumbers() => new() { 1, 2, 3 };
```

#### ❌ Not Supported: Cross-Class Dependencies
```csharp
public static class Config
{
    public const int Value = 10;
}

// ❌ WARNING: Config.Value not available in Generator (different class)
[CompileTime]
public static int GetConfigValue() => Config.Value;  // Will fail

// ✅ OK: Self-contained method in the same class
public static class Calculator
{
    private const int ConfigValue = 10;

    [CompileTime]
    public static int GetConfigValue() => ConfigValue;  // ✅ OK
}
```

### 🔍 Validation
CompileTime validates these requirements at compile-time and provides clear error messages:
- `COMPTIME001` - Method must be static
- `COMPTIME002` - Method containing type not found
- `COMPTIME003` - Generic or complex namespace detected (warning)
- `COMPTIME004` - Generic methods are not supported

## API Reference

### `[CompileTime]` Attribute

The `[CompileTime]` attribute marks methods for compile-time execution. Methods are executed during build and results are embedded as constants.

#### Basic Usage

```csharp
[CompileTime]
public static int Add(int a, int b) => a + b;

// Call in code - will be replaced with constant
var result = Add(2, 3);  // Becomes: var result = 5;
```

#### Attribute Parameters

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class CompileTimeAttribute : Attribute
{
    // Cache strategy for method results
    public CacheStrategy Cache { get; set; } = CacheStrategy.Persistent;

    // Performance threshold in milliseconds
    public int PerformanceThresholdMs { get; set; } = 1000;

    // Suppress performance warnings
    public bool SuppressPerformanceWarnings { get; set; } = false;
}
```

---

### Cache Strategies

Control how method results are cached between builds:

```csharp
public enum CacheStrategy
{
    // Never cache - always re-execute (useful for time-dependent values)
    Never = 0,

    // Cache only during current build session (default)
    Session = 1,

    // Cache persistently across builds (fastest)
    Persistent = 2
}
```

#### Examples

**Never cache (always fresh):**
```csharp
[CompileTime(Cache = CacheStrategy.Never)]
public static string GetBuildTimestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
```

**Session cache (re-execute on new build):**
```csharp
[CompileTime(Cache = CacheStrategy.Session)]
public static int GenerateRandomSeed() => new Random().Next();
```

**Persistent cache (cache across builds):**
```csharp
[CompileTime(Cache = CacheStrategy.Persistent)]  // Default
public static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);
```

---

### Performance Monitoring

Set performance thresholds and suppress warnings:

```csharp
// Warn if execution exceeds 500ms
[CompileTime(PerformanceThresholdMs = 500)]
public static byte[] GenerateHash(string input)
{
    using var sha = SHA256.Create();
    return sha.ComputeHash(Encoding.UTF8.GetBytes(input));
}

// Suppress performance warnings for known slow methods
[CompileTime(
    PerformanceThresholdMs = 5000,
    SuppressPerformanceWarnings = true)]
public static BigInteger CalculateLargePrime(int digits)
{
    // Expensive computation...
}
```

---

### Method Requirements

Methods marked with `[CompileTime]` must follow these rules:

✅ **Required:**
- Must be `static`
- Must be in a class (not a record, struct, or interface)
- Parameters must be compile-time constants

✅ **Allowed:**
- Any return type (primitives, strings, classes, structs)
- Recursion
- Calling any methods within the same class (not just `[CompileTime]` methods)
- LINQ, collections, all .NET Base Class Library (BCL) types
- Access to all members within the same class

❌ **Not Allowed:**
- File I/O (`File`, `Directory`)
- Network operations (`HttpClient`, `Socket`)
- Database access
- External dependencies from other classes in the same project
- Environment-dependent code (may work but not portable)

---

### Complete Example

```csharp
using Corsinvest.Fx.CompileTime;
using System.Security.Cryptography;
using System.Text;

public static class Crypto
{
    // Fast method - persistent cache
    [CompileTime(Cache = CacheStrategy.Persistent)]
    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    // Helper method in the same class - accessible to [CompileTime] methods
    private static byte[] GenerateRandomBytes(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    // Slow method - higher threshold, suppress warnings
    [CompileTime(
        Cache = CacheStrategy.Persistent,
        PerformanceThresholdMs = 3000,
        SuppressPerformanceWarnings = true)]
    public static string GenerateSalt(int iterations)
    {
        var salt = GenerateRandomBytes(32); // ✅ Accessing helper method in same class
        var processedSalt = ProcessSalt(salt); // ✅ Also accessing other methods in same class

        // Expensive key derivation
        using var pbkdf2 = new Rfc2898DeriveBytes(processedSalt, processedSalt, iterations);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    // Regular helper method in the same class - accessible to [CompileTime] methods
    private static byte[] ProcessSalt(byte[] salt)
    {
        // Some processing logic
        var processed = new byte[salt.Length];
        for (int i = 0; i < salt.Length; i++)
        {
            processed[i] = (byte)(salt[i] ^ 0x55); // Example transformation
        }
        return processed;
    }

    // Never cache - always fresh
    [CompileTime(Cache = CacheStrategy.Never)]
    public static string GetBuildId() => Guid.NewGuid().ToString("N");
}

// Usage in code
public class AppConfig
{
    // These are computed at compile-time!
    public const string AdminPasswordHash = Crypto.HashPassword("admin123");
    public const string AppSalt = Crypto.GenerateSalt(10000);
    public const string BuildId = Crypto.GetBuildId();
}
```

---

### Generated Code

CompileTime generates interceptor classes that replace method calls with constants:

```csharp
// Generated in: obj/Debug/net9.0/CompileTime/Crypto_Interceptors.g.cs
namespace CompileTime;

using System.Runtime.CompilerServices;

file static class Crypto_Interceptors
{
    [InterceptsLocation("Program.cs", 10, 5)]
    public static string HashPassword(string password)
    {
        // Pre-computed result embedded as constant
        return "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";
    }
}
```

## Best Practices

1. **Keep functions pure** - No side effects, deterministic output
2. **Use for expensive computations** - Hash generation, encoding, etc.
3. **Validate inputs** - Use `[CompileTimeValidate]` for compile-time checks
4. **Document generated constants** - Add XML docs to `[CompileTime]` methods

## Performance Benefits

```csharp
// Traditional approach (runtime)
int factorial = Factorial(10); // Computed every time

// CompileTime approach (compile-time)
int factorial = CompileTimeConstants.Factorial_10; // Pre-computed constant
```

**Benchmark Results:**
- Runtime: ~150ns
- CompileTime: ~0ns (constant)
- **Speedup: ∞** (no runtime cost!)

## Comparison with Alternatives

| Approach | Runtime Cost | Type Safety | Flexibility |
|----------|--------------|-------------|-------------|
| `const` | None | ✅ Yes | ❌ Limited |
| `static readonly` | Low | ✅ Yes | ⚠️ Medium |
| `[CompileTime]` | **None** | ✅ Yes | ✅ High |
| Runtime computation | High | ✅ Yes | ✅ High |

## Configuration

CompileTime can be configured via MSBuild properties in your `.csproj` file:

### Available Properties

```xml
<PropertyGroup>
  <!-- Enable/disable CompileTime execution (default: true) -->
  <CompileTimeEnabled>true</CompileTimeEnabled>

  <!-- Timeout in milliseconds for each method execution (default: 5000) -->
  <CompileTimeTimeout>5000</CompileTimeTimeout>

  <!-- Timeout behavior: Skip, Warning, or Error (default: Skip) -->
  <CompileTimeBehaviorOnTimeout>Skip</CompileTimeBehaviorOnTimeout>

  <!-- Generate performance report (CompileTimeReport.md) (default: false) -->
  <CompileTimeReport>false</CompileTimeReport>

  <!-- Debug mode: None, Files, or Verbose (default: None) -->
  <CompileTimeDebugMode>None</CompileTimeDebugMode>
</PropertyGroup>
```

### Configuration Details

#### CompileTimeEnabled
**Type:** `bool`
**Default:** `true`
**Description:** Enable or disable CompileTime execution globally.

**Example:**
```xml
<!-- Disable in Debug builds for faster compilation -->
<CompileTimeEnabled Condition="'$(Configuration)' == 'Debug'">false</CompileTimeEnabled>
```

---

#### CompileTimeTimeout
**Type:** `int` (milliseconds)
**Default:** `5000`
**Description:** Maximum time allowed for each method execution.

**Example:**
```xml
<!-- Increase timeout for complex calculations -->
<CompileTimeTimeout>10000</CompileTimeTimeout>
```

---

#### CompileTimeBehaviorOnTimeout
**Type:** `string` (`Skip`, `Warning`, or `Error`)
**Default:** `Skip`
**Description:** What to do when a method execution exceeds the timeout.

**Values:**
- `Skip` - Skip execution, use default value (no build error)
- `Warning` - Show warning, use default value
- `Error` - Fail the build with diagnostic `COMPTIME103`

**Example:**
```xml
<!-- Fail build on timeout (strict mode) -->
<CompileTimeBehaviorOnTimeout>Error</CompileTimeBehaviorOnTimeout>

<!-- Just warn (relaxed mode) -->
<CompileTimeBehaviorOnTimeout>Warning</CompileTimeBehaviorOnTimeout>
```

---

#### CompileTimeReport
**Type:** `bool`
**Default:** `false`
**Description:** Generate a Markdown performance report (`CompileTimeReport.md`) with execution statistics.

**Example:**
```xml
<!-- Enable report in CI builds -->
<CompileTimeReport Condition="'$(CI)' == 'true'">true</CompileTimeReport>
```

**Report Contents:**
- Execution times for each method
- Cache hit/miss statistics
- Performance warnings
- Memory usage

---

#### CompileTimeDebugMode
**Type:** `string` (`None`, `Files`, or `Verbose`)
**Default:** `None`
**Description:** Debug mode for troubleshooting CompileTime issues.

**Values:**
- `None` - Normal operation (optimal performance)
- `Files` - Write debug JSON files (`debug_request.json`, `debug_response.json`)
- `Verbose` - Files + detailed MSBuild logging

**Example:**
```xml
<!-- Debug mode for troubleshooting -->
<CompileTimeDebugMode>Files</CompileTimeDebugMode>

<!-- Verbose logging for detailed investigation -->
<CompileTimeDebugMode>Verbose</CompileTimeDebugMode>
```

**Debug Files Location:**
- `obj/CompileTime/debug_request.json` - Request sent to Generator
- `obj/CompileTime/debug_response.json` - Response from Generator
- `obj/CompileTimeDiagnostics.json` - Diagnostics for Analyzer

---

### Configuration Examples

#### Development Setup (Fast Builds)
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <CompileTimeEnabled>false</CompileTimeEnabled>
</PropertyGroup>
```

#### CI/CD Setup (Performance Tracking)
```xml
<PropertyGroup Condition="'$(CI)' == 'true'">
  <CompileTimeReport>true</CompileTimeReport>
  <CompileTimeBehaviorOnTimeout>Error</CompileTimeBehaviorOnTimeout>
</PropertyGroup>
```

#### Troubleshooting Setup
```xml
<PropertyGroup>
  <CompileTimeDebugMode>Verbose</CompileTimeDebugMode>
  <CompileTimeReport>true</CompileTimeReport>
</PropertyGroup>
```

---

## 📚 Documentation

Detailed guides for advanced features:

- [🪄 How It Works](docs/HowItWorks.md) - **The magic behind CompileTime** - Complete architecture and execution flow
- [📖 Cache System](docs/Cache.md) - Complete caching documentation (strategies, invalidation, troubleshooting)
- [🎛️ CompileTimeAttribute](docs/CompileTimeAttribute.md) - Attribute properties and configuration
- [⚙️ MSBuild Properties](docs/MSBuildProperties.md) - Build configuration options
- [🔍 Diagnostic IDs](docs/DiagnosticIds.md) - Error codes and warnings
- [📊 Performance Report Example](docs/PerformanceReport-Example.md) - Sample performance report

---

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
