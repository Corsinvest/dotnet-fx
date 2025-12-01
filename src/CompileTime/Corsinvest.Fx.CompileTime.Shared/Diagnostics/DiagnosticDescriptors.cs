using Microsoft.CodeAnalysis;

namespace Corsinvest.Fx.CompileTime.Diagnostics;

/// <summary>
/// Centralized collection of all diagnostic descriptors used by CompileTime analyzers and generators
/// </summary>
public static class DiagnosticDescriptors
{
    // COMPTIME001-099: Core analyzer errors
    public static readonly DiagnosticDescriptor MethodMustBeStatic = new(
        id: "COMPTIME001",
        title: "Method must be static",
        messageFormat: "Method '{0}' must be static to use [CompileTime]",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CompileTime methods must be static to be executed at compile-time.");

    public static readonly DiagnosticDescriptor MethodContainingTypeNotFound = new(
        id: "COMPTIME002",
        title: "Method containing type not found",
        messageFormat: "Method '{0}' containing type could not be resolved",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The containing type of the CompileTime method could not be resolved.");

    public static readonly DiagnosticDescriptor GenericOrComplexNamespace = new(
        id: "COMPTIME003",
        title: "Generic or complex namespace detected",
        messageFormat: "Method '{0}' is in a complex namespace '{1}' which may cause compilation issues",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Methods in generic or complex namespaces may cause issues during code generation.");

    public static readonly DiagnosticDescriptor GenericMethodNotSupported = new(
        id: "COMPTIME004",
        title: "Generic methods are not supported",
        messageFormat: "Method '{0}' has type parameters. Generic methods are not supported by CompileTime. Use non-generic methods or remove [CompileTime] attribute.",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CompileTime does not support methods with generic type parameters. Only generic return types are supported.");

    // COMPTIME100-199: Performance and timeout warnings
    public static readonly DiagnosticDescriptor SuggestCompileTime = new(
        id: "COMPTIME100",
        title: "Method could benefit from [CompileTime]",
        messageFormat: "Method '{0}' could benefit from compile-time execution. Consider adding [CompileTime] attribute.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "This method appears to return constant values or perform deterministic calculations that could be executed at compile-time.");

    public static readonly DiagnosticDescriptor SlowMethodExecution = new(
        id: "COMPTIME101",
        title: "Slow compile-time method execution",
        messageFormat: "Method '{0}' took {1}ms, exceeding performance threshold of {2}ms",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "CompileTime method execution exceeded the configured performance threshold.");

    public static readonly DiagnosticDescriptor MethodExecutionSkippedTimeout = new(
        id: "COMPTIME102",
        title: "Method execution skipped due to timeout",
        messageFormat: "Method '{0}' execution skipped after {1}ms timeout (behavior: Skip)",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Method execution was skipped due to timeout configuration.");

    public static readonly DiagnosticDescriptor MethodExecutionTimeoutError = new(
        id: "COMPTIME103",
        title: "Method execution timeout error",
        messageFormat: "Method '{0}' execution failed after {1}ms timeout (behavior: Error)",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Method execution failed due to timeout and timeout behavior is set to Error.");

    public static readonly DiagnosticDescriptor MethodExecutionTimeoutWarning = new(
        id: "COMPTIME104",
        title: "Method execution timeout warning",
        messageFormat: "Method '{0}' execution timed out after {1}ms, using default value (behavior: Warning)",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Method execution timed out and timeout behavior is set to Warning.");

    public static readonly DiagnosticDescriptor UnknownTimeoutBehavior = new(
        id: "COMPTIME105",
        title: "Unknown timeout behavior",
        messageFormat: "Unknown timeout behavior '{0}', defaulting to Skip for method '{1}'",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The specified timeout behavior is not recognized.");

    // COMPTIME301-399: Execution errors
    public static readonly DiagnosticDescriptor ExecutionError = new(
        id: "COMPTIME302",
        title: "Method execution error",
        messageFormat: "Method execution error: {0}",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An error occurred during method execution.");

    // COMPTIME901-999: Generator errors
    public static readonly DiagnosticDescriptor SourceGenerationError = new(
        id: "COMPTIME901",
        title: "Source generation error",
        messageFormat: "Failed to generate interceptors: {0}",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An error occurred during source code generation.");

    // COMPTIME997-998: Informational messages
    public static readonly DiagnosticDescriptor PerformanceReportGenerated = new(
        id: "COMPTIME997",
        title: "Performance report generated",
        messageFormat: "📊 Performance report generated: {0}",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "CompileTime performance report has been generated.");

    public static readonly DiagnosticDescriptor CompileTimeSummary = new(
        id: "COMPTIME998",
        title: "CompileTime Summary",
        messageFormat: "✅ CompileTimeGenerator: Processed {0} method(s) - {1} successful, {2} errors",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Summary of CompileTime processing results.");
}
