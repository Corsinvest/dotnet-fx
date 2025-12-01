# ⚙️ CompileTime MSBuild Properties

Complete guide for configuring CompileTime behavior through MSBuild properties in your `.csproj` file.

---

## 🚀 Quick Setup

### Minimal Configuration

```xml
<PropertyGroup>
  <!-- Enable Interceptors (REQUIRED) -->
  <Features>InterceptorsPreview</Features>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>
</PropertyGroup>

<PackageReference Include="Corsinvest.Fx.CompileTime" Version="1.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

### Full Configuration Example

```xml
<PropertyGroup>
  <!-- REQUIRED: Enable Interceptors -->
  <Features>InterceptorsPreview</Features>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>

  <!-- CompileTime Core Settings -->
  <CompileTimeEnabled>true</CompileTimeEnabled>
  <CompileTimeTimeout>5000</CompileTimeTimeout>
  <CompileTimeBehaviorOnTimeout>Skip</CompileTimeBehaviorOnTimeout>

  <!-- Reporting -->
  <CompileTimeReport>false</CompileTimeReport>

  <!-- Debug Mode -->
  <CompileTimeDebugMode>None</CompileTimeDebugMode>
</PropertyGroup>
```

---

## 📋 Properties Reference

### 🔧 Core Settings

#### `CompileTimeEnabled`

**Enable or disable CompileTime processing globally.**

| Property             | Type   | Default | Description                              |
| -------------------- | ------ | ------- | ---------------------------------------- |
| `CompileTimeEnabled` | `bool` | `true`  | Master switch for CompileTime processing |

```xml
<!-- Enable CompileTime (default) -->
<CompileTimeEnabled>true</CompileTimeEnabled>

<!-- Disable CompileTime globally -->
<CompileTimeEnabled>false</CompileTimeEnabled>

<!-- Conditional based on configuration -->
<CompileTimeEnabled Condition="'$(Configuration)' == 'Release'">true</CompileTimeEnabled>
<CompileTimeEnabled Condition="'$(Configuration)' == 'Debug'">false</CompileTimeEnabled>
```

**Use Cases:**

- Disable in CI/CD for faster builds
- Enable only in Release builds
- Temporary disable during development

---

#### `CompileTimeTimeout`

**Global timeout for method execution in milliseconds.**

| Property             | Type  | Default | Description                                         |
| -------------------- | ----- | ------- | --------------------------------------------------- |
| `CompileTimeTimeout` | `int` | `5000`  | Global timeout in milliseconds for method execution |

```xml
<!-- Default timeout (5 seconds) -->
<CompileTimeTimeout>5000</CompileTimeTimeout>

<!-- Short timeout for CI builds -->
<CompileTimeTimeout>2000</CompileTimeTimeout>

<!-- Long timeout for complex calculations -->
<CompileTimeTimeout>30000</CompileTimeTimeout>

<!-- Per-environment configuration -->
<CompileTimeTimeout Condition="'$(BuildEnvironment)' == 'CI'">1000</CompileTimeTimeout>
<CompileTimeTimeout Condition="'$(BuildEnvironment)' != 'CI'">10000</CompileTimeTimeout>
```

---

#### `CompileTimeBehaviorOnTimeout`

**Define what happens when methods timeout.**

| Property                     | Type     | Default | Options                    | Description                              |
| ---------------------------- | -------- | ------- | -------------------------- | ---------------------------------------- |
| `CompileTimeBehaviorOnTimeout` | `string` | `Skip`  | `Skip`, `Error`, `Warning` | Behavior when method execution times out |

```xml
<!-- Skip timed-out methods (default) -->
<CompileTimeBehaviorOnTimeout>Skip</CompileTimeBehaviorOnTimeout>

<!-- Fail build on timeout -->
<CompileTimeBehaviorOnTimeout>Error</CompileTimeBehaviorOnTimeout>

