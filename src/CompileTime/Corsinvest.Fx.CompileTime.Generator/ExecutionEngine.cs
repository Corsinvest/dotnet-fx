using Corsinvest.Fx.CompileTime.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace Corsinvest.Fx.CompileTime.Generator;

internal class ExecutionEngine
{
    private static IReadOnlyDictionary<ProjectType, string[]> AdditionalUsings { get; } = new Dictionary<ProjectType, string[]>
    {
        [ProjectType.AspNetCore] = ["Microsoft.AspNetCore.Http", "Microsoft.AspNetCore.Mvc", "Microsoft.Extensions.DependencyInjection"],
        [ProjectType.WinForms] = ["System.Windows.Forms", "System.Drawing"],
        [ProjectType.WPF] = ["System.Windows", "System.Windows.Controls", "System.Windows.Media"],
        [ProjectType.Worker] = ["Microsoft.Extensions.Hosting", "Microsoft.Extensions.Logging"],
        [ProjectType.Default] = []
    };

    public async Task<CompileTimeResponse> ExecuteAsync(CompileTimeRequest request,
                                                        CompileTimeRequest.Item item)
    {
        var stopwatch = Stopwatch.StartNew();
        AssemblyLoadContext? loadContext = null;
        long memoryBefore = 0;

        try
        {
            memoryBefore = GC.GetAllocatedBytesForCurrentThread();
            loadContext = new AssemblyLoadContext(name: $"CompileTime-{Guid.NewGuid()}", isCollectible: true);

            var references = request.GlobalReferencePaths.Select(path => MetadataReference.CreateFromFile(path)).ToList();
            var assembly = CompileMethodToAssembly(request, item, references, loadContext);

            // Use the original fully qualified type name
            var typeName = string.IsNullOrEmpty(item.Namespace) || item.Namespace == "<global namespace>"
                            ? item.TypeName
                            : $"{item.Namespace}.{item.TypeName}";

            var type = assembly.GetType(typeName) ?? throw new TypeLoadException($"Could not find type '{typeName}' in the dynamically compiled assembly.");
            var methodInfo = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                 .FirstOrDefault(m => m.Name == item.MethodName && ParametersMatch(m, item.MethodParameterTypeNames)) ?? throw new MissingMethodException($"Could not find a matching method '{item.MethodName}' in type '{typeName}'.");


            var result = await ExecuteMethodAsync(methodInfo, ConvertParameters(methodInfo, item.Parameters)!, item.TimeoutMs);
            stopwatch.Stop();
            long memoryAfter = GC.GetAllocatedBytesForCurrentThread();

            var formattedValue = FormatValueAsLiteral(result, item.ReturnTypeFullName);
            return new()
            {
                Success = true,
                SerializedValue = formattedValue,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                MemoryFootprintBytes = GetMemoryFootprint(result, memoryAfter - memoryBefore),
                InvocationId = item.InvocationId
            };
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            return new()
            {
                Success = false,
                ErrorMessage = $"Method execution timed out after {item.TimeoutMs}ms",
                SerializedValue = GetDefaultValue(item.ReturnTypeFullName),
                InvocationId = item.InvocationId
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new()
            {
                Success = false,
                ErrorMessage = ex.ToString(),
                ErrorCode = "COMPTIME302",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                SerializedValue = GetDefaultValue(item.ReturnTypeFullName),
                InvocationId = item.InvocationId
            };
        }
        finally
        {
            loadContext?.Unload();
        }
    }

    private static async Task<object> ExecuteMethodAsync(MethodInfo methodInfo, object[] parameters, int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);

        if (IsAsyncMethod(methodInfo))
        {
            var task = (Task)methodInfo.Invoke(null, parameters)!;
            var timeoutTask = Task.Delay(timeoutMs, cts.Token);

            var completedTask = await Task.WhenAny(task, timeoutTask);
            if (completedTask == timeoutTask) { throw new TimeoutException(); }
            await task;

            if (methodInfo.ReturnType.IsGenericType)
            {
                var property = methodInfo.ReturnType.GetProperty("Result");
                return property?.GetValue(task)!;
            }

            return null!;
        }
        else
        {
            return await Task.Run(() =>
            {
                cts.Token.ThrowIfCancellationRequested();
                return methodInfo.Invoke(null, parameters)!;
            }, cts.Token);
        }
    }

