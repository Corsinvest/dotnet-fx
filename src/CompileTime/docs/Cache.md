# 💾 Cache System Reference

Complete documentation for the CompileTime caching system - how it works, strategies, and troubleshooting.

---

## 🔧 Quick Reference

```csharp
// Default - Session cache (cache only during current build)
[CompileTime]
public static int Calculate() => 42;

// Persistent - Cache across builds (fastest)
[CompileTime(Cache = CacheStrategy.Persistent)]
public static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);

// Never - Always re-execute (no cache)
[CompileTime(Cache = CacheStrategy.Never)]
public static string GetTimestamp() => DateTime.Now.ToString();
```

---

## 📊 Cache Strategies

The `CacheStrategy` enum controls how method execution results are cached between builds.

```csharp
public enum CacheStrategy
{
    Default = 0,     // Same as Session
    Persistent = 1,  // Cache across builds
    Never = 2        // Never cache
}
```

### Strategy Comparison

| Strategy     | Scope         | Persistence | Use Case                           |
| ------------ | ------------- | ----------- | ---------------------------------- |
| `Default`    | Build session | In-memory   | General purpose                    |
| `Persistent` | Across builds | Disk        | Expensive, deterministic computations |
| `Never`      | None          | None        | Time-dependent or non-deterministic values |

---

## 🎯 When to Use Each Strategy

### ✅ Use `Default` (Session) When:

- Method is fast (<100ms)
- Result might change between code edits
- Not performance-critical
- Testing/development

**Example:**
```csharp
[CompileTime] // Default = CacheStrategy.Default
public static string GetEnvironment() =>
    #if DEBUG
        "Development"
    #else
        "Production"
    #endif;
```

---

### ✅ Use `Persistent` When:

- Method is expensive (>500ms)
- Result is deterministic (same inputs → same output)
- Method body rarely changes
- Performance is critical

**Example:**
```csharp
[CompileTime(Cache = CacheStrategy.Persistent)]
public static BigInteger CalculateLargePrime(int digits)
{
    // Expensive computation that takes seconds
    // Result is always the same for same input
}

[CompileTime(Cache = CacheStrategy.Persistent)]
public static string CompressData(string data)
{
    using var ms = new MemoryStream();
    using var gz = new GZipStream(ms, CompressionMode.Compress);
    gz.Write(Encoding.UTF8.GetBytes(data));
    return Convert.ToBase64String(ms.ToArray());
}
```

---

### ✅ Use `Never` When:

- Method uses time-dependent values (`DateTime.Now`, `Guid.NewGuid()`)
- Method uses randomness
- Result should be fresh on every build
- Testing scenarios where you need consistent re-execution

**Example:**
```csharp
[CompileTime(Cache = CacheStrategy.Never)]
public static string GetBuildTimestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

[CompileTime(Cache = CacheStrategy.Never)]
public static string GetBuildId() => Guid.NewGuid().ToString("N");

[CompileTime(Cache = CacheStrategy.Never)]
public static int GetRandomSeed() => new Random().Next();
```

---

## 🔍 How Cache Works

### Cache Key Generation

Each method invocation is uniquely identified by:

```
Namespace + ClassName + MethodName + Parameters
```

**Example:**
```csharp
namespace MyApp.Utils;

public static class Math
{
    [CompileTime(Cache = CacheStrategy.Persistent)]
    public static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);
}

// Cache keys generated:
// Call: Factorial(5)  → Key: "MyApp.Utils.Math.Factorial(5)"  → Hash: "A1B2C3D4"
// Call: Factorial(10) → Key: "MyApp.Utils.Math.Factorial(10)" → Hash: "E5F6G7H8"
```

**Hash Function:** FNV-1a 32-bit (8 hex characters)
- Collision probability: ~0.01% for 1000 methods
- Deterministic: same input always produces same hash

---

### Cache Storage

#### Session Cache (In-Memory)
- Stored in: MSBuild task memory during build
- Lifetime: Current `dotnet build` session
- Location: RAM only
- Cleared: When build completes

