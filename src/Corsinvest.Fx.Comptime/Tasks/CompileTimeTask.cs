// using Microsoft.Build.Framework;
// using Microsoft.Build.Utilities;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Corsinvest.CompileTime.Models;
// using Corsinvest.CompileTime.Helpers;
// using System.Text;
// using Corsinvest.CompileTime.Caching;
// using Corsinvest.CompileTime.Performance;
// using Corsinvest.CompileTime.Diagnostics;
// using System.Text.Json;
// using System.ComponentModel.DataAnnotations;

// namespace Corsinvest.Fx.Comptime.Tasks;

// public class CompileTimeTask : Microsoft.Build.Utilities.Task, IDisposable
// {
//     private readonly List<TaskDiagnosticData> _diagnostics = [];
//     private readonly CacheManager _cacheManager = new();
//     private PerformanceCollector? _performanceCollector;
//     private CSharpCompilation? _compilation;
//     private bool _disposed;

//     [Required] public ITaskItem[] SourceFiles { get; set; } = [];
//     [Required] public string ProjectDir { get; set; } = "";
//     public ITaskItem[] References { get; set; } = [];
//     public string OutputPath { get; set; } = "";
//     public int TimeoutMs { get; set; } = 5000;
//     public string TimeoutBehavior { get; set; } = "Skip";
//     public bool CompileTimeEnabled { get; set; } = true;
//     public bool GenerateReport { get; set; }
//     [Required] public string GeneratorAssemblyPath { get; set; } = "";
//     [Output] public ITaskItem[] GeneratedFiles { get; set; } = [];

//     private enum NetVersion
//     {
//         Net8AndBelow,
//         Net9Plus
//     }

//     private static readonly SymbolDisplayFormat FullClrFormat = new(
//         globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
//         typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
//         genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
//         miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
//     );

//     public override bool Execute()
//     {
//         _cacheManager.SetProjectDir(ProjectDir);
//         _cacheManager.Load();

//         if (GenerateReport) { _performanceCollector = new("Unknown", ProjectDir); }

//         try
//         {
//             Log.LogMessage(MessageImportance.High, "🔄 COMPILETIME TASK STARTING! 🔄");
//             Log.LogMessage(MessageImportance.High, $"  - ProjectDir: {ProjectDir}");
//             Log.LogMessage(MessageImportance.High, $"  - CompileTimeEnabled: {CompileTimeEnabled}");
//             Log.LogMessage(MessageImportance.High, $"  - OutputPath: {OutputPath}");
//             Log.LogMessage(MessageImportance.High, $"  - SourceFiles count: {SourceFiles?.Length ?? 0}");

//             if (!CompileTimeEnabled)
//             {
//                 Log.LogMessage(MessageImportance.High, "❌ CompileTime execution is DISABLED");
//                 return true;
//             }

//             // 1. Create compilation once (reads files only once)
//             _compilation = CreateCompilation();
//             if (_compilation == null) { return true; }

//             // 2. Analyze compilation for CompileTime methods
//             var compileTimeMethods = AnalyzeForCompileTimeMethods(_compilation);
//             Log.LogMessage(MessageImportance.High, $"Found {compileTimeMethods.Count} CompileTime methods");

//             // 3. Find method invocations using same compilation
//             var invocations = FindMethodInvocations(_compilation);
//             Log.LogMessage(MessageImportance.High, $"Found {invocations.Count} method invocations");

//             // 3. Esegui i metodi e genera interceptors
//             var generatedFiles = GenerateInterceptors(compileTimeMethods, invocations);

//             GeneratedFiles = [.. generatedFiles];

//             Log.LogMessage(MessageImportance.High, $"✅ Generated {GeneratedFiles.Length} files");
//             return true;
//         }
//         catch (Exception ex)
//         {
//             Log.LogError($"❌ CompileTime execution failed: {ex.Message}");
//             Log.LogError($"Stack trace: {ex.StackTrace}");
//             return false; // Blocca il build
//         }
//         finally
//         {
//             try
//             {
//                 _cacheManager.Save();
//                 Log.LogMessage(MessageImportance.Low, "Cache saved.");

//                 if (GenerateReport)
//                 {
//                     _performanceCollector!.StopTracking();
//                     _performanceCollector.GenerateReportContent(_cacheManager.FileName);
//                     Log.LogMessage(MessageImportance.High, $"Performance report generated at {_performanceCollector.FileName}");
//                 }

//                 // Save diagnostics for CompileTimeDiagnosticAnalyzer
//                 SaveDiagnostics();

//                 // Dispose resources properly
//                 Dispose();

//                 // Force cleanup to prevent hanging MSBuild processes
//                 GC.Collect();
//                 GC.WaitForPendingFinalizers();
//                 GC.Collect();

//                 Log.LogMessage(MessageImportance.Low, "CompileTimeTask cleanup completed.");
//             }
//             catch (Exception ex)
//             {
//                 Log.LogWarning($"Error during cleanup: {ex.Message}");
//             }
//         }
//     }

//     private CSharpCompilation? CreateCompilation()
//     {
//         Log.LogMessage("Creating compilation from source files...");

