# Corsinvest.Fx

**Modern Patterns and High-Level Features for C# - Bringing the best from Go, Rust, F#, and Swift**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

Corsinvest.Fx is a collection of independent NuGet packages that bring modern programming patterns and high-level features to C#. Each package is designed to be used standalone or combined with others for maximum flexibility.

---

## 🎯 Philosophy

**Corsinvest.Fx is NOT just another FP library.**

It's a pragmatic suite of **modern patterns and high-level features** that C# lacks natively, inspired by languages like Go, Rust, F#, and Swift.

### Our Principles

| Principle | What It Means |
|-----------|---------------|
| **Pragmatism** | Solve real problems, not academic exercises |
| **Safety** | Catch errors at compile-time, not runtime |
| **Readability** | Clear, elegant code without excessive complexity |
| **Modernità** | Best practices from modern languages |

### What We Include

```txt
✅ Functional Patterns (when useful)
   └─ ResultOf<T,E>, Option<T>, Union Types
   └─ Railway-oriented programming
   └─ Data transformation pipelines

✅ Modern Language Features
   └─ defer (Go-style resource cleanup)
   └─ Inline assembly (Rust/Zig-style performance - experimental)
```

### Our Motto

> **"Solve real problems elegantly, don't chase theoretical perfection"**

---

## 🌟 Why Choose Corsinvest.Fx?

### ✅ Use Corsinvest.Fx When You Want

- **Type-safe error handling** without exceptions (`ResultOf<T,E>`, `Option<T>`)
- **Discriminated unions** with pattern matching (`[Union]` attribute)
- **Go-style resource cleanup** (`defer`)
- **Data transformation pipelines** (`Pipe` extensions)
- **Gradual adoption** in existing C# codebases
- **Minimal learning curve** for your team

### ❌ Don't Use Corsinvest.Fx If

| Scenario | Why Not | Use Instead |
|----------|---------|-------------|
| **Pure FP needed** | Need HKT, Free monads, lenses, etc. | LanguageExt, F# |
| **Simple scripts** | One-off < 100 lines, no maintenance | Plain C# |
| **Team not ready** | Unfamiliar with Result/Option, unwilling to learn | Traditional C# patterns |
| **Batteries-included FP** | Need complete FP ecosystem (HTTP, DB, etc.) | LanguageExt + ecosystem |

---

## 📦 Packages

### Core Packages

| Package | Description | Status |
|---------|-------------|--------|
| **[Corsinvest.Fx.Functional](src/Corsinvest.Fx.Functional/)** | `ResultOf<T,E>`, `Option<T>`, `[Union]` attribute. Railway-oriented programming, pattern matching, LINQ support. | ✅ Stable |
| **[Corsinvest.Fx.Defer](src/Corsinvest.Fx.Defer/)** | Go-style defer statements for automatic cleanup on scope exit. | ✅ Stable |

### Experimental Packages

| Package | Description | Status |
|---------|-------------|--------|
| **[Corsinvest.Fx.Comptime](src/Corsinvest.Fx.Comptime/)** | Zig-style compile-time computation using source generators. | 🧪 Experimental |
| **[Corsinvest.Fx.Unsafe](src/Corsinvest.Fx.Unsafe/)** | Inline assembly wrappers and unsafe operations for performance-critical code. | 🧪 Experimental |

---

## 🚀 Quick Start

### Installation

Install individual packages via NuGet:

```bash
# Functional programming (Result, Option, Union)
dotnet add package Corsinvest.Fx.Functional

# Go-style defer
dotnet add package Corsinvest.Fx.Defer
```

### Quick Examples

**1. Type-Safe Error Handling (Functional)**
```csharp
using Corsinvest.Fx.Functional;

var result = ValidateEmail(email)
    .Bind(SaveToDatabase);

result.Match(
    ok => Console.WriteLine("Success!"),
    error => Console.WriteLine($"Error: {error}")
);
```

**2. Automatic Cleanup (Defer)**
```csharp
using static Corsinvest.Fx.Defer.Defer;

var file = File.Open(path, FileMode.Open);
using var _ = defer(() => file.Close());
// File closes automatically
```

**Experimental packages**: Unsafe (inline assembly), Comptime (compile-time computation)

📖 **See individual package READMEs for complete documentation**:
- [Functional](src/Corsinvest.Fx.Functional/README.md) - ResultOf, Option, Union types
- [Defer](src/Corsinvest.Fx.Defer/README.md) - Resource cleanup

---

## 📚 Explore Real-World Examples

The [`examples/`](examples/) folder contains practical, runnable code demonstrating all features:

### Core Examples

