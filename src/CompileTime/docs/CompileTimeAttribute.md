# 🎛️ CompileTimeAttribute Reference

Complete documentation for configuring the `[CompileTime]` attribute and its properties.

---

## 🔧 Quick Reference

```csharp
[CompileTime(
    Enabled = true,                        // Enable/disable processing
    Cache = CacheStrategy.Default,         // Caching strategy
    PerformanceWarningThresholdMs = 1000,  // Performance warning threshold
    SuppressWarnings = false,              // Suppress warnings
    TimeoutMs = -1                         // Method timeout (-1 = global)
)]
public static int MyMethod() => 42;
```

---

## 📋 Properties Reference

### ✅ `Enabled` Property

**Control whether individual methods are processed at compile-time.**

| Property  | Type   | Default | Description                                            |
| --------- | ------ | ------- | ------------------------------------------------------ |
| `Enabled` | `bool` | `true`  | Enable/disable compile-time processing for this method |

#### Enable/Disable Examples

```csharp
// ✅ ENABLED (default behavior)
[CompileTime] // Enabled = true is implicit
public static string GetAppName() => "MyApp";

// ✅ EXPLICITLY ENABLED
[CompileTime(Enabled = true)]
public static string GetVersion() => "1.0.0";

// ❌ DISABLED - runs at runtime
[CompileTime(Enabled = false)]
public static string GetCurrentTime() => DateTime.Now.ToString();

// 🔧 CONDITIONAL - based on build configuration
#if RELEASE
    [CompileTime(Enabled = true)]
#else
    [CompileTime(Enabled = false)] // Disabled in DEBUG builds
#endif
public static double ExpensiveCalculation()
{
    // Heavy computation only at compile-time in RELEASE
    return Enumerable.Range(1, 1000000).Sum(x => Math.Sqrt(x));
}
```

#### Use Cases

- **Development Speed**: Disable expensive calculations during development
- **Build Configuration**: Enable only in RELEASE builds
- **Temporary Disable**: Keep attribute but temporarily disable processing
- **Selective Processing**: Enable/disable based on conditions

---

### 🗃️ `Cache` Property

**Control how method execution results are cached.**

| Property | Type            | Default   | Description                         |
| -------- | --------------- | --------- | ----------------------------------- |
| `Cache`  | `CacheStrategy` | `Default` | Caching strategy for method results |

#### Cache Strategy Values

| Strategy     | Value | Behavior                 | Best For                                                  |
| ------------ | ----- | ------------------------ | --------------------------------------------------------- |
| `Default`    | `0`   | 💾 **Session cache**     | Most methods - caches during compilation session only     |
| `Persistent` | `1`   | 💽 **Cross-build cache** | Expensive deterministic calculations (saves to JSON file) |
| `Never`      | `2`   | 🚫 **No cache**          | Non-deterministic methods like `DateTime.Now`             |

#### Cache Examples

```csharp
// Default caching (session only)
[CompileTime]
public static int FastCalculation() => 42;

// Persistent caching (survives builds)
[CompileTime(Cache = CacheStrategy.Persistent)]
public static double[] GeneratePrimeNumbers()
{
    return Enumerable.Range(2, 1000)
                     .Where(IsPrime)
                     .ToArray();
}

// No caching (always execute)
[CompileTime(Cache = CacheStrategy.Never)]
public static DateTime GetBuildTimestamp() => DateTime.Now;

// Combined with other properties
[CompileTime(
    Cache = CacheStrategy.Persistent,
    PerformanceWarningThresholdMs = 5000
)]
public static string GenerateComplexConfiguration()
{
    // Expensive but deterministic calculation
    return BuildComplexConfig();
}
```

#### Cache Storage Location

When using `Cache = CacheStrategy.Persistent`, the cache is stored in your project's `obj/` folder:

```
YourProject/
├── YourProject.csproj
├── obj/
│   └── CompileTimeCache.json    ← Persistent cache stored here
└── src/
    └── YourClass.cs
```

**Cache Management:**

- 🗂️ **Automatic creation** - File is created when first needed
- 🔄 **Content validation** - Cache entries are invalidated when method content changes
- 🧹 **Build integration** - Cleared with `dotnet clean`, persists across normal builds
- 📁 **Project-specific** - Each project has its own cache file

---

### ⏱️ `PerformanceWarningThresholdMs` Property

**Set custom performance warning thresholds for individual methods.**

| Property                        | Type  | Default | Description                                                       |
| ------------------------------- | ----- | ------- | ----------------------------------------------------------------- |
| `PerformanceWarningThresholdMs` | `int` | `1000`  | Threshold in milliseconds before performance warning is triggered |

#### Performance Examples

```csharp
// Default threshold (1000ms)
[CompileTime]
public static int NormalMethod() => 42;

// Custom threshold - warn if > 500ms
[CompileTime(PerformanceWarningThresholdMs = 500)]
public static string FastMethod() => "quick";

// High threshold for known expensive operations
[CompileTime(PerformanceWarningThresholdMs = 5000)]
public static double[] ExpensiveCalculation()
{
    // This method is expected to take 2-3 seconds
    return CalculateLargeMatrix();
}

// Disable performance warnings (-1)
[CompileTime(PerformanceWarningThresholdMs = -1)]
public static void NoPerformanceWarning()
{
    Thread.Sleep(10000); // Won't trigger warning
}
```