//         // Parse all source files once
//         var syntaxTrees = new List<SyntaxTree>();
//         foreach (var sourceFile in SourceFiles)
//         {
//             try
//             {
//                 using var fileStream = File.OpenRead(sourceFile.ItemSpec);
//                 var content = new StreamReader(fileStream).ReadToEnd();
//                 var tree = CSharpSyntaxTree.ParseText(content, path: sourceFile.ItemSpec);
//                 syntaxTrees.Add(tree);
//             }
//             catch (Exception ex)
//             {
//                 Log.LogWarning($"Could not parse {sourceFile.ItemSpec}: {ex.Message}");
//             }
//         }

//         if (syntaxTrees.Count == 0)
//         {
//             Log.LogMessage("No valid syntax trees found");
//             return null;
//         }

//         // Create compilation with references (using using statements for better disposal)
//         var metadataReferences = new List<MetadataReference>();
//         foreach (var reference in References)
//         {
//             try
//             {
//                 metadataReferences.Add(MetadataReference.CreateFromFile(reference.ItemSpec));
//             }
//             catch (Exception ex)
//             {
//                 Log.LogWarning($"Could not load reference {reference.ItemSpec}: {ex.Message}");
//             }
//         }


//         return CSharpCompilation.Create("CompileTimeAnalysis")
//                                 .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
//                                 .AddReferences(metadataReferences)
//                                 .AddSyntaxTrees(syntaxTrees);
//     }

//     private List<IMethodSymbol> AnalyzeForCompileTimeMethods(CSharpCompilation compilation)
//     {
//         Log.LogMessage("Analyzing compilation for [CompileTime] methods...");

//         var methods = new List<IMethodSymbol>();

//         // Analyze each syntax tree for CompileTime methods
//         foreach (var tree in compilation.SyntaxTrees)
//         {
//             var semanticModel = compilation.GetSemanticModel(tree);
//             var root = tree.GetRoot();

//             var methodNodes = root.DescendantNodes()
//                                   .OfType<MethodDeclarationSyntax>()
//                                   .Where(method => method.AttributeLists.Count > 0
//                                                     && method.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == "CompileTime")));

//             foreach (var methodNode in methodNodes)
//             {
//                 var methodSymbol = semanticModel.GetDeclaredSymbol(methodNode);
//                 if (methodSymbol != null && CompileTimeHelper.IsValid(methodSymbol))
//                 {
//                     methods.Add(methodSymbol);
//                     Log.LogMessage($"Found CompileTime method: {methodSymbol.ContainingType.Name}.{methodSymbol.Name}");
//                 }
//             }
//         }

//         return methods;
//     }

//     private List<InvocationInfo> FindMethodInvocations(CSharpCompilation compilation)
//     {
//         Log.LogMessage("Finding method invocations...");

//         var invocations = new List<InvocationInfo>();

//         // Find invocations of CompileTime methods
//         foreach (var tree in compilation.SyntaxTrees)
//         {
//             var semanticModel = compilation.GetSemanticModel(tree);
//             var root = tree.GetRoot();

//             var invocationNodes = root.DescendantNodes()
//                                       .OfType<InvocationExpressionSyntax>()
//                                       .Where(a => a.Expression is MemberAccessExpressionSyntax memberAccess
//                                                     && memberAccess.Expression is IdentifierNameSyntax);

//             foreach (var invocationNode in invocationNodes)
//             {
//                 var invocationInfo = InvocationInfo.Get(invocationNode, semanticModel);
//                 if (invocationInfo != null)
//                 {
//                     invocations.Add(invocationInfo);
//                     Log.LogMessage($"Found invocation: {invocationInfo.MethodSymbol.ContainingType.Name}.{invocationInfo.MethodSymbol.Name}");
//                 }
//             }
//         }

//         return invocations;
//     }

//     private List<ITaskItem> GenerateInterceptors(List<IMethodSymbol> methods, List<InvocationInfo> invocations)
//     {
//         Log.LogMessage("Generating interceptor files...");

//         var generatedFiles = new List<ITaskItem>();

//         // Generate InterceptsLocationAttribute first
//         var attributeFilePath = Path.Combine(OutputPath, "InterceptsLocationAttribute.g.cs");
//         Directory.CreateDirectory(Path.GetDirectoryName(attributeFilePath)!);

//         File.WriteAllText(attributeFilePath, GenerateInterceptsLocationAttribute());
//         generatedFiles.Add(new TaskItem(attributeFilePath));

//         Log.LogMessage($"Generated: {attributeFilePath}");

//         // If no invocations found, only generate the attribute
//         if (invocations.Count == 0)
//         {
//             Log.LogMessage("No invocations found, skipping interceptor generation");
//             return generatedFiles;
//         }

//         // create request - deduplicate requests but keep track of all calls
//         var request = new CompileTimeRequest();
//         var uniqueRequests = new Dictionary<string, CompileTimeRequest.Item>();
//         var callsForRequest = new Dictionary<string, List<InvocationInfo>>();
//         var cacheHits = new Dictionary<string, CompileTimeResponse>();

