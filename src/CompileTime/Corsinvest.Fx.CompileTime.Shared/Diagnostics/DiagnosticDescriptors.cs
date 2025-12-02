using Microsoft.CodeAnalysis;

namespace Corsinvest.Fx.CompileTime.Diagnostics;

/// <summary>
/// Centralized collection of all diagnostic descriptors used by CompileTime analyzers and generators.
/// Diagnostics are numbered consecutively for simplicity.
/// </summary>
public static class DiagnosticDescriptors
{
    // COMPTIME001-004: Core analyzer errors
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

    // COMPTIME005-010: Performance and suggestions
    public static readonly DiagnosticDescriptor SuggestCompileTime = new(
        id: "COMPTIME005",
        title: "Method could benefit from [CompileTime]",
        messageFormat: "Method '{0}' could benefit from compile-time execution. Consider adding [CompileTime] attribute.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "This method appears to return constant values or perform deterministic calculations that could be executed at compile-time.");

    public static readonly DiagnosticDescriptor SlowMethodExecution = new(
        id: "COMPTIME006",
        title: "Slow compile-time method execution",
        messageFormat: "Method '{0}' took {1}ms, exceeding performance threshold of {2}ms",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "CompileTime method execution exceeded the configured performance threshold.");

    // COMPTIME011-015: Timeout handling
    public static readonly DiagnosticDescriptor MethodExecutionSkippedTimeout = new(
        id: "COMPTIME011",
        title: "Method execution skipped due to timeout",
        messageFormat: "Method '{0}' execution skipped after {1}ms timeout (behavior: Skip)",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Method execution was skipped due to timeout configuration.");

    public static readonly DiagnosticDescriptor MethodExecutionTimeoutError = new(
        id: "COMPTIME012",
        title: "Method execution timeout error",
        messageFormat: "Method '{0}' execution failed after {1}ms timeout (behavior: Error)",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Method execution failed due to timeout and timeout behavior is set to Error.");

    public static readonly DiagnosticDescriptor MethodExecutionTimeoutWarning = new(
        id: "COMPTIME013",
        title: "Method execution timeout warning",
        messageFormat: "Method '{0}' execution timed out after {1}ms, using default value (behavior: Warning)",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Method execution timed out and timeout behavior is set to Warning.");

    public static readonly DiagnosticDescriptor UnknownTimeoutBehavior = new(
        id: "COMPTIME014",
        title: "Unknown timeout behavior",
        messageFormat: "Unknown timeout behavior '{0}', defaulting to Skip for method '{1}'",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The specified timeout behavior is not recognized.");

    public static readonly DiagnosticDescriptor ExecutionTimeout = new(
        id: "COMPTIME015",
        title: "Method execution timeout",
        messageFormat: "Method execution timed out: {0}",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Method execution exceeded the configured timeout.");

    // COMPTIME016-020: Execution errors
    public static readonly DiagnosticDescriptor ExecutionError = new(
        id: "COMPTIME016",
        title: "Method execution error",
        messageFormat: "Method execution error: {0}",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An error occurred during method execution.");

    // COMPTIME021-025: Generator errors
    public static readonly DiagnosticDescriptor SourceGenerationError = new(
        id: "COMPTIME021",
        title: "Source generation error",
        messageFormat: "Failed to generate interceptors: {0}",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An error occurred during source code generation.");

    // COMPTIME026-030: Informational messages
    public static readonly DiagnosticDescriptor PerformanceReportGenerated = new(
        id: "COMPTIME026",
        title: "Performance report generated",
        messageFormat: "📊 Performance report generated: {0}",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "CompileTime performance report has been generated.");

    public static readonly DiagnosticDescriptor CompileTimeSummary = new(
        id: "COMPTIME027",
        title: "CompileTime Summary",
        messageFormat: "✅ CompileTimeGenerator: Processed {0} method(s) - {1} successful, {2} errors",
        category: "CompileTime",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Summary of CompileTime processing results.");
}