---

### 🔇 `SuppressWarnings` Property

**Suppress all warnings for specific methods.**

| Property           | Type   | Default | Description                           |
| ------------------ | ------ | ------- | ------------------------------------- |
| `SuppressWarnings` | `bool` | `false` | Suppress all warnings for this method |

#### Usage Examples

```csharp
// Normal method (warnings enabled)
[CompileTime]
public static int NormalMethod() => SlowOperation();

// Suppress all warnings for this method
[CompileTime(SuppressWarnings = true)]
public static int QuietMethod()
{
    Thread.Sleep(2000); // No performance warning
    return 42;
}

// Combined with other properties
[CompileTime(
    SuppressWarnings = true,
    Cache = CacheStrategy.Never
)]
public static DateTime SilentTimestamp() => DateTime.Now;
```

---

### ⏰ `TimeoutMs` Property

**Set custom timeout values for individual methods.**

| Property    | Type  | Default | Description                                                        |
| ----------- | ----- | ------- | ------------------------------------------------------------------ |
| `TimeoutMs` | `int` | `-1`    | Method execution timeout in milliseconds (-1 = use global setting) |

#### Configuration Examples

```csharp
// Use global timeout setting (default)
[CompileTime]
public static int UseGlobalTimeout() => 42;

// Custom timeout - 2 seconds
[CompileTime(TimeoutMs = 2000)]
public static string CustomTimeout() => "done";

// Quick timeout for fast methods
[CompileTime(TimeoutMs = 100)]
public static int QuickMethod() => 1 + 1;

// Long timeout for complex operations
[CompileTime(TimeoutMs = 30000)] // 30 seconds
public static double[] ComplexCalculation()
{
    return RunComplexAlgorithm();
}
```

#### Timeout Behaviors

The global `CompileTimeTimeoutBehavior` setting determines what happens when a timeout occurs:

- **`Skip`** (default): Method is skipped, uses type default value, emits warning
- **`Error`**: Timeout causes build failure
- **`Warning`**: Uses type default value, emits warning, build continues

---

## 🎯 Combining Properties

You can combine multiple properties for fine-grained control:

### Example 1: Production-Ready Configuration

```csharp
[CompileTime(
    Enabled = true,
    Cache = CacheStrategy.Persistent,
    PerformanceWarningThresholdMs = 2000,
    SuppressWarnings = false,
    TimeoutMs = 5000
)]
public static Dictionary<string, object> GetAppConfiguration()
{
    return new Dictionary<string, object>
    {
        ["Version"] = "1.0.0",
        ["BuildDate"] = DateTime.Now,
        ["Features"] = new[] { "Feature1", "Feature2" }
    };
}
```

### Example 2: Debug vs Release Configuration

```csharp
#if DEBUG
[CompileTime(
    Enabled = false,              // Disabled in debug
    SuppressWarnings = true
)]
#else
[CompileTime(
    Enabled = true,               // Enabled in release
    Cache = CacheStrategy.Persistent,
    PerformanceWarningThresholdMs = 3000
)]
#endif
public static string[] GetAssetPaths()
{
    return Directory.GetFiles("assets", "*", SearchOption.AllDirectories);
}
```

### Example 3: Experimental Method

```csharp
[CompileTime(
    Enabled = false,              // Temporarily disabled
    Cache = CacheStrategy.Never,  // Don't cache when enabled
    SuppressWarnings = true,      // Suppress experimental warnings
    TimeoutMs = 1000             // Quick timeout for testing
)]
public static object ExperimentalFeature()
{
    // Under development
    return null;
}
```

---

## 💡 Best Practices

### ✅ Do's

- Use `Enabled = false` for temporary disabling instead of removing the attribute
- Set appropriate `PerformanceWarningThresholdMs` for expensive operations
- Use `Cache = CacheStrategy.Persistent` for deterministic expensive calculations
- Use `Cache = CacheStrategy.Never` for time-dependent or random operations
- Combine properties thoughtfully for your specific needs

### ❌ Don'ts

- Don't set `TimeoutMs` too low for complex operations
- Don't use `SuppressWarnings = true` globally - be selective
- Don't forget to re-enable methods with `Enabled = false` when ready
- Don't use persistent caching for non-deterministic methods

### 🔧 Configuration Patterns

```csharp
// Fast deterministic calculation
[CompileTime(Cache = CacheStrategy.Persistent)]

// Slow but important calculation
[CompileTime(PerformanceWarningThresholdMs = 5000)]

// Time-dependent method
[CompileTime(Cache = CacheStrategy.Never)]

// Experimental/debugging
[CompileTime(Enabled = false, SuppressWarnings = true)]

// Critical production method
[CompileTime(
    Cache = CacheStrategy.Persistent,
    TimeoutMs = 10000,
    PerformanceWarningThresholdMs = 3000
)]
```

---

## 📊 Monitoring and Debugging

Enable performance reporting to monitor your CompileTime methods:

```xml
<CompileTimeGenerateReport>true</CompileTimeGenerateReport>
```

The generated report will show:

- ⏱️ Execution times for each method
- 🗃️ Cache hit/miss statistics
- ⚠️ Performance warnings and timeouts
- 💡 Optimization suggestions

---