//         foreach (var invocation in invocations)
//         {
//             var methodSymbol = invocation.MethodSymbol;
//             var attr = CompileTimeHelper.GetAttributes(methodSymbol);
//             var timeoutMs = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.TimeoutMs), TimeoutMs);

//             var syntaxNode = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
//             if (syntaxNode == null)
//             {
//                 Log.LogWarning($"Could not find syntax node for method symbol {methodSymbol.Name}. Skipping invocation.");
//                 continue;
//             }

//             var classDeclarationSyntax = syntaxNode.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
//             if (classDeclarationSyntax == null)
//             {
//                 Log.LogWarning($"Method {methodSymbol.Name} must be declared within a class. Skipping invocation.");
//                 continue;
//             }

//             // Create unique key for deduplication based on method signature and parameters
//             var methodKey = $"{methodSymbol.ContainingType.ContainingNamespace.ToDisplayString()}.{methodSymbol.ContainingType.Name}.{methodSymbol.Name}({string.Join(",", invocation.Parameters)})";

//             // Generate cache key
//             var cacheKey = CompileTimeHelper.GenerateShortHash(methodKey);
//             var methodContentHash = CompileTimeHelper.GetMethodContentHash(methodSymbol);

//             // Check cache strategy
//             var cacheStrategy = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.Cache), new CompileTimeAttribute().Cache);

//             // Add to calls tracking
//             if (!callsForRequest.ContainsKey(methodKey)) { callsForRequest[methodKey] = []; }
//             callsForRequest[methodKey].Add(invocation);

//             // Create unique request only once (but check cache first)
//             if (!uniqueRequests.ContainsKey(methodKey))
//             {
//                 // Try cache first
//                 if (_cacheManager.Entries.TryGetValue(cacheKey, out var entry)
//                         && entry.MethodContentHash == methodContentHash
//                         && entry.Success)
//                 {
//                     Log.LogMessage(MessageImportance.High, $"[CACHE HIT] Using cached result for {methodSymbol.Name}");

//                     // Create a response from cache for interceptor generation
//                     cacheHits[invocation.InvocationId] = new CompileTimeResponse
//                     {
//                         InvocationId = invocation.InvocationId,
//                         Success = entry.Success,
//                         SerializedValue = entry.SerializedValue,
//                         ErrorMessage = entry.ErrorMessage,
//                         ErrorCode = entry.ErrorCode,
//                         ExecutionTimeMs = entry.ExecutionTimeMs,
//                         MemoryFootprintBytes = entry.MemoryFootprintBytes,
//                     };

//                     // Add to performance collector
//                     var suppressWarnings = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.SuppressWarnings), new CompileTimeAttribute().SuppressWarnings);
//                     var performanceThreshold = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.PerformanceWarningThresholdMs), new CompileTimeAttribute().PerformanceWarningThresholdMs);
//                     var parametersTxt = string.Join(",", invocation.Parameters);

//                     _performanceCollector?.Add(new()
//                     {
//                         ClassName = methodSymbol.ContainingType.Name,
//                         MethodName = methodSymbol.Name,
//                         Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
//                         Cache = cacheStrategy,
//                         ThresholdMs = performanceThreshold,
//                         WasSuppressed = suppressWarnings,
//                         Parameters = parametersTxt,
//                         ExecutionTimeMs = 0, // Cache hit = 0ms execution time
//                         MemoryFootprintBytes = entry.MemoryFootprintBytes,
//                         ErrorMessage = entry.ErrorMessage,
//                         ThresholdExceeded = false // Cache hits never exceed threshold
//                     });

//                     continue;
//                 }

//                 Log.LogMessage(MessageImportance.High, $"[CACHE MISS] Will execute method: {methodSymbol.Name}");

//                 uniqueRequests[methodKey] = new()
//                 {
//                     ClassCode = classDeclarationSyntax.ToFullString(),
//                     Namespace = methodSymbol.ContainingType.ContainingNamespace.ToDisplayString(),
//                     TypeName = methodSymbol.ContainingType.Name,
//                     MethodName = methodSymbol.Name,
//                     MethodParameterTypeNames = [.. methodSymbol.Parameters.Select(p => p.Type.ToDisplayString(FullClrFormat))],
//                     Parameters = invocation.Parameters,
//                     TimeoutMs = timeoutMs == -1 ? TimeoutMs : timeoutMs,
//                     ReturnTypeFullName = methodSymbol.ReturnType.ToDisplayString(FullClrFormat),
//                     InvocationId = invocation.InvocationId
//                 };
//             }
//         }

//         request.Methods.AddRange(uniqueRequests.Values);

//         if (!request.Methods.Any())
//         {
//             Log.LogMessage("No valid requests to send to generator.");
//             return generatedFiles;
//         }

//         request.GlobalReferencePaths = [.. References.Select(r => r.ItemSpec).Where(p => !string.IsNullOrEmpty(p))];
//         request.ProjectType = DetectProjectType();

//         // Execute Generator once for all requests
//         var tempId = Guid.NewGuid().ToString("N");
//         var inputPath = Path.Combine(OutputPath, $"CompileTimeGeneratorRequests_{tempId}.json");
//         var outputPath = Path.Combine(OutputPath, $"CompileTimeGeneratorReponses_{tempId}.json");