<!-- Use default value and warn -->
<CompileTimeBehaviorOnTimeout>Warning</CompileTimeBehaviorOnTimeout>
```

**Timeout Behaviors Explained:**

| Value     | Behavior              | Build Result         | Generated Code          |
| --------- | --------------------- | -------------------- | ----------------------- |
| `Skip`    | 🚫 Skip method        | ✅ Success + Warning | Uses type default value |
| `Error`   | ❌ Fail build         | ❌ Build Error       | No code generated       |
| `Warning` | ⚠️ Use default + warn | ✅ Success + Warning | Uses type default value |

---

### 📊 Reporting and Diagnostics

#### `CompileTimeReport`

**Enable detailed performance and analytics reporting.**

| Property                    | Type   | Default | Description                                      |
| --------------------------- | ------ | ------- | ------------------------------------------------ |
| `CompileTimeReport` | `bool` | `false` | Generate detailed CompileTime performance report |

```xml
<!-- Enable reporting -->
<CompileTimeReport>true</CompileTimeReport>

<!-- Enable only in specific configurations -->
<CompileTimeReport Condition="'$(Configuration)' == 'Release'">true</CompileTimeReport>
```

**Generated Report Contents:**

- ⏱️ **Execution Times** - Method-by-method performance
- 🎯 **Performance Bottlenecks** - Slowest methods identified
- 📈 **Build Impact Analysis** - How CompileTime affects build time
- 🗃️ **Cache Statistics** - Hit/miss ratios and effectiveness
- 🔢 **Method Statistics** - Success/failure counts

**Report File Location:**

The report is always generated as `CompileTimeReport.md` in the project root directory (where your `.csproj` file is located).

```text
YourProject/
├── YourProject.csproj
├── CompileTimeReport.md         ← Report generated here
└── obj/
    └── CompileTimeCache.json    ← Persistent cache
```

---

### 🐛 Debug Settings

#### `CompileTimeDebugMode`

**Control debug output for troubleshooting CompileTime issues.**

| Property               | Type     | Default | Options                | Description                                  |
| ---------------------- | -------- | ------- | ---------------------- | -------------------------------------------- |
| `CompileTimeDebugMode` | `string` | `None`  | `None`, `Files`, `Verbose` | Debug mode for troubleshooting CompileTime |

```xml
<!-- Default: No debug output (production) -->
<CompileTimeDebugMode>None</CompileTimeDebugMode>

<!-- Files: Write debug JSON files -->
<CompileTimeDebugMode>Files</CompileTimeDebugMode>

<!-- Verbose: Files + detailed MSBuild logging -->
<CompileTimeDebugMode>Verbose</CompileTimeDebugMode>

<!-- Enable only for debugging -->
<CompileTimeDebugMode Condition="'$(Configuration)' == 'Debug'">Files</CompileTimeDebugMode>
```

**Debug Mode Behaviors:**

| Mode      | Debug Files | MSBuild Logging | Performance Impact | Use Case                     |
| --------- | ----------- | --------------- | ------------------ | ---------------------------- |
| `None`    | ❌          | ❌              | None               | Production builds            |
| `Files`   | ✅          | ❌              | Minimal            | Debugging communication      |
| `Verbose` | ✅          | ✅              | Moderate           | Deep troubleshooting         |

**Debug Files Generated (when `Files` or `Verbose`):**

```text
YourProject/
├── obj/
│   └── CompileTime/
│       ├── debug_request.json   ← Request sent to Generator
│       └── debug_response.json  ← Response from Generator
└── CompileTimeDiagnostics.json  ← Diagnostics for Analyzer (always)
```

**Example Debug Request (`debug_request.json`):**
```json
{
  "Methods": [
    {
      "Namespace": "MyApp",
      "ClassName": "Calculator",
      "MethodName": "Add",
      "Parameters": "2,3"
    }
  ]
}
```

**Example Debug Response (`debug_response.json`):**
```json
[
  {
    "Success": true,
    "Result": "5",
    "ExecutionTimeMs": 42
  }
]
```

---

#### `EmitCompilerGeneratedFiles`

**View the generated interceptor files for debugging and learning.**

| Property                     | Type   | Default | Description                                          |
| ---------------------------- | ------ | ------- | ---------------------------------------------------- |
| `EmitCompilerGeneratedFiles` | `bool` | `false` | Output generated source files to disk for inspection |

```xml
<!-- Enable to see generated interceptor files -->
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>