#### Persistent Cache (Disk)
- Stored in: `obj/CompileTimeCache.json`
- Lifetime: Across builds until invalidated
- Location: Project's `obj/` directory
- Cleared: `dotnet clean` or cache invalidation

**Cache File Structure:**
```json
{
  "Version": "1.0",
  "CreatedAt": "2025-01-15T10:30:00Z",
  "Entries": {
    "A1B2C3D4": {
      "MethodContentHash": "abc123...",
      "Success": true,
      "SerializedValue": "120",
      "ValueType": "System.Int32",
      "CachedAt": "2025-01-15T10:30:00Z",
      "ExecutionTimeMs": 15,
      "MemoryFootprintBytes": 1024
    }
  }
}
```

---

## 🔄 Cache Invalidation

The cache is automatically invalidated when:

### 1. Method Body Changes

```csharp
[CompileTime(Cache = CacheStrategy.Persistent)]
public static int Calculate() => 42;
// Build 1: Cache stored

[CompileTime(Cache = CacheStrategy.Persistent)]
public static int Calculate() => 43; // Body changed!
// Build 2: Cache invalidated → re-executed
```

**Detection:** Content hash of method body (IL code)
**Log:** `[CACHE INVALIDATED] Method body changed for Calculate`

---

### 2. Return Type Changes

```csharp
[CompileTime(Cache = CacheStrategy.Persistent)]
public static int GetValue() => 42;
// Build 1: Cache stored (ValueType: System.Int32)

[CompileTime(Cache = CacheStrategy.Persistent)]
public static string GetValue() => "42"; // Return type changed!
// Build 2: Cache invalidated → re-executed
```

**Detection:** `entry.ValueType != methodSymbol.ReturnType`
**Log:** `[CACHE INVALIDATED] Return type changed for GetValue (was: System.Int32, now: System.String)`

---

### 3. Manual Invalidation

Delete the cache file:

```bash
# Windows
del obj\CompileTimeCache.json

# Linux/Mac
rm obj/CompileTimeCache.json

# Or use dotnet clean (removes entire obj/ directory)
dotnet clean
```

---

## ⚠️ Cache Limitations

### What Cache **DOES NOT** Track

1. **External Dependencies**

   ```csharp
   public static class Config
   {
       public const int Value = 10; // If this changes...
   }

   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static int GetValue() => Config.Value; // ...cache is NOT invalidated
   ```

   **Workaround:** Use `CacheStrategy.Default` or manually delete cache after changing dependencies

2. **Parameter Type Changes**

   ```csharp
   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static int Process(int x) => x * 2;
   // Build 1: Process(5) → cached

   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static int Process(long x) => x * 2; // Parameter type changed
   // Build 2: Cache might still hit (same serialized value "5")
   ```

   **Workaround:** Delete cache manually or rename method

3. **Assembly References**

   Changes to referenced assemblies do NOT invalidate cache.

   **Workaround:** Use `dotnet clean` after updating dependencies

---

## 🐛 Troubleshooting

### Cache Not Working

**Symptom:** Method always re-executes even with `Persistent` cache

**Checklist:**
1. ✅ Verify `Cache = CacheStrategy.Persistent` is set
2. ✅ Check `obj/CompileTimeCache.json` exists after build
3. ✅ Ensure method body hasn't changed
4. ✅ Enable debug mode to see cache logs:

```xml
<PropertyGroup>
  <CompileTimeDebugMode>Verbose</CompileTimeDebugMode>
</PropertyGroup>
```

Build and check for logs:
```
[CACHE HIT] Using cached result for MyMethod
[CACHE MISS] Will execute method: MyMethod
[CACHE INVALIDATED] Method body changed for MyMethod
```

---

### Stale Cache

**Symptom:** Old cached value used after changing external dependencies

**Solution:**
```bash
# Clear cache manually
dotnet clean
dotnet build

# Or delete cache file only
rm obj/CompileTimeCache.json
dotnet build
```

**Prevention:**
Use `CacheStrategy.Default` for methods with external dependencies:

