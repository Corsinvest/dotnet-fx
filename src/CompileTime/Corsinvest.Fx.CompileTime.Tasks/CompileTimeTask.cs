using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Text.Json;
using RequiredDA = System.ComponentModel.DataAnnotations.RequiredAttribute;
using Corsinvest.Fx.CompileTime.Helpers;
using Corsinvest.Fx.CompileTime.Models;
using Corsinvest.Fx.CompileTime.Diagnostics;
using System.Xml.Linq;

namespace Corsinvest.Fx.CompileTime.Tasks;

public class CompileTimeTask : Microsoft.Build.Utilities.Task, IDisposable
{
    private static JsonSerializerOptions jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly SymbolDisplayFormat _fullClrFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
    );

    private readonly List<TaskDiagnosticData> _diagnostics = [];
    private readonly CacheManager _cacheManager = new();
    private PerformanceCollector? _performanceCollector;
    private CSharpCompilation? _compilation;
    private bool _disposed;

    [RequiredDA] public ITaskItem[] SourceFiles { get; set; } = [];
    [RequiredDA] public string ProjectDir { get; set; } = string.Empty;
    [RequiredDA] public string GeneratorAssemblyPath { get; set; } = string.Empty;
    public ITaskItem[] References { get; set; } = [];
    public string OutputPath { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 5000;
    public string TimeoutBehavior { get; set; } = "Skip";
    public bool CompileTimeEnabled { get; set; } = true;
    public bool GenerateReport { get; set; }
    public string DebugMode { get; set; } = "None";
    [Output] public ITaskItem[] GeneratedFiles { get; set; } = [];

    // Debug mode helper
    private bool IsVerboseLogging =>
        DebugMode.Equals("Verbose", StringComparison.OrdinalIgnoreCase);

    public override bool Execute() => ExecuteInternalAsync(CancellationToken.None).GetAwaiter().GetResult();

    private async Task<bool> ExecuteInternalAsync(CancellationToken cancellationToken)
    {
        _cacheManager.SetProjectDir(ProjectDir);
        _cacheManager.Load();

        if (GenerateReport) { _performanceCollector = new("Unknown", ProjectDir); }

        try
        {
            Log.LogMessage(MessageImportance.High, "🔄 COMPILETIME TASK STARTING! 🔄");
            Log.LogMessage(MessageImportance.High, "  - ProjectDir: {0}", ProjectDir);
            Log.LogMessage(MessageImportance.High, "  - CompileTimeEnabled: {0}", CompileTimeEnabled);
            Log.LogMessage(MessageImportance.High, "  - OutputPath: {0}", OutputPath);
            Log.LogMessage(MessageImportance.High, "  - SourceFiles count: {0}", SourceFiles?.Length ?? 0);

            if (!CompileTimeEnabled)
            {
                Log.LogMessage(MessageImportance.High, "❌ CompileTime execution is DISABLED");
                return true;
            }

            // 1. Create compilation once (reads files only once)
            _compilation = await CreateCompilationAsync(cancellationToken);
            if (_compilation == null) { return true; }

            // 2. Analyze compilation for CompileTime methods and invocations
            var (compileTimeMethods, invocations) = Analyzing(_compilation);
            Log.LogMessage(MessageImportance.High, "Found {0} CompileTime methods", compileTimeMethods.Count);
            Log.LogMessage(MessageImportance.High, "Found {0} method invocations", invocations.Count);

            // 3. Esegui i metodi e genera interceptors
            var generatedFiles = await GenerateInterceptorsAsync(compileTimeMethods, invocations, cancellationToken);

            GeneratedFiles = [.. generatedFiles];

            Log.LogMessage(MessageImportance.High, "✅ Generated {0} files", GeneratedFiles.Length);
            return true;
        }
        catch (Exception ex)
        {
            Log.LogError("❌ CompileTime execution failed: {0}", ex.Message);
            Log.LogError("Stack trace: {0}", ex.StackTrace);
            return false; // Blocca il build
        }
        finally
        {
            try
            {
                _cacheManager.Save();
                Log.LogMessage(MessageImportance.Low, "Cache saved.");

                if (GenerateReport)
                {
                    _performanceCollector!.StopTracking();
                    _performanceCollector.GenerateReportContent(_cacheManager.FileName);
                    Log.LogMessage(MessageImportance.High, "Performance report generated at {0}", _performanceCollector.FileName);
                }

                // Save diagnostics for CompileTimeDiagnosticAnalyzer
                await SaveDiagnosticsAsync(cancellationToken);

                // Dispose resources properly
                Dispose();

                // Force cleanup to prevent hanging MSBuild processes
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Log.LogMessage(MessageImportance.Low, "CompileTimeTask cleanup completed.");
            }
            catch (Exception ex)
            {
                Log.LogWarning("Error during cleanup: {0}", ex.Message);
            }
        }
    }

    private async Task<CSharpCompilation?> CreateCompilationAsync(CancellationToken cancellationToken)
    {
        Log.LogMessage("Creating compilation from source files...");

        // Parse all source files once
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var sourceFile in SourceFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(sourceFile.ItemSpec, cancellationToken);
                var tree = CSharpSyntaxTree.ParseText(content, path: sourceFile.ItemSpec, cancellationToken: cancellationToken);
                syntaxTrees.Add(tree);
            }
            catch (Exception ex)
            {
                Log.LogWarning("Could not parse {0}: {1}", sourceFile.ItemSpec, ex.Message);
            }
        }

        if (syntaxTrees.Count == 0)
        {
            Log.LogMessage("No valid syntax trees found");
            return null;
        }

        // Create compilation with references (using using statements for better disposal)
        var metadataReferences = new List<MetadataReference>();
        foreach (var reference in References)
        {
            try
            {
                metadataReferences.Add(MetadataReference.CreateFromFile(reference.ItemSpec));
            }
            catch (Exception ex)
            {
                Log.LogWarning("Could not load reference {0}: {1}", reference.ItemSpec, ex.Message);
            }
        }


        return CSharpCompilation.Create("CompileTimeAnalysis")
                                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                                .AddReferences(metadataReferences)
                                .AddSyntaxTrees(syntaxTrees);
    }

    private (List<IMethodSymbol>, List<InvocationInfo>) Analyzing(CSharpCompilation compilation)
    {
        Log.LogMessage("Analyzing compilation for [CompileTime]...");

        var methods = new List<IMethodSymbol>();
        var invocations = new List<InvocationInfo>();

        // Analyze each syntax tree for CompileTime methods
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            var methodNodes = root.DescendantNodes()
                                  .OfType<MethodDeclarationSyntax>()
                                  .Where(method => method.AttributeLists.Count > 0
                                                    && method.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == "CompileTime")));

            foreach (var methodNode in methodNodes)
            {
                var methodSymbol = semanticModel.GetDeclaredSymbol(methodNode);
                if (methodSymbol != null && CompileTimeHelper.IsValid(methodSymbol))
                {
                    methods.Add(methodSymbol);
                    Log.LogMessage("Found CompileTime method: {0}.{1}", methodSymbol.ContainingType.Name, methodSymbol.Name);
                }
            }

            // Get invocations: both MemberAccess (Class.Method()) and direct calls (Method())
            var invocationNodes = root.DescendantNodes()
                                      .OfType<InvocationExpressionSyntax>()
                                      .Where(inv => inv.Expression is MemberAccessExpressionSyntax
                                                 || inv.Expression is IdentifierNameSyntax);

            foreach (var invocationNode in invocationNodes)
            {
                var invocationInfo = InvocationInfo.Get(invocationNode, semanticModel);
                if (invocationInfo != null)
                {
                    invocations.Add(invocationInfo);
                    Log.LogMessage("Found invocation: {0}.{1}", invocationInfo.MethodSymbol.ContainingType.Name, invocationInfo.MethodSymbol.Name);
                }
            }
        }


        return (methods, invocations);
    }

    private async Task<List<ITaskItem>> GenerateInterceptorsAsync(List<IMethodSymbol> methods, List<InvocationInfo> invocations, CancellationToken cancellationToken)
    {
        Log.LogMessage("Generating interceptor files...");

        var generatedFiles = new List<ITaskItem>();

        // Generate InterceptsLocationAttribute first
        var attributeFilePath = Path.Combine(OutputPath, "InterceptsLocationAttribute.g.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(attributeFilePath)!);

        await File.WriteAllTextAsync(attributeFilePath, GetSourceInterceptsLocationAttribute(), cancellationToken);
        generatedFiles.Add(new TaskItem(attributeFilePath));

        Log.LogMessage("Generated: {0}", attributeFilePath);

        // If no invocations found, only generate the attribute
        if (invocations.Count == 0)
        {
            Log.LogMessage("No invocations found, skipping interceptor generation");
            return generatedFiles;
        }

        // create request - deduplicate requests but keep track of all calls
        var request = new CompileTimeRequest();
        var uniqueRequests = new Dictionary<string, CompileTimeRequest.Item>();
        var callsForRequest = new Dictionary<string, List<InvocationInfo>>();
        var cacheHits = new Dictionary<string, CompileTimeResponse>();

        foreach (var invocation in invocations)
        {
            var methodSymbol = invocation.MethodSymbol;
            var attr = CompileTimeHelper.GetAttributes(methodSymbol);
            var timeoutMs = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.TimeoutMs), TimeoutMs);
            var performanceThreshold = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.PerformanceWarningThresholdMs), new CompileTimeAttribute().PerformanceWarningThresholdMs);

            var syntaxNode = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken);
            if (syntaxNode == null)
            {
                Log.LogWarning("Could not find syntax node for method symbol {0}. Skipping invocation.", methodSymbol.Name);
                continue;
            }

            var classDeclarationSyntax = syntaxNode.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDeclarationSyntax == null)
            {
                Log.LogWarning("Method {0} must be declared within a class. Skipping invocation.", methodSymbol.Name);
                continue;
            }

            // Validate method is not generic
            if (methodSymbol.IsGenericMethod)
            {
                AddDiagnostic(
                    DiagnosticDescriptors.GenericMethodNotSupported,
                    methodSymbol,
                    [methodSymbol.Name]
                );

                Log.LogError("Method {0} cannot be generic. CompileTime does not support generic methods.", methodSymbol.Name);
                continue;
            }

            // Create unique key for deduplication based on method signature and parameters
            var methodKey = $"{methodSymbol.ContainingType.ContainingNamespace.ToDisplayString()}.{methodSymbol.ContainingType.Name}.{methodSymbol.Name}({string.Join(",", invocation.Parameters)})";

            // Generate short hash for compact cache keys
            var cacheKey = CompileTimeHelper.GenerateShortHash(methodKey);
            var methodContentHash = CompileTimeHelper.GetMethodContentHash(methodSymbol);

            // Check cache strategy
            var cacheStrategy = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.Cache), new CompileTimeAttribute().Cache);

            // Add to calls tracking
            if (!callsForRequest.ContainsKey(methodKey)) { callsForRequest[methodKey] = []; }
            callsForRequest[methodKey].Add(invocation);

            // Create unique request only once (but check cache first)
            if (!uniqueRequests.ContainsKey(methodKey))
            {
                // Try cache first
                var cacheEntryExists = _cacheManager.Entries.TryGetValue(cacheKey, out var entry);

                if (cacheEntryExists && entry!.Success)
                {
                    // Validate cache entry
                    var currentReturnType = methodSymbol.ReturnType.ToDisplayString();
                    var contentHashMatch = entry.MethodContentHash == methodContentHash;
                    var returnTypeMatch = entry.ValueType == currentReturnType;

                    if (contentHashMatch && returnTypeMatch)
                    {
                        Log.LogMessage(MessageImportance.High, "[CACHE HIT] Using cached result for {0}", methodSymbol.Name);

                        // Create a response from cache for interceptor generation
                        cacheHits[invocation.InvocationId] = new CompileTimeResponse
                        {
                            InvocationId = invocation.InvocationId,
                            Success = entry.Success,
                            SerializedValue = entry.SerializedValue,
                            ErrorMessage = entry.ErrorMessage,
                            ErrorCode = entry.ErrorCode,
                            ExecutionTimeMs = entry.ExecutionTimeMs,
                            MemoryFootprintBytes = entry.MemoryFootprintBytes,
                        };

                        // Add to performance collector
                        var suppressWarnings = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.SuppressWarnings), new CompileTimeAttribute().SuppressWarnings);
                        var parametersTxt = string.Join(",", invocation.Parameters);

                        _performanceCollector?.Add(new()
                        {
                            ClassName = methodSymbol.ContainingType.Name,
                            MethodName = methodSymbol.Name,
                            Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
                            Cache = cacheStrategy,
                            ThresholdMs = performanceThreshold,
                            WasSuppressed = suppressWarnings,
                            Parameters = parametersTxt,
                            ExecutionTimeMs = 0, // Cache hit = 0ms execution time
                            MemoryFootprintBytes = entry.MemoryFootprintBytes,
                            ErrorMessage = entry.ErrorMessage,
                            ThresholdExceeded = false // Cache hits never exceed threshold
                        });

                        continue;
                    }
                    else
                    {
                        // Cache invalidation
                        if (!contentHashMatch)
                        {
                            Log.LogMessage(MessageImportance.High, "[CACHE INVALIDATED] Method body changed for {0}", methodSymbol.Name);
                        }
                        if (!returnTypeMatch)
                        {
                            Log.LogMessage(MessageImportance.High, "[CACHE INVALIDATED] Return type changed for {0} (was: {1}, now: {2})", methodSymbol.Name, entry.ValueType, currentReturnType);
                        }
                    }
                }

                Log.LogMessage(MessageImportance.High, "[CACHE MISS] Will execute method: {0}", methodSymbol.Name);

                uniqueRequests[methodKey] = new()
                {
                    ClassCode = classDeclarationSyntax.ToFullString(),
                    Namespace = methodSymbol.ContainingType.ContainingNamespace.ToDisplayString(),
                    TypeName = methodSymbol.ContainingType.Name,
                    MethodName = methodSymbol.Name,
                    MethodParameterTypeNames = [.. methodSymbol.Parameters.Select(p => p.Type.ToDisplayString(_fullClrFormat))],
                    Parameters = invocation.Parameters,
                    TimeoutMs = timeoutMs == -1 ? TimeoutMs : timeoutMs,
                    ReturnTypeFullName = methodSymbol.ReturnType.ToDisplayString(_fullClrFormat),
                    InvocationId = invocation.InvocationId
                };
            }
        }

        request.Methods.AddRange(uniqueRequests.Values);

        if (request.Methods.Count == 0)
        {
            Log.LogMessage("No valid requests to send to generator.");
            return generatedFiles;
        }

        request.GlobalReferencePaths = [.. References.Select(r => r.ItemSpec).Where(p => !string.IsNullOrEmpty(p))];
        request.ProjectType = DetectProjectType();

        // Execute Generator via stdin/stdout
        var responses = new List<CompileTimeResponse>();

        if (!File.Exists(GeneratorAssemblyPath))
        {
            throw new FileNotFoundException($"Generator assembly not found at '{GeneratorAssemblyPath}'. Please check the 'GeneratorAssemblyPath' property in your project file.");
        }

        // Serialize request to JSON (formatted for readability - minimal performance impact)
        var requestJson = JsonSerializer.Serialize(request, jsonSerializerOptions);

        if (IsVerboseLogging)
        {
            Log.LogMessage(MessageImportance.High, "Executing generator via file: \"{0}\"", GeneratorAssemblyPath);
        }

        var requestFile = Path.Combine(OutputPath, "debug_request.json");
        var responseFile = Path.Combine(OutputPath, "debug_response.json");

        try
        {
            await File.WriteAllTextAsync(requestFile, requestJson, cancellationToken);

            // Use .exe directly if it exists, otherwise use dotnet + dll
            var exePath = GeneratorAssemblyPath.Replace(".dll", ".exe");
            var fileName = File.Exists(exePath) ? exePath : "dotnet";
            var arguments = File.Exists(exePath) ? $"\"{requestFile}\"" : $"\"{GeneratorAssemblyPath}\" \"{requestFile}\"";

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new()
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            process.WaitForExit();

            // Log all output for debugging
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                Log.LogMessage(MessageImportance.High, "[Generator output]\n{0}", stdout);
            }
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Log.LogMessage(MessageImportance.High, "[Generator stderr]\n{0}", stderr);
            }

            if (process.ExitCode != 0)
            {
                Log.LogError("Generator process exited with code {0}.", process.ExitCode);
                throw new InvalidOperationException($"Generator process failed with exit code {process.ExitCode}");
            }

            // Read response from file
            if (!File.Exists(responseFile))
            {
                throw new FileNotFoundException($"Generator did not produce response file: {responseFile}");
            }

            var responseJson = await File.ReadAllTextAsync(responseFile, cancellationToken);

            responses = JsonSerializer.Deserialize<List<CompileTimeResponse>>(responseJson) ?? [];
        }
        catch (Exception ex)
        {
            Log.LogError("Failed to execute generator: {0}", ex.Message);
            throw;
        }
        finally
        {
            // Files are kept for debugging (debug_request.json and debug_response.json)
            // They get overwritten on next build, no cleanup needed
        }

        // Combine responses from generator with cache hits
        var responseMap = responses.ToDictionary(a => a.InvocationId, a => a);
        foreach (var cacheHit in cacheHits)
        {
            responseMap[cacheHit.Key] = cacheHit.Value;
        }

        // Cache new results
        foreach (var response in responses)
        {
            var invocation = invocations.FirstOrDefault(i => i.InvocationId == response.InvocationId);
            if (invocation != null)
            {
                var methodSymbol = invocation.MethodSymbol;
                var attr = CompileTimeHelper.GetAttributes(methodSymbol);
                var cacheStrategy = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.Cache), new CompileTimeAttribute().Cache);

                if (cacheStrategy != CacheStrategy.Never && response.Success)
                {
                    var methodKey = $"{methodSymbol.ContainingType.ContainingNamespace.ToDisplayString()}.{methodSymbol.ContainingType.Name}.{methodSymbol.Name}({string.Join(",", invocation.Parameters)})";
                    var cacheKey = CompileTimeHelper.GenerateShortHash(methodKey);
                    var methodContentHash = CompileTimeHelper.GetMethodContentHash(methodSymbol);

                    _cacheManager.Set(cacheKey, new()
                    {
                        MethodContentHash = methodContentHash,
                        Success = response.Success,
                        SerializedValue = response.SerializedValue,
                        ValueType = methodSymbol.ReturnType.ToDisplayString(),
                        ErrorMessage = response.ErrorMessage,
                        ErrorCode = response.ErrorCode,
                        CachedAt = DateTime.UtcNow,
                        ExecutionTimeMs = response.ExecutionTimeMs,
                        Persistent = cacheStrategy == CacheStrategy.Persistent,
                        MemoryFootprintBytes = response.MemoryFootprintBytes
                    });
                }
            }
        }

        // Group invocations by class for separate file generation
        var invocationsByClass = invocations.GroupBy(a => new
        {
            ClassName = a.MethodSymbol.ContainingType.Name,
            Namespace = a.MethodSymbol.ContainingType.ContainingNamespace.ToDisplayString() ?? string.Empty
        }).ToList();

        foreach (var classGroup in invocationsByClass)
        {
            var className = classGroup.Key.ClassName;
            var namespaceName = classGroup.Key.Namespace;

            var safeClassName = CompileTimeHelper.GenerateSafeClassName(namespaceName, className);
            var interceptorFilePath = Path.Combine(OutputPath, $"{safeClassName}.g.cs");

            var interceptorContent = GenerateInterceptorClass(classGroup, methods, responseMap, callsForRequest);

            await File.WriteAllTextAsync(interceptorFilePath, interceptorContent, cancellationToken);
            generatedFiles.Add(new TaskItem(interceptorFilePath));

            Log.LogMessage("Generated: {0}", interceptorFilePath);
        }

        return generatedFiles;
    }

    private string GenerateInterceptorClass(IGrouping<dynamic, InvocationInfo> classGroup,
                                            List<IMethodSymbol> methods,
                                            Dictionary<string, CompileTimeResponse> responseMap,
                                            Dictionary<string, List<InvocationInfo>> callsForRequest)
    {
        var className = (string)classGroup.Key.ClassName;
        var namespaceName = (string)classGroup.Key.Namespace;

        var displayName = string.IsNullOrEmpty(namespaceName)
                            ? $"global::{className}"
                            : $"{namespaceName}.{className}";

        var sb = new StringBuilder();

        // Generate file header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by CompileTime MSBuild Task - Method Call Interceptors");
        sb.AppendLine("// https://github.com/Corsinvest/dotnet-fx/tree/master/src/CompileTime");
        sb.AppendLine($"// Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("#pragma warning disable CS9270");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();

        // Use fixed CompileTime namespace
        sb.AppendLine("namespace CompileTime");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Contains compile-time interceptors for {displayName} methods.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static class {CompileTimeHelper.GenerateSafeClassName(namespaceName, className)}");
        sb.AppendLine("    {");

        // Get relevant calls for this class
        var classCallsForRequest = callsForRequest.Where(kvp =>
        {
            var firstCall = kvp.Value.FirstOrDefault();
            return firstCall != null &&
                   firstCall.MethodSymbol.ContainingType.Name == className &&
                   firstCall.MethodSymbol.ContainingType.ContainingNamespace.ToDisplayString() == namespaceName;
        }).ToList();

        foreach (var callGroup in classCallsForRequest)
        {
            var methodKey = callGroup.Key;
            var calls = callGroup.Value;
            var firstCall = calls.First();

            // Use original method name (not GUID)
            var interceptorName = firstCall.MethodSymbol.Name;

            // Add parameter suffix for overloaded methods
            var parameterSignature = string.Join(",", firstCall.Parameters.Select(p => p.ToString()));
            if (!string.IsNullOrEmpty(parameterSignature))
            {
                interceptorName += $"_{Math.Abs(parameterSignature.GetHashCode()):X8}";
            }

            // Generate multiple InterceptsLocation attributes for same method
            foreach (var call in calls)
            {
                sb.AppendLine(GenerateInterceptsLocationAttribute(call));
            }

            // Generate interceptor method with original name and signature
            var originalMethod = methods.FirstOrDefault(m => m.Name == firstCall.MethodSymbol.Name);
            if (originalMethod != null)
            {
                var parameterSignatureForMethod = originalMethod.Parameters.Length > 0
                                            ? string.Join(", ", originalMethod.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))
                                            : string.Empty;

                var returnType = firstCall.MethodSymbol.ReturnType.ToDisplayString();
                sb.AppendLine($"        public static {returnType} {interceptorName}({parameterSignatureForMethod})");
                sb.AppendLine("        {");

                if (!responseMap.TryGetValue(firstCall.InvocationId, out var response))
                {
                    Log.LogError("Response not found for method {0}. Skipping interceptor generation for this method.", methodKey);
                    sb.AppendLine($"            // ERROR: Response not found for {methodKey}");
                    sb.AppendLine("            throw new InvalidOperationException(\"Compile-time method execution failed.\");");
                    sb.AppendLine("        }");
                    sb.AppendLine();
                    continue; // Skip this interceptor if response is missing
                }
                var executedValue = response.SerializedValue;
                ProcessResponse(response, firstCall);

                // Check if method returns void or plain Task (executed for side effects only)
                if (returnType == "void")
                {
                    // void methods - no return needed
                }
                else if (returnType == "System.Threading.Tasks.Task")
                {
                    sb.AppendLine("            return System.Threading.Tasks.Task.CompletedTask;");
                }
                else if (returnType.StartsWith("System.Threading.Tasks.Task<"))
                {
                    // Task<T> methods - wrap the result in Task.FromResult
                    var innerType = returnType.Substring("System.Threading.Tasks.Task<".Length).TrimEnd('>');
                    if (!string.IsNullOrEmpty(executedValue) && !executedValue!.StartsWith("/*") && executedValue != "null")
                    {
                        sb.AppendLine($"            return System.Threading.Tasks.Task.FromResult({executedValue});");
                    }
                    else
                    {
                        sb.AppendLine($"            return System.Threading.Tasks.Task.FromResult(default({innerType}));");
                    }
                }
                else
                {
                    // Normal methods - return the value directly
                    sb.AppendLine($"            return {executedValue};");
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("#pragma warning restore CS9270");

        return sb.ToString();
    }

    private ProjectType DetectProjectType()
    {
        var csproj = Directory.GetFiles(ProjectDir, "*.csproj").FirstOrDefault();
        if (csproj is null)
        {
            Log.LogWarning("No .csproj found in {0}. Using Default.", ProjectDir);
            return ProjectType.Default;
        }

        try
        {
            var xml = XDocument.Load(csproj);
            var project = xml.Root;

            // Detect Sdk attribute
            var sdkAttr = project?.Attribute("Sdk")?.Value ?? string.Empty;

            if (sdkAttr.Contains("Microsoft.NET.Sdk.Web"))
            {
                return ProjectType.AspNetCore;
            }
            else if (sdkAttr.Contains("Microsoft.NET.Sdk.Worker"))
            {
                return ProjectType.Worker;
            }
            else if (sdkAttr.Contains("Microsoft.NET.Sdk.WindowsDesktop"))
            {
                var props = project!.Descendants("PropertyGroup");

                if (props.Elements("UseWindowsForms").Any(x => x.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    return ProjectType.WinForms;
                }
                else if (props.Elements("UseWPF").Any(x => x.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
                {

                    return ProjectType.WPF;
                }
            }

            return ProjectType.Default;
        }
        catch (Exception ex)
        {
            Log.LogWarning("Error parsing {0}: {1}. Defaulting.", csproj, ex.Message);
            return ProjectType.Default;
        }
    }

    private void AddDiagnostic(DiagnosticDescriptor descriptor, IMethodSymbol methodSymbol, object[] messageArgs)
    {
        var location = methodSymbol.Locations.FirstOrDefault();
        if (location?.SourceTree == null) { return; }

        var filePath = location.SourceTree.FilePath;

        // For MSBuild warnings, we need the full path - construct it from ProjectDir if it's just a filename
        var fullFilePath = filePath;
        if (!Path.IsPathRooted(filePath)) { fullFilePath = Path.Combine(ProjectDir, filePath); }

        _diagnostics.Add(new()
        {
            Id = descriptor.Id,
            MessageArgs = messageArgs,
            FilePath = fullFilePath,
            StartPosition = location.SourceSpan.Start,
            Length = location.SourceSpan.Length
        });

        // Emit critical errors directly as MSBuild errors (dual-mode approach)
        if (IsCriticalError(descriptor.Id))
        {
            var lineInfo = location.GetLineSpan();
            var line = lineInfo.StartLinePosition.Line + 1;
            var column = lineInfo.StartLinePosition.Character + 1;

            var formattedMessage = string.Format(descriptor.MessageFormat.ToString(), messageArgs);
            Log.LogError(
                subcategory: "CompileTime",
                errorCode: descriptor.Id,
                helpKeyword: descriptor.HelpLinkUri,
                file: fullFilePath,
                lineNumber: line,
                columnNumber: column,
                endLineNumber: 0,
                endColumnNumber: 0,
                message: formattedMessage
            );
        }
    }

    private static bool IsCriticalError(string diagnosticId) =>
        // Only emit these as MSBuild errors (will fail the build)
        // All other diagnostics are shown by the Analyzer
        diagnosticId == DiagnosticDescriptors.MethodMustBeStatic.Id ||
               diagnosticId == DiagnosticDescriptors.MethodContainingTypeNotFound.Id ||
               diagnosticId == DiagnosticDescriptors.ExecutionError.Id ||
               diagnosticId == DiagnosticDescriptors.MethodExecutionTimeoutError.Id;

    private async System.Threading.Tasks.Task SaveDiagnosticsAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_diagnostics.Count == 0) { return; }

            var diagnosticsFile = Path.Combine(ProjectDir, "obj", "CompileTimeDiagnostics.json");
            var directory = Path.GetDirectoryName(diagnosticsFile);

            if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }

            var json = JsonSerializer.Serialize(_diagnostics, jsonSerializerOptions);
            await File.WriteAllTextAsync(diagnosticsFile, json, cancellationToken);
            Log.LogMessage(MessageImportance.Low, "Saved {0} diagnostics to {1}", _diagnostics.Count, diagnosticsFile);
        }
        catch (Exception ex)
        {
            Log.LogWarning("Failed to save diagnostics: {0}", ex.Message);
        }
    }

    private string GenerateInterceptsLocationAttribute(InvocationInfo call)
    {
        // Prefer modern format when Roslyn API is available (detected via reflection)
        // Modern format: version/data with xxHash128 checksum + byte offset
        if (!string.IsNullOrEmpty(call.InterceptableData))
        {
            return $"""        [global::System.Runtime.CompilerServices.InterceptsLocation({call.InterceptableVersion}, "{call.InterceptableData}")]""";
        }

        // Fallback to legacy format (filePath, line, character) when API not available
        // This format is deprecated but still works in .NET 9
        var relativePath = GetRelativePathFromGenerated(call.FilePath);
        return $"""        [global::System.Runtime.CompilerServices.InterceptsLocation(@"{relativePath}", {call.Line}, {call.Character})]""";
    }

    private string GetRelativePathFromGenerated(string filePath)
    {
        try
        {
            // Normalize paths to avoid issues with different path separators
            var generatedDir = Path.GetFullPath(OutputPath).Replace('\\', '/');
            var sourceFile = Path.GetFullPath(filePath).Replace('\\', '/');

            if (IsVerboseLogging)
            {
                Log.LogMessage(MessageImportance.Low, $"GetRelativePathFromGenerated: generatedDir = {generatedDir}");
                Log.LogMessage(MessageImportance.Low, $"GetRelativePathFromGenerated: sourceFile = {sourceFile}");
            }

            // Use URI approach but ensure proper directory handling
            if (!generatedDir.EndsWith("/")) { generatedDir += "/"; }

            var relativeUri = new Uri(generatedDir).MakeRelativeUri(new Uri(sourceFile));
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (IsVerboseLogging)
            {
                Log.LogMessage(MessageImportance.Low, $"GetRelativePathFromGenerated: relativePath = {relativePath}");
            }

            // Ensure forward slashes for interceptor attributes
            return relativePath.Replace('\\', '/');
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Could not convert path {filePath} to relative: {ex.Message}. Using absolute path.");
            // Return the absolute path as fallback - interceptors can handle absolute paths
            return filePath.Replace('\\', '/');
        }
    }

    private void ProcessResponse(CompileTimeResponse response, InvocationInfo invocation)
    {
        var attr = CompileTimeHelper.GetAttributes(invocation.MethodSymbol);
        var performanceThreshold = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.PerformanceWarningThresholdMs), new CompileTimeAttribute().PerformanceWarningThresholdMs);
        var suppressWarnings = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.SuppressWarnings), new CompileTimeAttribute().SuppressWarnings);
        var cacheStrategy = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.Cache), new CompileTimeAttribute().Cache);
        var timeoutMs = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.TimeoutMs), TimeoutMs); // Use task's TimeoutMs as default

        var parametersTxt = string.Join(",", invocation.Parameters);

        if (response.Success)
        {
            if (response.ExecutionTimeMs > 0
                && !suppressWarnings && performanceThreshold > 0
                && response.ExecutionTimeMs > performanceThreshold)
            {
                AddDiagnostic(DiagnosticDescriptors.SlowMethodExecution, invocation.MethodSymbol, [invocation.MethodSymbol.Name, response.ExecutionTimeMs.ToString(), performanceThreshold.ToString()]);
                Log.LogMessage(MessageImportance.High, "Performance warning: Method '{0}' took {1}ms (threshold: {2}ms)", invocation.MethodSymbol.Name, response.ExecutionTimeMs, performanceThreshold);
            }
        }
        else
        {
            if (response.ErrorCode == DiagnosticDescriptors.ExecutionTimeout.Id) // Timeout
            {
                var actualTimeoutMs = timeoutMs == -1 ? TimeoutMs : timeoutMs;
                switch (TimeoutBehavior.ToLowerInvariant())
                {
                    case "skip": AddDiagnostic(DiagnosticDescriptors.MethodExecutionSkippedTimeout, invocation.MethodSymbol, [invocation.MethodSymbol.Name, actualTimeoutMs.ToString()]); break;
                    case "error":
                    case "fail": AddDiagnostic(DiagnosticDescriptors.MethodExecutionTimeoutError, invocation.MethodSymbol, [invocation.MethodSymbol.Name, actualTimeoutMs.ToString()]); break;
                    case "warning":
                    case "warn": AddDiagnostic(DiagnosticDescriptors.MethodExecutionTimeoutWarning, invocation.MethodSymbol, [invocation.MethodSymbol.Name, actualTimeoutMs.ToString()]); break;
                    default: AddDiagnostic(DiagnosticDescriptors.UnknownTimeoutBehavior, invocation.MethodSymbol, [TimeoutBehavior, invocation.MethodSymbol.Name]); break;
                }
            }
            else
            {
                AddDiagnostic(DiagnosticDescriptors.ExecutionError, invocation.MethodSymbol, [response.ErrorMessage ?? "Unknown error"]);
            }
        }

        _performanceCollector?.Add(new()
        {
            ClassName = invocation.MethodSymbol.ContainingType.Name,
            MethodName = invocation.MethodSymbol.Name,
            Namespace = invocation.MethodSymbol.ContainingNamespace.ToDisplayString(),
            ExecutionTimeMs = response.ExecutionTimeMs,
            ThresholdMs = performanceThreshold,
            Cache = cacheStrategy,
            ThresholdExceeded = response.ExecutionTimeMs > performanceThreshold,
            WasSuppressed = suppressWarnings,
            ErrorMessage = response.ErrorMessage,
            Parameters = parametersTxt,
            MemoryFootprintBytes = response.MemoryFootprintBytes
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // Clear references to compilations and large objects
            _compilation = null;

            // Dispose performance collector if it implements IDisposable
            if (_performanceCollector is IDisposable disposableCollector)
            {
                disposableCollector.Dispose();
            }
            _performanceCollector = null;

            // Clear diagnostic list
            _diagnostics.Clear();

            Log.LogMessage(MessageImportance.Low, "CompileTimeTask resources disposed.");

            _disposed = true;
        }
    }

    private static string GetSourceInterceptsLocationAttribute()
     => """
                #nullable enable

                namespace System.Runtime.CompilerServices
                {
                    // InterceptsLocationAttribute for .NET 9+ interceptor support
                    // Supports both legacy (filePath, line, character) and modern (version, data) formats
                    // See: https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md
                    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
                    internal sealed class InterceptsLocationAttribute : Attribute
                    {
                        // Legacy constructor for compatibility (deprecated but still works)
                        public InterceptsLocationAttribute(string filePath, int line, int character)
                        {
                            FilePath = filePath;
                            Line = line;
                            Character = character;
                        }

                        // Modern constructor for .NET 9+ (version/data format)
                        public InterceptsLocationAttribute(int version, string data)
                        {
                            Version = version;
                            Data = data;
                        }

                        public string? FilePath { get; }
                        public int Line { get; }
                        public int Character { get; }
                        public int Version { get; }
                        public string? Data { get; }
                    }
                }

                #nullable restore
                """;
}