- **[01_OptionBasics.cs](examples/01_OptionBasics.cs)** - Parsing, config, null handling
- **[02_ResultOfValidation.cs](examples/02_ResultOfValidation.cs)** - Multi-step validation
- **[03_ResultOfRailway.cs](examples/03_ResultOfRailway.cs)** - Order processing pipeline
- **[04_UnionTypes.cs](examples/04_UnionTypes.cs)** - Payment methods, API states, shapes
- **[05_PipeWorkflow.cs](examples/05_PipeWorkflow.cs)** - Data transformation pipelines
- **[06_CombinedPatterns.cs](examples/06_CombinedPatterns.cs)** - User registration flow (Option + ResultOf + Pipe)

### Advanced Examples

- **[07_OptionChaining.cs](examples/07_OptionChaining.cs)** - OrElse cascading, Flatten, lazy evaluation
- **[08_ResultOfRecover.cs](examples/08_ResultOfRecover.cs)** - Recovery strategies, retry logic
- **[09_DeferAsync.cs](examples/09_DeferAsync.cs)** - Async resource cleanup

Run all examples:

```bash
dotnet run --project examples/Corsinvest.Fx.Examples.csproj
```

---

## 💡 Feature Highlights

### ResultOf - Type-Safe Error Handling

Railway-oriented programming without exceptions:

```csharp
var result = ValidateEmail(email)
    .Bind(e => ValidateName(name))
    .Bind(n => ValidateAge(age))
    .Map(data => new User(data.Email, data.Name, data.Age))
    .Bind(user => SaveToDatabase(user));

result.Match(
    ok => Console.WriteLine($"Success: {ok.Value.Id}"),
    error => Console.WriteLine($"Error: {error.ErrorValue}")
);
```

### Union Types - Discriminated Unions

Pattern matching for different states:

```csharp
[Union]
public partial record PaymentMethod
{
    public partial record CreditCard(string Number, string ExpiryDate);
    public partial record PayPal(string Email);
    public partial record BankTransfer(string Iban, string Bic);
}

decimal CalculateFee(PaymentMethod payment) => payment.Match(
    creditCard => 2.5m,
    payPal => 1.5m,
    bankTransfer => 0.0m
);
```

### Option - Null Safety

Eliminate null reference exceptions:

```csharp
Option<User> FindUser(int id) =>
    users.ContainsKey(id)
        ? Option.Some(users[id])
        : Option.None<User>();

var userName = FindUser(42)
    .Map(u => u.Name)
    .GetValueOr("Guest");
```

### Defer - Automatic Cleanup

Go-style resource management:

```csharp
using static Corsinvest.Fx.Defer.Defer;

void ProcessFile(string path)
{
    var file = File.Open(path, FileMode.Open);
    using var _ = defer(() => file.Close());

    // File automatically closed on scope exit (even on exception)
    ProcessData(file);
}
```

---

## 🔧 Troubleshooting

For common issues and solutions, please refer to the **Troubleshooting** section in the README of the specific package you are using:

- [Functional Package Troubleshooting](src/Corsinvest.Fx.Functional/README.md#-troubleshooting)
- [Defer Package Troubleshooting](src/Corsinvest.Fx.Defer/README.md#-troubleshooting)

If you still need help:

1. Check the [examples/](examples/) folder for similar use cases.
2. Search [existing issues](https://github.com/Corsinvest/dotnet-fx/issues).
3. Open a [new issue](https://github.com/Corsinvest/dotnet-fx/issues/new) with a minimal reproducible code sample.

---

## 🧪 Building and Testing

```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run all tests
dotnet test

# Run tests with coverage
pwsh tests/RunTestsAndCoverage.ps1
```

**Quality Metrics:**

- ✅ **Comprehensive test suite** with high coverage
- ✅ **Clean build** without warnings or errors
- ✅ **Multiple real-world examples**

---

## 📖 Documentation

Each package has its own detailed README:

- [Functional - Result, Option, Union](src/Corsinvest.Fx.Functional/README.md)
- [Defer - Go-Style Defer](src/Corsinvest.Fx.Defer/README.md)
- [Unsafe - Inline Assembly](src/Corsinvest.Fx.Unsafe/README.md) *(experimental)*
- [Comptime - Compile-Time Computation](src/Corsinvest.Fx.Comptime/README.md) *(experimental)*

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🔗 Links

- **NuGet**: [Corsinvest.Fx packages](https://www.nuget.org/profiles/Corsinvest)
- **GitHub**: [https://github.com/Corsinvest/dotnet-fx](https://github.com/Corsinvest/dotnet-fx)
- **Issues**: [Report bugs or request features](https://github.com/Corsinvest/dotnet-fx/issues)
- **Project Docs**: [PROJECT.md](PROJECT.md) - Philosophy, roadmap, decisions

---

## 🙏 Acknowledgments

Inspired by functional programming languages and modern systems languages:

- **F#** - Discriminated unions and Result types
- **Rust** - Result/Option types and pattern matching
- **Go** - Defer statement for resource cleanup
- **Zig** - Compile-time execution philosophy
- **Swift** - Union types and modern syntax

---

Made with ❤️ by [Corsinvest](https://www.corsinvest.it)