//         Log.LogMessage(MessageImportance.High, $"Input path: {inputPath}");
//         Log.LogMessage(MessageImportance.High, $"Output path: {outputPath}");

//         var responses = new List<CompileTimeResponse>();

//         try
//         {
//             File.WriteAllText(inputPath, JsonSerializer.Serialize(request));

//             if (!File.Exists(GeneratorAssemblyPath))
//             {
//                 throw new FileNotFoundException($"Generator assembly not found at '{GeneratorAssemblyPath}'. Please check the 'GeneratorAssemblyPath' property in your project file.");
//             }

//             Log.LogMessage(MessageImportance.High, $"Executing generator... dotnet \"{GeneratorAssemblyPath}\" \"{inputPath}\" \"{outputPath}\"");

//             var process = new System.Diagnostics.Process
//             {
//                 StartInfo = new()
//                 {
//                     FileName = "dotnet",
//                     Arguments = $"\"{GeneratorAssemblyPath}\" \"{inputPath}\" \"{outputPath}\"",
//                     RedirectStandardOutput = true,
//                     RedirectStandardError = true,
//                     UseShellExecute = false,
//                     CreateNoWindow = true,
//                 }
//             };

//             process.Start();
//             var stdout = process.StandardOutput.ReadToEnd();
//             var stderr = process.StandardError.ReadToEnd();
//             process.WaitForExit();

//             if (process.ExitCode != 0)
//             {
//                 Log.LogError($"Generator process exited with code {process.ExitCode}.");
//                 if (!string.IsNullOrWhiteSpace(stdout)) { Log.LogMessage(MessageImportance.High, $"Generator stdout: {stdout}"); }
//                 if (!string.IsNullOrWhiteSpace(stderr)) { Log.LogError($"Generator stderr: {stderr}"); }

//                 throw new InvalidOperationException($"Generator process failed. See build log for details.");
//             }

//             responses = JsonSerializer.Deserialize<List<CompileTimeResponse>>(File.ReadAllText(outputPath)) ?? [];
//         }
//         finally
//         {
//             if (File.Exists(inputPath)) { File.Delete(inputPath); }
//             if (File.Exists(outputPath)) { File.Delete(outputPath); }
//         }

//         // Combine responses from generator with cache hits
//         var responseMap = responses.ToDictionary(a => a.InvocationId, a => a);
//         foreach (var cacheHit in cacheHits)
//         {
//             responseMap[cacheHit.Key] = cacheHit.Value;
//         }

//         // Cache new results
//         foreach (var response in responses)
//         {
//             var invocation = invocations.FirstOrDefault(i => i.InvocationId == response.InvocationId);
//             if (invocation != null)
//             {
//                 var methodSymbol = invocation.MethodSymbol;
//                 var attr = CompileTimeHelper.GetAttributes(methodSymbol);
//                 var cacheStrategy = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.Cache), new CompileTimeAttribute().Cache);

//                 if (cacheStrategy != CacheStrategy.Never && response.Success)
//                 {
//                     var methodKey = $"{methodSymbol.ContainingType.ContainingNamespace.ToDisplayString()}.{methodSymbol.ContainingType.Name}.{methodSymbol.Name}({string.Join(",", invocation.Parameters)})";
//                     var cacheKey = CompileTimeHelper.GenerateShortHash(methodKey);
//                     var methodContentHash = CompileTimeHelper.GetMethodContentHash(methodSymbol);

//                     _cacheManager.Set(cacheKey, new()
//                     {
//                         MethodContentHash = methodContentHash,
//                         Success = response.Success,
//                         SerializedValue = response.SerializedValue,
//                         ValueType = methodSymbol.ReturnType.ToDisplayString(),
//                         ErrorMessage = response.ErrorMessage,
//                         ErrorCode = response.ErrorCode,
//                         CachedAt = DateTime.UtcNow,
//                         ExecutionTimeMs = response.ExecutionTimeMs,
//                         Persistent = cacheStrategy == CacheStrategy.Persistent,
//                         MemoryFootprintBytes = response.MemoryFootprintBytes
//                     });
//                 }
//             }
//         }

//         // Group invocations by class for separate file generation
//         var invocationsByClass = invocations.GroupBy(a => new
//         {
//             ClassName = a.MethodSymbol.ContainingType.Name,
//             Namespace = a.MethodSymbol.ContainingType.ContainingNamespace.ToDisplayString() ?? ""
//         }).ToList();

//         foreach (var classGroup in invocationsByClass)
//         {
//             var className = classGroup.Key.ClassName;
//             var namespaceName = classGroup.Key.Namespace;

//             var safeClassName = CompileTimeHelper.GenerateSafeClassName(namespaceName, className);
//             var interceptorFilePath = Path.Combine(OutputPath, $"{safeClassName}.g.cs");

//             var interceptorContent = GenerateInterceptorClass(classGroup, methods, responseMap, callsForRequest);

//             File.WriteAllText(interceptorFilePath, interceptorContent);
//             generatedFiles.Add(new TaskItem(interceptorFilePath));

