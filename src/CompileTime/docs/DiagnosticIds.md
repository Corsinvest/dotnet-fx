# 🛠️ CompileTime Diagnostic ID Reference

Complete guide to CompileTime diagnostic messages, diagnostic IDs, and troubleshooting.

---

## 🔍 Diagnostic ID Categories

CompileTime uses consecutive diagnostic IDs for simplicity:

| Range               | Category                    | Description                              |
| ------------------- | --------------------------- | ---------------------------------------- |
| **COMPTIME001-004** | Core Diagnostics            | Method validation and basic requirements |
| **COMPTIME005-010** | Performance and Suggestions | Performance issues and optimization hints|
| **COMPTIME011-015** | Timeout Handling            | Timeout behavior and execution limits    |
| **COMPTIME016-020** | Execution Errors            | Runtime execution failures               |
| **COMPTIME021-025** | Generator Errors            | Source generation and compilation issues |
| **COMPTIME026-030** | Informational Messages      | Build reports and summaries              |

---

## 🚨 Core Diagnostics (COMPTIME001-099)

### Method Validation and Requirements

| Code          | Severity   | Description                             | Solution                                  |
| ------------- | ---------- | --------------------------------------- | ----------------------------------------- |
| `COMPTIME001` | ❌ Error   | Method must be static                   | Make the method `static`                  |
| `COMPTIME002` | ❌ Error   | Method containing type not found        | Ensure the containing class is accessible |
| `COMPTIME003` | ⚠️ Warning | Generic or complex namespace detected   | Review namespace complexity               |
| `COMPTIME004` | ❌ Error   | Generic methods are not supported       | Remove type parameters or [CompileTime]   |

#### Examples

```csharp
// ❌ COMPTIME001: Method must be static
[CompileTime]
public int NonStaticMethod() => 42; // ERROR: Remove [CompileTime] or make static

// ✅ Correct
[CompileTime]
public static int StaticMethod() => 42;
```

```csharp
// ⚠️ COMPTIME003: Complex namespace
namespace MyApp.Services.ComplexNamespace.VeryDeeplyNested
{
    public static class Calculator
    {
        [CompileTime]
        public static int Calculate() => 42; // May trigger warning
    }
}
```

```csharp
// ❌ COMPTIME004: Generic methods not supported
[CompileTime]
public static T Identity<T>(T value) => value; // ERROR: Remove <T> or [CompileTime]

// ✅ Correct: Generic return type (not generic method)
[CompileTime]
public static List<int> GetNumbers() => new() { 1, 2, 3 };
```

---

## ⏱️ Performance Diagnostics (COMPTIME100-199)

### Execution Time and Performance Issues

| Code          | Severity   | Description                             | Solution                                   |
| ------------- | ---------- | --------------------------------------- | ------------------------------------------ |
| `COMPTIME100` | 💡 Info    | Method could benefit from [CompileTime] | Consider adding `[CompileTime]` attribute |
| `COMPTIME101` | ⚠️ Warning | Slow method execution                   | Optimize method or increase threshold      |
| `COMPTIME102` | ⚠️ Warning | Method execution skipped due to timeout | Increase timeout or optimize method        |
| `COMPTIME103` | ❌ Error   | Method execution timeout error          | Fix infinite loops or increase timeout     |
| `COMPTIME104` | ⚠️ Warning | Method execution timeout warning        | Consider optimization                      |
| `COMPTIME105` | ⚠️ Warning | Unknown timeout behavior                | Check `CompileTimeTimeoutBehavior` setting |

#### Performance Examples

### Suggestion Analyzer (COMPTIME100)

The built-in suggestion analyzer helps you identify opportunities to use `[CompileTime]`. It runs in the background and provides suggestions for methods that appear to be good candidates for compile-time execution.

**Diagnostic:** `COMPTIME100` - Method could benefit from [CompileTime]

This informational diagnostic is triggered when a method meets the following criteria:
- It is `static`.
- It has no parameters.
- It does not return `void`.
- It has a simple body (a single `return` statement or an expression body).

#### Example

```csharp
// 💡 COMPTIME005: Suggestion will appear for this method
public static string GetApiVersion()
{
    return "v1.2.3";
}

// ✅ Applying the suggestion
[CompileTime]
public static string GetApiVersion()
{
    return "v1.2.3";
}
```

