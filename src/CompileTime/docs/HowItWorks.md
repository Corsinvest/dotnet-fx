# 🪄 How CompileTime Works - The Magic Behind the Scenes

**Complete guide to CompileTime's internal architecture and execution flow**

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [The 8-Step Journey](#the-8-step-journey)
3. [Architecture Components](#architecture-components)
4. [Detailed Flow](#detailed-flow)
5. [Code Generation](#code-generation)
6. [Cache System](#cache-system)
7. [Example Walkthrough](#example-walkthrough)
8. [Troubleshooting](#troubleshooting)

---

## Overview

CompileTime uses a sophisticated pipeline combining **Roslyn Source Generators**, **MSBuild Tasks**, and **Runtime Compilation** to execute C# code at compile-time and inject the results as constants.

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Your C# Source Code                          │
│  [CompileTime]                                                  │
│  public static int Calculate() => 42;                           │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│              MSBuild CompileTimeTask                            │
│  • Scans for [CompileTime] methods                              │
│  • Extracts method code                                         │
│  • Manages cache                                                │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│           CompileTime Generator Process                         │
│  • Compiles method in isolated context                          │
│  • Executes with provided parameters                            │
│  • Returns serialized result                                    │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│              Interceptor Generation                             │
│  [InterceptsLocation(...)]                                      │
│  public static int Calculate() => 42; // Constant!              │
└─────────────────────────────────────────────────────────────────┘
```

---

## The 8-Step Journey

### Step 1: **Compilation Starts** 🚀

When you run `dotnet build`, MSBuild executes the **CompileTimeTask** before the main C# compilation.

**File:** `CompileTimeTask.cs:Execute()`

```csharp
public override bool Execute()
{
    // Called by MSBuild during build
    Log.LogMessage("⚡ CompileTime: Starting compile-time execution...");

    // Parse compilation for [CompileTime] methods
    var compilation = CreateCompilation();
    // ...
}
```

---

### Step 2: **Method Discovery** 🔍

The task scans your source code using **Roslyn** to find all methods marked with `[CompileTime]`.

**File:** `CompileTimeTask.cs:194-210`

```csharp
// Find all invocations of [CompileTime] methods
var invocations = compilation.SyntaxTrees
    .SelectMany(tree => tree.GetRoot().DescendantNodes()
        .OfType<InvocationExpressionSyntax>())
    .Select(invocation => new {
        Invocation = invocation,
        Symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol
    })
    .Where(x => x.Symbol != null && HasCompileTimeAttribute(x.Symbol))
    .ToList();
```

**What happens:**
- Roslyn parses your entire codebase
- Finds all method calls
- Checks if the called method has `[CompileTime]` attribute
- Extracts call location (file, line, column) for later interception

---

### Step 3: **Validation** ✅

Each discovered method is validated against CompileTime requirements.

**File:** `CompileTimeTask.cs:266-289`

```csharp
// Validate: Must be in a class
if (classDeclarationSyntax == null)
{
    Log.LogWarning($"Method {methodSymbol.Name} must be in a class.");
    continue;
}

// Validate: Must not be generic
if (methodSymbol.IsGenericMethod)
{
    AddDiagnostic(DiagnosticDescriptors.GenericMethodNotSupported, ...);
    continue;
}
```

**Validations:**
- ✅ Method is `static` (COMPTIME001)
- ✅ Method is in a `class` (not record/struct/interface)
- ✅ Method is not generic `<T>` (COMPTIME004)
- ✅ Namespace is not too complex (COMPTIME003 warning)

---

### Step 4: **Code Extraction** 📦

CompileTime extracts the **entire class** containing the `[CompileTime]` method. This is critical!

**File:** `CompileTimeTask.cs:349-361`

```csharp
uniqueRequests[methodKey] = new()
{
    ClassCode = classDeclarationSyntax.ToFullString(), // ← ENTIRE class!
    Namespace = methodSymbol.ContainingType.ContainingNamespace.ToDisplayString(),
    ClassName = methodSymbol.ContainingType.Name,
    MethodName = methodSymbol.Name,
    Parameters = invocation.Parameters,
    // ...
};
```

**Why the entire class?**
- The method might call other methods in the same class
- Helper methods, constants, nested classes are all included
- **External classes are NOT available** - this is a key limitation!

**Example:**
```csharp
public static class Calculator
{
    // ✅ This entire class is sent to the Generator
    private const int BASE = 10;

    private static int Helper(int x) => x * BASE;

    [CompileTime]
    public static int Calculate(int n) => Helper(n) + 5; // Can call Helper!
}

// ❌ This class is NOT available in Generator
public static class ExternalUtils
{
    public const int Value = 42;
}
```

---

### Step 5: **Generator Process Launch** 🚀

CompileTime launches a **separate .NET process** (`CompileTime.Generator.dll`) to execute methods in isolation.

**File:** `CompileTimeTask.cs:383-433`

```csharp
var generatorPath = Path.Combine(TaskDirectory, "CompileTime.Generator.dll");

var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"\"{generatorPath}\"",
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }
};

process.Start();

// Send request as JSON via stdin
var requestJson = JsonSerializer.Serialize(request);
await process.StandardInput.WriteLineAsync(requestJson);

// Read response from stdout
var responseJson = await process.StandardOutput.ReadToEndAsync();
```

**Why a separate process?**
- **Isolation**: Execution doesn't affect the build process
- **Timeout control**: Can kill runaway executions
- **Memory safety**: Process memory is reclaimed after execution
- **Security**: Sandboxed environment

---

### Step 6: **Runtime Compilation & Execution** ⚡

The Generator process compiles and executes the method using **Roslyn's Compilation API**.

**File:** `ExecutionEngine.cs:130-192`

```csharp
private static Assembly CompileMethodToAssembly(
    CompileTimeRequest request,
    CompileTimeRequest.Item item,
    IEnumerable<MetadataReference> references,
    AssemblyLoadContext loadContext)
{
    // Extract usings from the class code
    var syntaxTree = CSharpSyntaxTree.ParseText(item.ClassCode);
    var usings = syntaxTree.GetRoot()
        .DescendantNodes()
        .OfType<UsingDirectiveSyntax>()
        .Select(u => u.Name?.ToString())
        .ToList();

    // Add standard usings
    usings.AddRange([
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Threading.Tasks",
        // ...
    ]);

    // Wrap class in namespace if needed
    var codeWithNamespace = string.IsNullOrEmpty(item.Namespace)
        ? item.ClassCode
        : $"namespace {item.Namespace} {{ {item.ClassCode} }}";

    var wrapperCode = $"{string.Join("\n", usings.Select(u => $"using {u};"))}\n\n{codeWithNamespace}";

    // Compile to in-memory assembly
    var compilation = CSharpCompilation.Create($"CompileTime_{Guid.NewGuid()}")
        .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
        .AddReferences(references)
        .AddSyntaxTrees(CSharpSyntaxTree.ParseText(wrapperCode));

    using var ms = new MemoryStream();
    var emitResult = compilation.Emit(ms);

    if (!emitResult.Success)
    {
        var errors = string.Join("; ", emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));
        throw new InvalidOperationException($"Compilation failed: {errors}");
    }

    ms.Seek(0, SeekOrigin.Begin);
    return loadContext.LoadFromStream(ms); // Load assembly into memory
}
```

**Then execute:**

```csharp
var type = assembly.GetType($"{namespace}.{className}");
var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

// Execute with timeout
var result = await ExecuteMethodAsync(methodInfo, parameters, timeoutMs);
```

**What happens:**
1. Class code is wrapped with `using` statements
2. Code is compiled to an in-memory assembly
3. Method is located via reflection
4. Method is invoked with provided parameters
5. Result is captured and serialized

---

### Step 7: **Result Serialization** 📝

The execution result is serialized into C# literal syntax.

**File:** `ExecutionEngine.cs:229-278`

```csharp
private string FormatValueAsLiteral(object value, string returnTypeName)
    => value switch
    {
        string str => FormatAsRawStringLiteral(str),      // "Hello" → """Hello"""
        int i => i.ToString(CultureInfo.InvariantCulture), // 42 → 42
        long l => $"{l}L",                                 // 42 → 42L
        float f => $"{f:R}f",                              // 3.14 → 3.14f
        double d => d.ToString("R"),                       // 3.14 → 3.14
        decimal m => $"{m}m",                              // 3.14 → 3.14m
        bool b => b ? "true" : "false",                    // true → true
        DateTime dt => $"new DateTime({dt.Year}, {dt.Month}, ...)",
        Array arr => FormatArrayLiteral(arr, returnTypeName),
        _ => value.ToString() ?? GetDefaultValue(returnTypeName)
    };
```

**Examples:**
- `42` → `42`
- `"Hello"` → `"""Hello"""`
- `new[] {1,2,3}` → `new int[] { 1, 2, 3 }`
- `DateTime.Now` → `new DateTime(2025, 12, 1, 10, 30, 0)`

---

### Step 8: **Interceptor Generation** 🎯

The final step: generate interceptor code that replaces method calls with constants.

**File:** `CompileTimeTask.cs:537-620`

```csharp
private void GenerateInterceptors(...)
{
    foreach (var methodKey in responseMap.Keys)
    {
        var response = responseMap[methodKey];
        var calls = callsForRequest[methodKey];

        // Generate interceptor method
        sb.AppendLine($"    file static class {interceptorClassName}");
        sb.AppendLine("    {");

        // Add [InterceptsLocation] for EACH call site
        foreach (var call in calls)
        {
            sb.AppendLine($"        [InterceptsLocation(@\"{call.FilePath}\", {call.Line}, {call.Column})]");
        }

        // Generate method that returns the constant
        sb.AppendLine($"        public static {returnType} {methodName}({parameters})");
        sb.AppendLine("        {");
        sb.AppendLine($"            return {response.SerializedValue};"); // ← The magic!
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }
}
```

**Generated code example:**

**Your code:**
```csharp
var a = Calculate(5);
var b = Calculate(5);
var c = Calculate(10);
```

**Generated interceptor:**
```csharp
file static class Calculator_Interceptors
{
    [InterceptsLocation(@"Program.cs", 10, 13)]  // var a = Calculate(5);
    [InterceptsLocation(@"Program.cs", 11, 13)]  // var b = Calculate(5);
    public static int Calculate(int n)
    {
        return 120; // Pre-computed result for Calculate(5)
    }

    [InterceptsLocation(@"Program.cs", 12, 13)]  // var c = Calculate(10);
    public static int Calculate(int n)
    {
        return 3628800; // Pre-computed result for Calculate(10)
    }
}
```

**How Interceptors Work:**
- C# 12+ feature that redirects method calls to different implementations
- `[InterceptsLocation(file, line, column)]` specifies exact call site
- At compile-time, the C# compiler **replaces** your `Calculate(5)` call with the interceptor
- **Zero runtime overhead** - it's as if you wrote `120` directly!

---

## Architecture Components

### 1. **Corsinvest.Fx.CompileTime** (User-facing package)

**Contains:**
- `[CompileTime]` attribute
- `CacheStrategy` enum
- User documentation

**Purpose:** API surface for developers

---

### 2. **Corsinvest.Fx.CompileTime.Tasks** (MSBuild Task)

**Contains:**
- `CompileTimeTask` - Main orchestration
- `CacheManager` - Persistent cache
- Method discovery & validation
- Interceptor generation

**Purpose:** Compile-time coordinator

---

### 3. **Corsinvest.Fx.CompileTime.Generator** (Execution Engine)

**Contains:**
- `ExecutionEngine` - Method compilation & execution
- Result serialization
- Timeout handling
- Memory safety

**Purpose:** Isolated execution environment

---

### 4. **Corsinvest.Fx.CompileTime.Analyzers** (Roslyn Analyzer)

**Contains:**
- `CompileTimeDiagnosticAnalyzer` - IDE integration
- Performance warnings
- Error reporting

**Purpose:** Developer feedback in IDE

---

### 5. **Corsinvest.Fx.CompileTime.Shared** (Common types)

**Contains:**
- `CompileTimeRequest` / `CompileTimeResponse`
- `DiagnosticDescriptors`
- Helper utilities

**Purpose:** Shared models between components

---

## Detailed Flow

### Request Flow

```
User Code
    ↓
CompileTimeTask.Execute()
    ↓
Roslyn: Parse & Analyze
    ↓
Extract Methods & Invocations
    ↓
Check Cache (CacheManager)
    ├─ HIT → Use cached result ✅
    └─ MISS → Continue ⏬
         ↓
Create CompileTimeRequest (JSON)
    ↓
Launch Generator Process
    ↓
Send Request via STDIN
    ↓
Generator: Compile & Execute
    ↓
Receive Response via STDOUT
    ↓
Update Cache
    ↓
Generate Interceptors
    ↓
Write to .g.cs files
    ↓
C# Compiler: Apply Interceptors
    ↓
Final Assembly 🎉
```

---

### Cache Flow

```csharp
// Cache key generation
var methodKey = "MyNamespace.Calculator.Factorial(5)";
var cacheKey = GenerateShortHash(methodKey); // "A1B2C3D4"

// Cache lookup
if (_cacheManager.Entries.TryGetValue(cacheKey, out var entry))
{
    // Validate cache entry
    if (entry.MethodContentHash == currentHash &&
        entry.ValueType == returnType)
    {
        // ✅ Cache HIT - use cached result
        return entry.SerializedValue;
    }
    else
    {
        // ⚠️ Cache INVALIDATED - method changed
    }
}

// ❌ Cache MISS - execute method
var result = await ExecuteMethod(...);

// Save to cache
_cacheManager.Set(cacheKey, new Entry
{
    MethodContentHash = currentHash,
    SerializedValue = result,
    ValueType = returnType,
    Persistent = cacheStrategy == CacheStrategy.Persistent
});
```

---

## Code Generation

### Output Structure

CompileTime generates files in `obj/Debug/netX.0/CompileTime/`:

```
obj/
└── Debug/
    └── net9.0/
        └── CompileTime/
            ├── Calculator_Interceptors.g.cs
            ├── StringHelper_Interceptors.g.cs
            ├── InterceptsLocationAttribute.g.cs
            └── ...
```

### Generated File Format

```csharp
// <auto-generated />
#nullable enable
#pragma warning disable CS1591 // Missing XML comment

using System.Runtime.CompilerServices;

namespace MyApp;

file static class Calculator_Interceptors_A1B2C3D4
{
    [InterceptsLocation(@"K:\Projects\MyApp\Program.cs", 10, 13)]
    public static int Factorial(int n)
    {
        return 120;
    }
}
```

**Key features:**
- `file static` - Scoped to this file only, no naming conflicts
- `[InterceptsLocation]` - Precise call-site interception
- Constant return value - Zero runtime computation!

---

## Cache System

### Cache File Structure

**Location:** `obj/CompileTimeCache.json`

```json
{
  "Version": "1.0",
  "CreatedAt": "2025-12-01T10:30:00Z",
  "Entries": {
    "A1B2C3D4": {
      "MethodContentHash": "abc123def456",
      "Success": true,
      "SerializedValue": "120",
      "ValueType": "System.Int32",
      "ErrorMessage": null,
      "ErrorCode": null,
      "CachedAt": "2025-12-01T10:30:00Z",
      "ExecutionTimeMs": 15,
      "MemoryFootprintBytes": 1024,
      "Persistent": true
    }
  }
}
```

### Cache Invalidation

Cache is invalidated when:

1. **Method body changes**
   ```csharp
   // Hash of method IL code changes
   MethodContentHash: "abc123" → "def456"
   ```

2. **Return type changes**
   ```csharp
   // Before: int Calc() => 42
   ValueType: "System.Int32"

   // After: string Calc() => "42"
   ValueType: "System.String" // ← Cache invalidated!
   ```

3. **Manual deletion**
   ```bash
   dotnet clean
   # or
   rm obj/CompileTimeCache.json
   ```

**See [Cache.md](Cache.md) for complete cache documentation.**

---

## Example Walkthrough

### Your Code

```csharp
using Corsinvest.Fx.CompileTime;

public static class Math
{
    [CompileTime]
    public static int Factorial(int n)
    {
        return n <= 1 ? 1 : n * Factorial(n - 1);
    }
}

public class Program
{
    public static void Main()
    {
        var result = Math.Factorial(5); // Line 12, Column 22
        Console.WriteLine(result);
    }
}
```

---

### Step-by-Step Execution

#### **Build Start**

```
$ dotnet build
MSBuild version 17.8.0+...
  Determining projects to restore...
  ⚡ CompileTime: Starting compile-time execution...
```

---

#### **Method Discovery**

```
[CompileTime Task] Found invocation: Math.Factorial(5)
  Location: Program.cs:12:22
  Method: Math.Factorial
  Parameters: [5]
```

---

#### **Cache Check**

```
[CompileTime Task] Cache key: A1B2C3D4
[CompileTime Task] Cache MISS - will execute
```

---

#### **Code Extraction**

Sends to Generator:
```json
{
  "ClassCode": "public static class Math\n{\n    [CompileTime]\n    public static int Factorial(int n)\n    {\n        return n <= 1 ? 1 : n * Factorial(n - 1);\n    }\n}",
  "Namespace": "MyApp",
  "ClassName": "Math",
  "MethodName": "Factorial",
  "Parameters": [5],
  "TimeoutMs": 5000
}
```

---

#### **Generator Execution**

```
[Generator] Compiling method Math.Factorial...
[Generator] Compilation successful
[Generator] Executing Factorial(5)...
[Generator] Result: 120 (System.Int32)
[Generator] Execution time: 15ms
```

Returns:
```json
{
  "Success": true,
  "SerializedValue": "120",
  "ExecutionTimeMs": 15,
  "InvocationId": "guid-1234"
}
```

---

#### **Cache Update**

```
[CompileTime Task] Caching result for A1B2C3D4
[CompileTime Task] Cache strategy: Persistent
[CompileTime Task] Saved to obj/CompileTimeCache.json
```

---

#### **Interceptor Generation**

Writes `obj/.../Math_Interceptors.g.cs`:

```csharp
file static class Math_Interceptors_A1B2C3D4
{
    [InterceptsLocation(@"K:\Projects\MyApp\Program.cs", 12, 22)]
    public static int Factorial(int n)
    {
        return 120;
    }
}
```

---

#### **C# Compilation**

```
[Compiler] Applying interceptors...
[Compiler] Program.cs:12:22 → Intercepted by Math_Interceptors_A1B2C3D4.Factorial
```

Your code **effectively becomes**:
```csharp
var result = 120; // Direct constant!
```

---

#### **Build Complete**

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.45
```

---

#### **Next Build (Cache Hit)**

```
$ dotnet build
  ⚡ CompileTime: Starting compile-time execution...
[CompileTime Task] Cache key: A1B2C3D4
[CompileTime Task] ✅ Cache HIT - using cached result
[CompileTime Task] Execution time: 0ms (cached)

Build succeeded.
Time Elapsed 00:00:01.12  ← Much faster!
```

---

## Troubleshooting

### Debug Mode

Enable verbose logging:

```xml
<PropertyGroup>
  <CompileTimeDebugMode>Verbose</CompileTimeDebugMode>
</PropertyGroup>
```

**Output files:**
- `obj/CompileTime/debug_request.json` - Request sent to Generator
- `obj/CompileTime/debug_response.json` - Response from Generator
- `obj/CompileTimeDiagnostics.json` - Diagnostics for Analyzer

---

### Common Issues

#### **"Method execution timed out"**

**Cause:** Method took longer than `CompileTimeTimeout` (default 5s)

**Solution:**
```xml
<CompileTimeTimeout>10000</CompileTimeTimeout>
```

---

#### **"Could not find type 'MyClass'"**

**Cause:** External class not available in Generator

**Solution:** Move the code into the same class:

```csharp
// ❌ WRONG
public static class Config { public const int Value = 10; }

[CompileTime]
public static int GetValue() => Config.Value; // Config not available!

// ✅ CORRECT
public static class Calculator
{
    private const int Value = 10; // In same class!

    [CompileTime]
    public static int GetValue() => Value;
}
```

---

#### **"Cache invalidated on every build"**

**Cause:** Method body or dependencies changing

**Check:**
```bash
# View cache file
cat obj/CompileTimeCache.json

# Compare MethodContentHash between builds
```

**Solution:** Use `CacheStrategy.Default` if external dependencies change frequently.

---

## Performance

### Benchmarks

| Scenario | First Build | Cached Build | Runtime |
|----------|-------------|--------------|---------|
| Factorial(10) | ~50ms | ~0ms | ~150ns |
| SHA256 Hash | ~120ms | ~0ms | ~2000ns |
| Complex Regex | ~300ms | ~0ms | ~5000ns |

**Speedup:** ∞ (cached builds have zero runtime cost!)

---

## Related Documentation

- [📖 Cache System](Cache.md) - Complete caching guide
- [🎛️ CompileTimeAttribute](CompileTimeAttribute.md) - Attribute reference
- [⚙️ MSBuild Properties](MSBuildProperties.md) - Configuration
- [🔍 Diagnostic IDs](DiagnosticIds.md) - Error codes
- [⚠️ Limitations](../README.md#limitations) - What's supported

---

## Summary

CompileTime achieves **zero runtime overhead** through:

1. 🔍 **Roslyn analysis** - Discover `[CompileTime]` methods at build-time
2. ⚡ **Isolated execution** - Compile & run in separate process
3. 💾 **Persistent caching** - Cache results across builds
4. 🎯 **C# 12 Interceptors** - Replace calls with constants at compile-time

The result: **Zig-style `comptime` for C#** with full type safety and IDE integration!

---

**Questions or issues?** See [GitHub Issues](https://github.com/Corsinvest/dotnet-fx/issues)