<!-- Conditional output (useful for development) -->
<EmitCompilerGeneratedFiles Condition="'$(Configuration)' == 'Debug'">true</EmitCompilerGeneratedFiles>
```

**Generated Files Location:**

```text
YourProject/
├── obj/
│   └── Debug/
│       └── generated/
│           └── Corsinvest.CompileTime/
│               └── Corsinvest.CompileTime.CompileTimeGenerator/
│                   ├── InterceptsLocationAttribute.g.cs     ← Interceptor attribute definition
│                   └── CompileTime_ABC123_YourClass.g.cs    ← Generated interceptor methods
```

**Use Cases:**

- 🔍 **Debugging**: See exactly what code is generated
- 📚 **Learning**: Understand how interceptors work
- 🧪 **Verification**: Ensure correct values are generated
- 🔧 **Troubleshooting**: Diagnose compilation issues

**How to View Generated Files:**

1. **Enable file generation** in your `.csproj`:

   ```xml
   <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
   ```

2. **Build your project**:

   ```bash
   dotnet build
   ```

3. **Navigate to the generated files**:

   ```text
   YourProject/obj/Debug/generated/Corsinvest.CompileTime/Corsinvest.CompileTime.CompileTimeGenerator/
   ```

4. **Open the `.g.cs` files** to see the generated code:

   **InterceptsLocationAttribute.g.cs** - Attribute definition:

   ```csharp
   [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
   internal sealed class InterceptsLocationAttribute : Attribute
   {
       public InterceptsLocationAttribute(string filePath, int line, int character) { }
   }
   ```

   **CompileTime_ABC123_YourClass.g.cs** - Interceptor methods:

   ```csharp
   [InterceptsLocation(@"C:\YourProject\YourClass.cs", line: 42, character: 18)]
   public static double CalculatePi_Generated()
   {
       return 3.141592653589793; // Pre-computed value!
   }
   ```

---

## 🎯 Configuration Patterns

### Development vs Production

```xml
<PropertyGroup>
  <Features>InterceptorsPreview</Features>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>
</PropertyGroup>

<!-- Development: Fast builds, limited CompileTime -->
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <CompileTimeEnabled>false</CompileTimeEnabled>
  <CompileTimeSuppressWarnings>true</CompileTimeSuppressWarnings>
</PropertyGroup>

<!-- Production: Full CompileTime with monitoring -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <CompileTimeEnabled>true</CompileTimeEnabled>
  <CompileTimeTimeout>10000</CompileTimeTimeout>
  <CompileTimeBehaviorOnTimeout>Error</CompileTimeBehaviorOnTimeout>
  <CompileTimeReport>true</CompileTimeReport>
  <CompileTimePerformanceThreshold>2000</CompileTimePerformanceThreshold>
</PropertyGroup>
```

### CI/CD Optimized

```xml
<PropertyGroup>
  <Features>InterceptorsPreview</Features>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>
</PropertyGroup>

<!-- CI/CD: Fast builds with error handling -->
<PropertyGroup Condition="'$(BuildEnvironment)' == 'CI'">
  <CompileTimeEnabled>true</CompileTimeEnabled>
  <CompileTimeTimeout>2000</CompileTimeTimeout>
  <CompileTimeBehaviorOnTimeout>Skip</CompileTimeBehaviorOnTimeout>
  <CompileTimeReport>false</CompileTimeReport>
</PropertyGroup>

<!-- Local development: Full features -->
<PropertyGroup Condition="'$(BuildEnvironment)' != 'CI'">
  <CompileTimeEnabled>true</CompileTimeEnabled>
  <CompileTimeTimeout>10000</CompileTimeTimeout>
  <CompileTimeBehaviorOnTimeout>Warning</CompileTimeBehaviorOnTimeout>
  <CompileTimeReport>true</CompileTimeReport>
</PropertyGroup>
```

### Performance Monitoring

```xml
<PropertyGroup>
  <Features>InterceptorsPreview</Features>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>

  <!-- Enable comprehensive monitoring -->
  <CompileTimeEnabled>true</CompileTimeEnabled>
  <CompileTimeReport>true</CompileTimeReport>

  <!-- Timeout configuration -->
  <CompileTimeTimeout>5000</CompileTimeTimeout>
  <CompileTimeBehaviorOnTimeout>Error</CompileTimeBehaviorOnTimeout>
</PropertyGroup>
```

### Experimental/Testing

```xml
<PropertyGroup>
  <Features>InterceptorsPreview</Features>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>

  <!-- Conservative settings for testing -->
  <CompileTimeEnabled>true</CompileTimeEnabled>
  <CompileTimeTimeout>1000</CompileTimeTimeout>
  <CompileTimeBehaviorOnTimeout>Skip</CompileTimeBehaviorOnTimeout>
  <CompileTimeReport>true</CompileTimeReport>
  <CompileTimeDebugMode>Files</CompileTimeDebugMode>
</PropertyGroup>
```

---

## 🔗 Environment Variables

You can also use environment variables to control CompileTime behavior:

```bash
# Disable CompileTime via environment
set COMPILETIME_ENABLED=false

# Set timeout via environment
set COMPILETIME_TIMEOUT=3000

# Enable reporting via environment
set COMPILETIME_GENERATE_REPORT=true
```

Use in `.csproj`:

```xml
<PropertyGroup>
  <CompileTimeEnabled Condition="'$(COMPILETIME_ENABLED)' != ''">$(COMPILETIME_ENABLED)</CompileTimeEnabled>
  <CompileTimeTimeout Condition="'$(COMPILETIME_TIMEOUT)' != ''">$(COMPILETIME_TIMEOUT)</CompileTimeTimeout>
  <CompileTimeReport Condition="'$(COMPILETIME_GENERATE_REPORT)' != ''">$(COMPILETIME_GENERATE_REPORT)</CompileTimeReport>
</PropertyGroup>
```

---

## 🛠️ Troubleshooting

### Common Issues

#### 1. Interceptors Not Working

```xml
<!-- REQUIRED: Missing interceptor configuration -->
<Features>InterceptorsPreview</Features>
<InterceptorsNamespaces>$(InterceptorsNamespaces);CompileTime</InterceptorsNamespaces>
```

#### 2. Methods Not Processing

```xml
<!-- Check if CompileTime is enabled -->
<CompileTimeEnabled>true</CompileTimeEnabled>

<!-- Check timeout settings -->
<CompileTimeTimeout>5000</CompileTimeTimeout>
<CompileTimeBehaviorOnTimeout>Skip</CompileTimeBehaviorOnTimeout>
```

#### 3. Build Performance Issues

```xml
<!-- Reduce timeout for faster builds -->
<CompileTimeTimeout>2000</CompileTimeTimeout>

<!-- Disable in Debug builds -->
<CompileTimeEnabled Condition="'$(Configuration)' == 'Debug'">false</CompileTimeEnabled>

<!-- Suppress warnings in CI -->
<CompileTimeSuppressWarnings Condition="'$(BuildEnvironment)' == 'CI'">true</CompileTimeSuppressWarnings>
```

### Debugging Configuration

```xml
<PropertyGroup>
  <!-- Enable verbose logging -->
  <CompileTimeReport>true</CompileTimeReport>
  <CompileTimeSuppressWarnings>false</CompileTimeSuppressWarnings>

  <!-- Use lenient settings -->
  <CompileTimeTimeout>30000</CompileTimeTimeout>
  <CompileTimeBehaviorOnTimeout>Warning</CompileTimeBehaviorOnTimeout>
  <CompileTimePerformanceThreshold>5000</CompileTimePerformanceThreshold>
</PropertyGroup>
```

---

## 💡 Best Practices

### ✅ Recommended Configurations

1. **Use conditional compilation** for different environments
2. **Enable reporting** in CI/CD for performance monitoring
3. **Set appropriate timeouts** based on your methods' complexity
4. **Use Skip behavior** for non-critical build environments
5. **Disable in Debug** builds for faster development

### ❌ Avoid These Patterns

1. **Don't set timeout too low** (< 1000ms) unless necessary
2. **Don't suppress warnings globally** without good reason
3. **Don't enable Error timeout behavior** in CI without testing
4. **Don't forget** the required interceptor configuration

### 🔧 MSBuild Integration Tips

```xml
<!-- Custom MSBuild targets -->
<Target Name="CompileTimeReport" AfterTargets="Build" Condition="'$(CompileTimeReport)' == 'true'">
  <Message Text="CompileTime report generated at: $(MSBuildProjectDirectory)\CompileTimeReport.md" Importance="high" />
</Target>

<!-- Conditional package reference -->
<PackageReference Include="Corsinvest.Fx.CompileTime" Version="1.0.0" Condition="'$(CompileTimeEnabled)' == 'true'">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

---