//             Log.LogMessage($"Generated: {interceptorFilePath}");
//         }

//         return generatedFiles;
//     }

//     private string GenerateInterceptorClass(IGrouping<dynamic, InvocationInfo> classGroup,
//                                             List<IMethodSymbol> methods,
//                                             Dictionary<string, CompileTimeResponse> responseMap,
//                                             Dictionary<string, List<InvocationInfo>> callsForRequest)
//     {
//         var className = (string)classGroup.Key.ClassName;
//         var namespaceName = (string)classGroup.Key.Namespace;

//         var displayName = string.IsNullOrEmpty(namespaceName)
//                             ? $"global::{className}"
//                             : $"{namespaceName}.{className}";

//         var sb = new StringBuilder();

//         // Generate file header
//         sb.AppendLine("// <auto-generated />");
//         sb.AppendLine("// Generated by CompileTime MSBuild Task - Method Call Interceptors");
//         sb.AppendLine("// https://github.com/Corsinvest/Corsinvest.CompileTime");
//         sb.AppendLine($"// Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
//         sb.AppendLine();
//         sb.AppendLine("#pragma warning disable CS9270");
//         sb.AppendLine("#nullable enable");
//         sb.AppendLine("using System;");
//         sb.AppendLine("using System.Collections.Generic;");
//         sb.AppendLine("using System.Runtime.CompilerServices;");
//         sb.AppendLine();

//         // Use fixed CompileTime namespace
//         sb.AppendLine("namespace CompileTime");
//         sb.AppendLine("{");
//         sb.AppendLine("    /// <summary>");
//         sb.AppendLine($"    /// Contains compile-time interceptors for {displayName} methods.");
//         sb.AppendLine("    /// </summary>");
//         sb.AppendLine($"    public static class {CompileTimeHelper.GenerateSafeClassName(namespaceName, className)}");
//         sb.AppendLine("    {");

//         // Detect target framework for interceptor syntax
//         var targetFramework = DetectTargetFramework();
//         sb.AppendLine($"// Target Framework: {targetFramework}");

//         // Get relevant calls for this class
//         var classCallsForRequest = callsForRequest.Where(kvp =>
//         {
//             var firstCall = kvp.Value.FirstOrDefault();
//             return firstCall != null &&
//                    firstCall.MethodSymbol.ContainingType.Name == className &&
//                    firstCall.MethodSymbol.ContainingType.ContainingNamespace.ToDisplayString() == namespaceName;
//         }).ToList();

//         foreach (var callGroup in classCallsForRequest)
//         {
//             var methodKey = callGroup.Key;
//             var calls = callGroup.Value;
//             var firstCall = calls.First();

//             // Use original method name (not GUID)
//             var interceptorName = firstCall.MethodSymbol.Name;

//             // Add parameter suffix for overloaded methods
//             var parameterSignature = string.Join(",", firstCall.Parameters.Select(p => p.ToString()));
//             if (!string.IsNullOrEmpty(parameterSignature))
//             {
//                 interceptorName += $"_{Math.Abs(parameterSignature.GetHashCode()):X8}";
//             }

//             // Generate multiple InterceptsLocation attributes for same method using appropriate syntax
//             foreach (var call in calls)
//             {
//                 sb.AppendLine(GenerateInterceptsLocationAttribute(call, targetFramework));
//             }

//             // Generate interceptor method with original name and signature
//             var originalMethod = methods.FirstOrDefault(m => m.Name == firstCall.MethodSymbol.Name);
//             if (originalMethod != null)
//             {
//                 var parameterSignatureForMethod = originalMethod.Parameters.Length > 0
//                                             ? string.Join(", ", originalMethod.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))
//                                             : string.Empty;

//                 var returnType = firstCall.MethodSymbol.ReturnType.ToDisplayString();
//                 sb.AppendLine($"        public static {returnType} {interceptorName}({parameterSignatureForMethod})");
//                 sb.AppendLine("        {");

//                 if (!responseMap.TryGetValue(firstCall.InvocationId, out var response))
//                 {
//                     Log.LogError($"Response not found for method {methodKey}. Skipping interceptor generation for this method.");
//                     sb.AppendLine($"            // ERROR: Response not found for {methodKey}");
//                     sb.AppendLine("            throw new InvalidOperationException(\"Compile-time method execution failed.\");");
//                     sb.AppendLine("        }");
//                     sb.AppendLine();
//                     continue; // Skip this interceptor if response is missing
//                 }
//                 var executedValue = response.SerializedValue;
//                 ProcessResponse(response, originalMethod, firstCall);

//                 // Check if method returns void or plain Task (executed for side effects only)
//                 if (returnType == "void")
//                 {
//                     // void methods - no return needed
//                 }
//                 else if (returnType == "System.Threading.Tasks.Task")
//                 {
//                     sb.AppendLine("            return System.Threading.Tasks.Task.CompletedTask;");
//                 }
//                 else if (returnType.StartsWith("System.Threading.Tasks.Task<"))
//                 {
//                     // Task<T> methods - wrap the result in Task.FromResult
//                     var innerType = returnType.Substring("System.Threading.Tasks.Task<".Length).TrimEnd('>');
//                     if (!string.IsNullOrEmpty(executedValue) && !executedValue!.StartsWith("/*") && executedValue != "null")
//                     {
//                         sb.AppendLine($"            return System.Threading.Tasks.Task.FromResult({executedValue});");
//                     }
//                     else
//                     {
//                         sb.AppendLine($"            return System.Threading.Tasks.Task.FromResult(default({innerType}));");
//                     }
//                 }
//                 else
//                 {
//                     // Normal methods - return the value directly
//                     sb.AppendLine($"            return {executedValue};");
//                 }
//             }