```csharp
// WRONG - External dependency with Persistent cache
[CompileTime(Cache = CacheStrategy.Persistent)]
public static int GetValue() => Config.SomeValue; // ❌ Cache won't invalidate

// RIGHT - External dependency with Default cache
[CompileTime(Cache = CacheStrategy.Default)]
public static int GetValue() => Config.SomeValue; // ✅ Re-executed each build
```

---

### Cache File Too Large

**Symptom:** `obj/CompileTimeCache.json` is >10MB

**Causes:**
- Too many persistent cached methods
- Large serialized return values (e.g., byte arrays)

**Solutions:**

1. **Use Default instead of Persistent** for rarely-called methods:
   ```csharp
   // Before: Always cached
   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static byte[] GetLargeData() => /* 1MB array */;

   // After: Only cached during build session
   [CompileTime(Cache = CacheStrategy.Default)]
   public static byte[] GetLargeData() => /* 1MB array */;
   ```

2. **Delete old cache periodically:**
   ```bash
   # Add to CI pipeline
   dotnet clean
   dotnet build
   ```

---

### Hash Collision (Very Rare)

**Symptom:** Two methods with different signatures get same cache key

**Probability:** <0.01% for typical projects (<1000 `[CompileTime]` methods)

**Detection:**
```
[CACHE INVALIDATED] Method body changed for MethodA
```
...but you didn't change MethodA (collision with MethodB).