```csharp
// ⚠️ COMPTIME101: Slow method execution
[CompileTime]
public static int SlowMethod()
{
    Thread.Sleep(2000); // Takes 2 seconds, triggers warning if threshold < 2000ms
    return 42;
}

// ✅ Solutions:
[CompileTime(PerformanceWarningThresholdMs = 3000)] // Increase threshold
public static int SlowMethodWithCustomThreshold()
{
    Thread.Sleep(2000); // No warning with 3-second threshold
    return 42;
}

[CompileTime(SuppressWarnings = true)] // Suppress warnings
public static int SlowMethodSuppressed()
{
    Thread.Sleep(2000); // No warning
    return 42;
}
```

```csharp
// ❌ COMPTIME103: Timeout error
[CompileTime(TimeoutMs = 100)]
public static int InfiniteLoopMethod()
{
    while (true) { } // Will timeout and cause build error
    return 42;
}

// ✅ Solution:
[CompileTime(TimeoutMs = 5000)] // Increase timeout
public static int FixedMethod()
{
    // Ensure method completes within timeout
    return ExpensiveButFiniteCalculation();
}
```

---

## 💥 Execution Diagnostics (COMPTIME301-399)

### Runtime Execution Failures

| Code          | Severity | Description              | Solution                               |
| ------------- | -------- | ------------------------ | -------------------------------------- |
| `COMPTIME301` | ❌ Error | Method execution timeout | Increase timeout or fix infinite loops |
| `COMPTIME302` | ❌ Error | Method execution error   | Fix runtime exceptions in method       |

#### Execution Error Examples

```csharp
// ❌ COMPTIME302: Method execution error
[CompileTime]
public static int MethodWithException()
{
    throw new InvalidOperationException("This will cause COMPTIME302");
    return 42;
}

// ✅ Solution:
[CompileTime]
public static int SafeMethod()
{
    try
    {
        return RiskyCalculation();
    }
    catch
    {
        return 0; // Provide fallback value
    }
}
```

```csharp
// ❌ COMPTIME301: Execution timeout
[CompileTime]
public static int TimeoutMethod()
{
    for (int i = 0; i < int.MaxValue; i++)
    {
        // This will timeout
        Math.Sqrt(i);
    }
    return 42;
}

// ✅ Solution:
[CompileTime(TimeoutMs = 10000)] // Increase timeout
public static int OptimizedMethod()
{
    // Use reasonable limits
    for (int i = 0; i < 1000; i++)
    {
        Math.Sqrt(i);
    }
    return 42;
}
```

---

## 🔧 Generator Diagnostics (COMPTIME901-999)

### Source Generation and Compilation Issues

| Code          | Severity | Description                  | Solution                             |
| ------------- | -------- | ---------------------------- | ------------------------------------ |
| `COMPTIME901` | ❌ Error | Source generation error      | Check generator logs and file access |
| `COMPTIME997` | 💡 Info  | Performance report generated | Report successfully created          |
| `COMPTIME998` | 💡 Info  | CompileTime summary          | Build summary information            |

#### Generator Error Examples

```csharp
// ❌ COMPTIME901: Source generation error
// Usually caused by:
// 1. Missing interceptor configuration
// 2. File access issues
// 3. Complex method signatures

// ✅ Check interceptor configuration:
```

```xml
<PropertyGroup>
  <!-- REQUIRED: Enable interceptors -->
  <Features>InterceptorsPreview</Features>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>
</PropertyGroup>
```

---

## 🔧 Troubleshooting Guide

### Common Issues and Solutions

#### 1. **No methods are being processed**

**Symptoms:**

- Methods with `[CompileTime]` run at runtime
- No interceptor files generated

**Solutions:**

```xml
<!-- Check interceptor configuration -->
<PropertyGroup>
  <Features>InterceptorsPreview</Features>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>
</PropertyGroup>

<!-- Check if CompileTime is enabled -->
<CompileTimeEnabled>true</CompileTimeEnabled>

<!-- Enable file generation to see what's happening -->
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
```

#### 2. **Build hangs or takes too long**

**Symptoms:**

- Build process freezes
- Very long compilation times

**Solutions:**