//             sb.AppendLine("        }");
//             sb.AppendLine();
//         }

//         sb.AppendLine("    }");
//         sb.AppendLine("}");
//         sb.AppendLine("#pragma warning restore CS9270");

//         return sb.ToString();
//     }

//     private NetVersion DetectTargetFramework()
//     {
//         var projectFilePath = Path.Combine(ProjectDir, Path.GetFileName(ProjectDir) + ".csproj");
//         if (!File.Exists(projectFilePath))
//         {
//             Log.LogWarning($"Could not find project file at {projectFilePath}. Defaulting to .NET 8 syntax.");
//             return NetVersion.Net8AndBelow;
//         }

//         try
//         {
//             var projectFileContent = File.ReadAllText(projectFilePath);

//             // Parse <TargetFramework>net9.0</TargetFramework> or <TargetFramework>net8.0</TargetFramework>
//             var match = System.Text.RegularExpressions.Regex.Match(projectFileContent, @"<TargetFramework>net(\d+)\.(\d+)</TargetFramework>");
//             if (match.Success)
//             {
//                 var major = int.Parse(match.Groups[1].Value);
//                 var version = major >= 9 ? NetVersion.Net9Plus : NetVersion.Net8AndBelow;
//                 Log.LogMessage(MessageImportance.Low, $"Detected target framework: .NET {major}.x -> {version}");
//                 return version;
//             }

//             // Check for multi-targeting: <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
//             var multiMatch = System.Text.RegularExpressions.Regex.Match(projectFileContent, @"<TargetFrameworks>([^<]+)</TargetFrameworks>");
//             if (multiMatch.Success)
//             {
//                 var frameworks = multiMatch.Groups[1].Value;
//                 if (frameworks.Contains("net9") || frameworks.Contains("net10")) // Future-proof
//                 {
//                     Log.LogMessage(MessageImportance.Low, $"Multi-targeting detected with .NET 9+: {frameworks} -> Net9Plus");
//                     return NetVersion.Net9Plus;
//                 }
//             }

//             Log.LogMessage(MessageImportance.Low, "No explicit .NET version found, defaulting to .NET 8 syntax");
//             return NetVersion.Net8AndBelow;
//         }
//         catch (Exception ex)
//         {
//             Log.LogWarning($"Error detecting target framework from {projectFilePath}: {ex.Message}. Defaulting to .NET 8 syntax.");
//             return NetVersion.Net8AndBelow;
//         }
//     }

//     private ProjectType DetectProjectType()
//     {
//         var projectFilePath = Path.Combine(ProjectDir, Path.GetFileName(ProjectDir) + ".csproj"); // Assuming project file name matches directory name
//         if (!File.Exists(projectFilePath))
//         {
//             Log.LogWarning($"Could not find project file at {projectFilePath}. Defaulting to ProjectType.Default.");
//             return ProjectType.Default;
//         }

//         try
//         {
//             var projectFileContent = File.ReadAllText(projectFilePath);
//             // Simple XML parsing to find Sdk attribute or specific properties
//             // This is a very basic approach and might need more robust XML parsing for complex project files.
//             if (projectFileContent.Contains("Sdk=\"Microsoft.NET.Sdk.Web\"")) { return ProjectType.AspNetCore; }
//             if (projectFileContent.Contains("Sdk=\"Microsoft.NET.Sdk.Worker\"")) { return ProjectType.Worker; }
//             if (projectFileContent.Contains("Sdk=\"Microsoft.NET.Sdk.WindowsDesktop\""))
//             {
//                 if (projectFileContent.Contains("<UseWindowsForms>true</UseWindowsForms>")) { return ProjectType.WinForms; }
//                 if (projectFileContent.Contains("<UseWPF>true</UseWPF>")) { return ProjectType.WPF; }
//             }
//             // Add more detection logic as needed
//             return ProjectType.Default;
//         }
//         catch (Exception ex)
//         {
//             Log.LogWarning($"Error detecting project type from {projectFilePath}: {ex.Message}. Defaulting to ProjectType.Default.");
//             return ProjectType.Default;
//         }
//     }

//     private void AddDiagnostic(DiagnosticDescriptor descriptor, IMethodSymbol methodSymbol, object[] messageArgs)
//     {
//         var location = methodSymbol.Locations.FirstOrDefault();
//         if (location?.SourceTree == null) { return; }

//         var filePath = location.SourceTree.FilePath;

//         // For MSBuild warnings, we need the full path - construct it from ProjectDir if it's just a filename
//         var fullFilePath = filePath;
//         if (!Path.IsPathRooted(filePath)) { fullFilePath = Path.Combine(ProjectDir, filePath); }