**Solution:**
1. Delete `obj/CompileTimeCache.json`
2. File issue at [GitHub Issues](https://github.com/Corsinvest/dotnet-fx/issues) with:
   - Both method signatures
   - Generated cache keys

---

## 📈 Cache Performance

### Cache Hit Performance

```
Method execution time: 500ms
Cache hit time: <1ms
Speedup: >500x
```

### Cache File Size

Typical cache file sizes:

| Methods Cached | Avg Entry Size | Total Size |
| -------------- | -------------- | ---------- |
| 10             | ~200 bytes     | ~2KB       |
| 100            | ~200 bytes     | ~20KB      |
| 1,000          | ~200 bytes     | ~200KB     |

**Note:** Large return values (e.g., byte arrays) increase entry size significantly.

---

## 🔍 Cache Inspection

### View Cache File

```bash
# Windows
type obj\CompileTimeCache.json

# Linux/Mac
cat obj/CompileTimeCache.json
```

### Cache File Format

```json
{
  "Version": "1.0",
  "CreatedAt": "2025-01-15T10:30:00.000Z",
  "Entries": {
    "HASH_KEY": {
      "MethodContentHash": "abc123...",  // Hash of method IL code
      "Success": true,                   // Execution succeeded
      "SerializedValue": "120",          // JSON-serialized return value
      "ValueType": "System.Int32",       // Return type
      "ErrorMessage": null,              // Error message if failed
      "ErrorCode": null,                 // Error code if failed
      "CachedAt": "2025-01-15T10:30:00.000Z",  // When cached
      "ExecutionTimeMs": 15,             // Original execution time
      "MemoryFootprintBytes": 1024       // Memory used
    }
  }
}
```

---

## 🎯 Best Practices

### ✅ DO:

1. **Use Persistent for expensive, deterministic computations**
   ```csharp
   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static string HashPassword(string password) => /* ... */;
   ```

2. **Use Never for time-dependent values**
   ```csharp
   [CompileTime(Cache = CacheStrategy.Never)]
   public static string GetBuildTime() => DateTime.Now.ToString();
   ```

3. **Keep methods pure (no side effects)**
   - Deterministic output
   - No global state modification
   - No I/O operations

4. **Document cache strategy**
   ```csharp
   /// <summary>
   /// Computes factorial. Cached persistently due to expensive computation.
   /// </summary>
   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static int Factorial(int n) => /* ... */;
   ```

### ❌ DON'T:

1. **Don't use Persistent for non-deterministic methods**
   ```csharp
   // ❌ WRONG - Random results with Persistent cache
   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static int GetRandom() => new Random().Next();

   // ✅ RIGHT - Use Never
   [CompileTime(Cache = CacheStrategy.Never)]
   public static int GetRandom() => new Random().Next();
   ```

2. **Don't rely on cache for correctness**
   ```csharp
   // ❌ WRONG - Expecting cache invalidation on external change
   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static int GetValue() => ExternalConfig.Value;

   // ✅ RIGHT - Use Default or Never
   [CompileTime(Cache = CacheStrategy.Default)]
   public static int GetValue() => ExternalConfig.Value;
   ```

3. **Don't cache large return values persistently**
   ```csharp
   // ❌ WRONG - 10MB cache entry
   [CompileTime(Cache = CacheStrategy.Persistent)]
   public static byte[] GetLargeBlob() => new byte[10_000_000];

   // ✅ RIGHT - Session cache or compute at runtime
   [CompileTime(Cache = CacheStrategy.Default)]
   public static byte[] GetLargeBlob() => new byte[10_000_000];
   ```

---

## 🔗 Related Documentation

- [CompileTimeAttribute Reference](CompileTimeAttribute.md) - Complete attribute documentation
- [MSBuild Properties](MSBuildProperties.md) - Configuration options
- [Diagnostic IDs](DiagnosticIds.md) - Error codes and warnings

---

## 📚 Examples

### Example 1: Fibonacci with Persistent Cache

```csharp
[CompileTime(Cache = CacheStrategy.Persistent)]
public static int Fibonacci(int n)
{
    if (n <= 1) return n;
    return Fibonacci(n - 1) + Fibonacci(n - 2);
}

// First build: Computes Fibonacci(30) in ~500ms → cached
// Subsequent builds: Cache hit → <1ms
```

### Example 2: Build Metadata with Never Cache

```csharp
[CompileTime(Cache = CacheStrategy.Never)]
public static string GetBuildInfo() =>
    $"Built on {DateTime.Now:yyyy-MM-dd HH:mm:ss} by {Environment.UserName}";

// Every build: Fresh timestamp and username
```

### Example 3: Config Processing with Default Cache

```csharp
[CompileTime(Cache = CacheStrategy.Default)]
public static string ProcessConfig()
{
    // Reads from external file or config
    var config = File.ReadAllText("config.json");
    return JsonSerializer.Deserialize<AppConfig>(config).Value;
}

// Re-executed each build session (config might change)
```

---

## ❓ FAQ

**Q: Can I disable cache globally?**

A: No, but you can set `Cache = CacheStrategy.Never` on individual methods or disable CompileTime entirely:
```xml
<CompileTimeEnabled>false</CompileTimeEnabled>
```

**Q: Where is the cache file stored?**

A: `{ProjectRoot}/obj/CompileTimeCache.json`

**Q: How do I clear the cache?**

A: Run `dotnet clean` or delete `obj/CompileTimeCache.json` manually.

**Q: Does cache work across different machines (CI/CD)?**

A: No. The cache file is in `obj/` which is typically gitignored. Each machine builds its own cache.

**Q: What happens if I change parameter values?**

A: New cache entry is created. Each unique parameter combination has its own cache entry.

```csharp
Factorial(5)  → Cache entry 1
Factorial(10) → Cache entry 2 (different from entry 1)
```

**Q: Can cache cause build non-determinism?**

A: No. Cache only stores deterministic results. If a method is non-deterministic (e.g., `DateTime.Now`), use `Cache = CacheStrategy.Never`.

---

## 🛠️ Debugging Cache

Enable verbose logging:

```xml
<PropertyGroup>
  <CompileTimeDebugMode>Verbose</CompileTimeDebugMode>
</PropertyGroup>
```

Build output will show:

```
[CACHE MISS] Will execute method: Factorial
[CACHE HIT] Using cached result for Factorial
[CACHE INVALIDATED] Method body changed for Factorial
[CACHE INVALIDATED] Return type changed for Factorial (was: System.Int32, now: System.String)
```

---

This documentation covers the complete cache system. For issues or questions, visit [GitHub Issues](https://github.com/Corsinvest/dotnet-fx/issues).