    private static bool IsAsyncMethod(MethodInfo methodInfo)
        => methodInfo.ReturnType == typeof(Task)
            || (methodInfo.ReturnType.IsGenericType
                && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

    private static Assembly CompileMethodToAssembly(CompileTimeRequest request,
                                             CompileTimeRequest.Item item,
                                             IEnumerable<MetadataReference> references,
                                             AssemblyLoadContext loadContext)
    {
        // Extract usings from the provided ClassCode
        var syntaxTree = CSharpSyntaxTree.ParseText(item.ClassCode);
        var usings = syntaxTree.GetRoot()
                               .DescendantNodes()
                               .OfType<UsingDirectiveSyntax>()
                               .Select(u => u.Name?.ToString())
                               .Where(u => !string.IsNullOrEmpty(u))
                               .Distinct()
                               .ToList();

        usings.AddRange(
        [
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Linq",
            "System.Net.Http",
            "System.Threading",
            "System.Threading.Tasks",
            "Corsinvest.Fx.CompileTime"
        ]);

        if (AdditionalUsings.TryGetValue(request.ProjectType, out var projectUsings))
        {
            usings.AddRange(projectUsings);
        }

        var usingsBlock = string.Join("\n", usings.Distinct().Select(u => $"using {u};"));

        // Wrap the class code in the correct namespace if needed
        var codeWithNamespace = string.IsNullOrEmpty(item.Namespace) || item.Namespace == "<global namespace>"
            ? item.ClassCode
            : $@"namespace {item.Namespace}
{{
{item.ClassCode}
}}";

        var wrapperCode = $@"{usingsBlock}

{codeWithNamespace}";

        var compilation = CSharpCompilation.Create($"CompileTime_{Guid.NewGuid()}")
                                           .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                                           .AddReferences(references)
                                           .AddSyntaxTrees(CSharpSyntaxTree.ParseText(wrapperCode));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = string.Join("; ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()));
            throw new InvalidOperationException($"Compilation failed: {errors}\n\nGenerated code:\n{wrapperCode}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        return loadContext.LoadFromStream(ms);
    }

    private static bool ParametersMatch(MethodInfo methodInfo, List<string> symbolParameterTypeNames)
    {
        var reflectionParams = methodInfo.GetParameters();
        if (reflectionParams.Length != symbolParameterTypeNames.Count) { return false; }
        if (reflectionParams.Length == 0) { return true; }

        for (int i = 0; i < reflectionParams.Length; i++)
        {
            var reflectionTypeName = reflectionParams[i].ParameterType.FullName;
            if (reflectionTypeName is null) { return false; }

            if (!string.Equals(reflectionTypeName, symbolParameterTypeNames[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static long GetMemoryFootprint(object value, long allocationDelta)
    {
        if (value is null) { return 0; }
        if (value is string s) { return 24L + (s.Length * sizeof(char)); }
        if (value.GetType().IsPrimitive || value.GetType().IsEnum) { return Marshal.SizeOf(value.GetType()); }

        try
        {
            using var ms = new MemoryStream();
            JsonSerializer.Serialize(ms, value);
            return ms.Length;
        }
        catch { return allocationDelta; }
    }

    private string FormatValueAsLiteral(object value, string returnTypeName)
        => value == null
            ? GetDefaultValue(returnTypeName)
            : value switch
            {
                string str => FormatAsRawStringLiteral(str),
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => $"{l.ToString(CultureInfo.InvariantCulture)}L",
                float f => $"{f.ToString("R", CultureInfo.InvariantCulture)}f",
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                decimal dec => $"{dec.ToString(CultureInfo.InvariantCulture)}m",
                bool b => b ? "true" : "false",
                char c => $"'{c}'",
                DateTime dt => $"new DateTime({dt.Year}, {dt.Month}, {dt.Day}, {dt.Hour}, {dt.Minute}, {dt.Second})",
                Array array => FormatArrayLiteral(array, returnTypeName),
                _ => value.ToString() ?? GetDefaultValue(returnTypeName)
            };

    private static string FormatAsRawStringLiteral(string str)
    {
        int maxQuotes = 0, current = 0;
        foreach (char c in str)
        {
            current = c == '"' ? current + 1 : 0;
            if (current > maxQuotes) { maxQuotes = current; }
        }

        var quoteCount = Math.Max(3, maxQuotes + 1);
        string quotes = new('"', quoteCount);

        var content = str.Replace("\r\n", "\n").Replace("\r", "\n");

        if (!content.Contains('\n')) { return $"{quotes}{content}{quotes}"; }

        return $"{quotes}\n{content}\n{quotes}";
    }

    private string FormatArrayLiteral(Array array, string returnTypeName)
    {
        var elementType = returnTypeName.Replace("[]", string.Empty);
        if (array.Length == 0) { return $"new {elementType}[0]"; }

        var elements = new List<string>();
        foreach (var item in array)
        {
            elements.Add(FormatValueAsLiteral(item, elementType));
        }

        return $"new {elementType}[] {{ {string.Join(", ", elements)} }}";
    }

    private static object?[] ConvertParameters(MethodInfo methodInfo, object[] rawParameters)
    {
        var paramInfos = methodInfo.GetParameters();
        var converted = new object?[paramInfos.Length];

        for (int i = 0; i < paramInfos.Length; i++)
        {
            var targetType = paramInfos[i].ParameterType;
            var value = rawParameters[i];

            converted[i] = value is JsonElement elem
                            ? JsonSerializer.Deserialize(elem.GetRawText(), targetType)
                            : Convert.ChangeType(value, targetType);
        }

        return converted;
    }

    private static string GetDefaultValue(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) { return "null"; }

        // Handle arrays and collections with [] syntax
        if (typeName.EndsWith("[]")) { return "[]"; }

        if (typeName.Contains("System.Collections.Generic.IEnumerable") || typeName.Contains("System.Collections.IEnumerable"))
        {
            return "[]";
        }

        // Primitive types and common value types
        switch (typeName.Replace("global::", string.Empty))
        {
            case "bool":
            case "System.Boolean": return "false";
            case "byte":
            case "System.Byte": return "0";
            case "sbyte":
            case "System.SByte": return "0";
            case "char":
            case "System.Char": return "'\\0'";
            case "decimal":
            case "System.Decimal": return "0m";
            case "double":
            case "System.Double": return "0.0";
            case "float":
            case "System.Single": return "0.0f";
            case "int":
            case "System.Int32": return "0";
            case "uint":
            case "System.UInt32": return "0U";
            case "long":
            case "System.Int64": return "0L";
            case "ulong":
            case "System.UInt64": return "0UL";
            case "short":
            case "System.Int16": return "0";
            case "ushort":
            case "System.UInt16": return "0";
            case "string":
            case "System.String": return "\"\"";
            case "System.DateTime": return "new DateTime()"; // Or DateTime.MinValue
                                                             // Add other common value types like Guid, TimeSpan, etc. if needed
        }

        // If it's a struct, it's a value type. If it's a class, it's a reference type.
        // Without reflection on the actual Type object, this is hard to determine accurately from just the string name.
        // For now, assume anything not explicitly handled above is a reference type (returns null)
        // unless it's a known value type pattern.
        // This is still a heuristic, but better than before.
        if (typeName.StartsWith("System.") && (typeName.Contains("ValueTuple") || typeName.Contains("Nullable")))
        {
            return $"default({typeName})";
        }

        return "null"; // Default for reference types
    }
}