//         _diagnostics.Add(new()
//         {
//             Id = descriptor.Id,
//             MessageArgs = messageArgs,
//             FilePath = fullFilePath,
//             StartPosition = location.SourceSpan.Start,
//             Length = location.SourceSpan.Length
//         });

//         // var lineInfo = location.GetLineSpan();
//         // var line = lineInfo.StartLinePosition.Line + 1;
//         // var column = lineInfo.StartLinePosition.Character + 1;

//         // var formattedMessage = string.Format(descriptor.MessageFormat.ToString(), messageArgs);
//         // Log.LogWarning(null, descriptor.Id, null, fullFilePath, line, column, 0, 0, formattedMessage);
//     }

//     private void SaveDiagnostics()
//     {
//         try
//         {
//             if (_diagnostics.Count == 0) { return; }

//             var diagnosticsFile = Path.Combine(ProjectDir, "obj", "CompileTimeDiagnostics.json");
//             var directory = Path.GetDirectoryName(diagnosticsFile);

//             if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }

//             File.WriteAllText(diagnosticsFile, JsonSerializer.Serialize(_diagnostics, new JsonSerializerOptions
//             {
//                 WriteIndented = true
//             }));
//             Log.LogMessage(MessageImportance.Low, $"Saved {_diagnostics.Count} diagnostics to {diagnosticsFile}");
//         }
//         catch (Exception ex)
//         {
//             Log.LogWarning($"Failed to save diagnostics: {ex.Message}");
//         }
//     }

//     private string GetRelativePathFromGenerated(string filePath)
//     {
//         try
//         {
//             // Normalize paths to avoid issues with different path separators
//             var generatedDir = Path.GetFullPath(OutputPath).Replace('\\', '/');
//             var sourceFile = Path.GetFullPath(filePath).Replace('\\', '/');

//             Log.LogMessage(MessageImportance.Low, $"GetRelativePathFromGenerated: generatedDir = {generatedDir}");
//             Log.LogMessage(MessageImportance.Low, $"GetRelativePathFromGenerated: sourceFile = {sourceFile}");

//             // Use URI approach but ensure proper directory handling
//             if (!generatedDir.EndsWith("/")) { generatedDir += "/"; }

//             var relativeUri = new Uri(generatedDir).MakeRelativeUri(new Uri(sourceFile));
//             var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

//             Log.LogMessage(MessageImportance.Low, $"GetRelativePathFromGenerated: relativePath = {relativePath}");

//             // Ensure forward slashes for interceptor attributes
//             return relativePath.Replace('\\', '/');
//         }
//         catch (Exception ex)
//         {
//             Log.LogWarning($"Could not convert path {filePath} to relative: {ex.Message}. Using absolute path.");
//             // Return the absolute path as fallback - interceptors can handle absolute paths
//             return filePath.Replace('\\', '/');
//         }
//     }

//     private string GenerateInterceptsLocationAttribute(InvocationInfo call, NetVersion netVersion)
//     {
//         // Use InvocationInfo native detection if available
//         if (call.IsNet9OrGreater && !string.IsNullOrEmpty(call.InterceptableData))
//         {
//             return GenerateNet9InterceptsAttribute(call);
//         }

//         // For now, always use .NET 8 syntax until we have proper .NET 9 support
//         return GenerateNet8InterceptsAttribute(call);

//         /* Future: when .NET 9 interceptors are stable
//         return netVersion switch
//         {
//             NetVersion.Net9Plus => GenerateNet9InterceptsAttribute(call),
//             NetVersion.Net8AndBelow => GenerateNet8InterceptsAttribute(call),
//             _ => GenerateNet8InterceptsAttribute(call) // Default fallback
//         };
//         */
//     }

//     private string GenerateNet8InterceptsAttribute(InvocationInfo call)
//     {
//         var relativePath = GetRelativePathFromGenerated(call.FilePath);
//         return $"""        [global::System.Runtime.CompilerServices.InterceptsLocation(@"{relativePath}", {call.Line}, {call.Character})]""";
//     }

//     private string GenerateNet9InterceptsAttribute(InvocationInfo call)
//     {
//         // Use data from InvocationInfo if available, otherwise encode manually
//         if (call.IsNet9OrGreater && !string.IsNullOrEmpty(call.InterceptableData))
//         {
//             return $"""        [global::System.Runtime.CompilerServices.InterceptsLocation({call.InterceptableVersion}, "{call.InterceptableData}")]""";
//         }
//         else
//         {
//             // Fallback to manual encoding
//             var data = EncodeLocationDataForNet9(call);
//             return $"""        [global::System.Runtime.CompilerServices.InterceptsLocation(1, "{data}")]""";
//         }
//     }

//     private string EncodeLocationDataForNet9(InvocationInfo call)
//     {
//         // Encode location information into base64 string for .NET 9 interceptors
//         var locationInfo = $"{call.FilePath}:{call.Line}:{call.Character}";
//         var bytes = System.Text.Encoding.UTF8.GetBytes(locationInfo);
//         var base64 = Convert.ToBase64String(bytes);