```xml
<!-- Reduce timeout for faster builds -->
<CompileTimeTimeout>2000</CompileTimeTimeout>

<!-- Use Skip behavior to avoid build failures -->
<CompileTimeTimeoutBehavior>Skip</CompileTimeTimeoutBehavior>

<!-- Disable in Debug builds -->
<CompileTimeEnabled Condition="'$(Configuration)' == 'Debug'">false</CompileTimeEnabled>
```

#### 3. **COMPTIME901 errors**

**Symptoms:**

- "Failed to generate interceptors"
- "Object reference not set to an instance of an object"

**Solutions:**

```csharp
// Ensure methods are simple and deterministic
[CompileTime]
public static int SimpleMethod() => 42; // ✅ Good

// Avoid complex patterns
[CompileTime]
public static T GenericMethod<T>() => default(T); // ❌ May cause issues
```

#### 4. **Performance warnings (COMPTIME012)**

**Symptoms:**

- Build warnings about slow methods
- Methods taking longer than expected

**Solutions:**

```csharp
// Option 1: Increase threshold
[CompileTime(PerformanceWarningThresholdMs = 5000)]
public static int SlowMethod() => ExpensiveCalculation();

// Option 2: Suppress warnings
[CompileTime(SuppressWarnings = true)]
public static int QuietMethod() => ExpensiveCalculation();

// Option 3: Optimize the method
[CompileTime]
public static int OptimizedMethod() => FastCalculation();
```

---

## 📊 Diagnostic Configuration

### Enable Detailed Diagnostics

```xml
<PropertyGroup>
  <!-- Generate detailed reports -->
  <CompileTimeGenerateReport>true</CompileTimeGenerateReport>

  <!-- Show generated files -->
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>

  <!-- Enable all warnings -->
  <CompileTimeSuppressWarnings>false</CompileTimeSuppressWarnings>

  <!-- Use Warning timeout behavior for debugging -->
  <CompileTimeTimeoutBehavior>Warning</CompileTimeTimeoutBehavior>
</PropertyGroup>
```

### Debug-Friendly Configuration

```xml
<!-- For troubleshooting -->
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <CompileTimeEnabled>true</CompileTimeEnabled>
  <CompileTimeTimeout>30000</CompileTimeTimeout>
  <CompileTimeTimeoutBehavior>Warning</CompileTimeTimeoutBehavior>
  <CompileTimeGenerateReport>true</CompileTimeGenerateReport>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompileTimeSuppressWarnings>false</CompileTimeSuppressWarnings>
</PropertyGroup>
```

---

## 💡 Best Practices for Error Prevention

### ✅ Do's

1. **Keep methods simple and deterministic**
2. **Set appropriate timeouts** for your methods
3. **Use performance thresholds** to catch slow methods early
4. **Test methods both at compile-time and runtime**
5. **Enable reporting** to monitor performance

### ❌ Don'ts

1. **Don't ignore timeout warnings** - they indicate potential issues
2. **Don't suppress all warnings globally** - be selective
3. **Don't use CompileTime for non-deterministic operations** (unless Cache = Never)
4. **Don't forget the interceptor configuration** - it's mandatory

### 🔧 Method Design Guidelines

```csharp
// ✅ Good CompileTime method
[CompileTime]
public static double CalculateConstant()
{
    return Math.PI * Math.E; // Simple, deterministic, fast
}

// ❌ Problematic CompileTime method
[CompileTime]
public static string GetRandomData()
{
    var random = new Random();
    Thread.Sleep(random.Next(1000, 5000)); // Non-deterministic, slow, random timing
    return HttpClient.GetStringAsync("https://api.example.com/data").Result; // Network call, can fail
}

// ✅ Better approach for the above
[CompileTime(Cache = CacheStrategy.Never)] // Don't cache random data
public static string GetRandomData()
{
    return Guid.NewGuid().ToString(); // Simple, no network, deterministic per execution
}
```

---

## 📞 Getting Help

If you encounter errors not covered in this guide:

1. **Enable detailed diagnostics** (see configuration above)
2. **Check the generated report** (`CompileTimeReport.md`)
3. **Review generated files** (if `EmitCompilerGeneratedFiles` is enabled)
4. **Create a minimal reproduction** case
Report the issue with diagnostic IDs and configuration details