//         Log.LogMessage(MessageImportance.Low, $"Encoded .NET 9 location data: {locationInfo} -> {base64}");
//         return base64;
//     }

//     private void ProcessResponse(CompileTimeResponse response, IMethodSymbol methodSymbol, InvocationInfo invocation)
//     {
//         var attr = CompileTimeHelper.GetAttributes(methodSymbol);
//         var performanceThreshold = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.PerformanceWarningThresholdMs), new CompileTimeAttribute().PerformanceWarningThresholdMs);
//         var suppressWarnings = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.SuppressWarnings), new CompileTimeAttribute().SuppressWarnings);
//         var cacheStrategy = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.Cache), new CompileTimeAttribute().Cache);
//         var timeoutMs = CompileTimeHelper.GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.TimeoutMs), TimeoutMs); // Use task's TimeoutMs as default

//         var parametersTxt = string.Join(",", invocation.Parameters);

//         if (response.Success)
//         {
//             if (response.ExecutionTimeMs > 0
//                 && !suppressWarnings && performanceThreshold > 0
//                 && response.ExecutionTimeMs > performanceThreshold)
//             {
//                 AddDiagnostic(DiagnosticDescriptors.SlowMethodExecution, methodSymbol, [methodSymbol.Name, response.ExecutionTimeMs.ToString(), performanceThreshold.ToString()]);
//                 Log.LogMessage(MessageImportance.High, $"Performance warning: Method '{methodSymbol.Name}' took {response.ExecutionTimeMs}ms (threshold: {performanceThreshold}ms)");
//             }
//         }
//         else
//         {
//             if (response.ErrorCode == "COMPTIME301") // Timeout
//             {
//                 var actualTimeoutMs = timeoutMs == -1 ? TimeoutMs : timeoutMs;
//                 switch (TimeoutBehavior.ToLowerInvariant())
//                 {
//                     case "skip": AddDiagnostic(DiagnosticDescriptors.MethodExecutionSkippedTimeout, methodSymbol, [methodSymbol.Name, actualTimeoutMs.ToString()]); break;
//                     case "error":
//                     case "fail": AddDiagnostic(DiagnosticDescriptors.MethodExecutionTimeoutError, methodSymbol, [methodSymbol.Name, actualTimeoutMs.ToString()]); break;
//                     case "warning":
//                     case "warn": AddDiagnostic(DiagnosticDescriptors.MethodExecutionTimeoutWarning, methodSymbol, [methodSymbol.Name, actualTimeoutMs.ToString()]); break;
//                     default: AddDiagnostic(DiagnosticDescriptors.UnknownTimeoutBehavior, methodSymbol, [TimeoutBehavior, methodSymbol.Name]); break;
//                 }
//             }
//             else
//             {
//                 AddDiagnostic(DiagnosticDescriptors.ExecutionError, methodSymbol, [response.ErrorMessage ?? "Unknown error"]);
//             }
//         }

//         _performanceCollector?.Add(new()
//         {
//             ClassName = methodSymbol.ContainingType.Name,
//             MethodName = methodSymbol.Name,
//             Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
//             ExecutionTimeMs = response.ExecutionTimeMs,
//             ThresholdMs = performanceThreshold,
//             Cache = cacheStrategy,
//             ThresholdExceeded = response.ExecutionTimeMs > performanceThreshold,
//             WasSuppressed = suppressWarnings,
//             ErrorMessage = response.ErrorMessage,
//             Parameters = parametersTxt,
//             MemoryFootprintBytes = response.MemoryFootprintBytes
//         });
//     }

//     public void Dispose()
//     {
//         Dispose(true);
//         GC.SuppressFinalize(this);
//     }

//     protected virtual void Dispose(bool disposing)
//     {
//         if (!_disposed && disposing)
//         {
//             // Clear references to compilations and large objects
//             _compilation = null;

//             // Dispose performance collector if it implements IDisposable
//             if (_performanceCollector is IDisposable disposableCollector)
//             {
//                 disposableCollector.Dispose();
//             }
//             _performanceCollector = null;

//             // Clear diagnostic list
//             _diagnostics.Clear();

//             Log.LogMessage(MessageImportance.Low, "CompileTimeTask resources disposed.");

//             _disposed = true;
//         }
//     }

//     private static string GenerateInterceptsLocationAttribute()
//      => """
//             #nullable enable

//             namespace System.Runtime.CompilerServices
//             {
//                 [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
//                 internal sealed class InterceptsLocationAttribute : Attribute
//                 {
//                     // Legacy constructor for .NET 8 and below
//                     public InterceptsLocationAttribute(string filePath, int line, int character)
//                     {
//                         FilePath = filePath;
//                         Line = line;
//                         Character = character;
//                     }

//                     // Modern constructor for .NET 9+
//                     public InterceptsLocationAttribute(int version, string data)
//                     {
//                         Version = version;
//                         Data = data;
//                     }

//                     public string? FilePath { get; }
//                     public int Line { get; }
//                     public int Character { get; }
//                     public int Version { get; }
//                     public string? Data { get; }
//                 }
//             }

//             #nullable restore
//             """;
// }
