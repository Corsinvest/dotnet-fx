using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Corsinvest.Fx.CompileTime.Diagnostics;
using Corsinvest.Fx.CompileTime.Models;

namespace Corsinvest.Fx.CompileTime.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CompileTimeDiagnosticAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics =
        ImmutableArray.Create(
            DiagnosticDescriptors.GenericMethodNotSupported,
            DiagnosticDescriptors.SlowMethodExecution,
            DiagnosticDescriptors.MethodExecutionTimeoutWarning,
            DiagnosticDescriptors.MethodExecutionSkippedTimeout,
            DiagnosticDescriptors.CompileTimeSummary,
            DiagnosticDescriptors.PerformanceReportGenerated
        );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => _supportedDiagnostics;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Find CompileTimeDiagnostics.json in AdditionalFiles
            var diagnosticsFile = compilationContext.Options.AdditionalFiles
                .FirstOrDefault(f => f.Path.EndsWith("CompileTimeDiagnostics.json", StringComparison.OrdinalIgnoreCase));

            if (diagnosticsFile == null) { return; }

            // Read and parse JSON
            var text = diagnosticsFile.GetText(compilationContext.CancellationToken);
            if (text == null) { return; }

            TaskDiagnosticData[]? taskDiagnostics;
            try
            {
                taskDiagnostics = JsonSerializer.Deserialize<TaskDiagnosticData[]>(text.ToString());
            }
            catch
            {
                // Invalid JSON, skip
                return;
            }

            if (taskDiagnostics == null || taskDiagnostics.Length == 0) { return; }

            // Register action to report diagnostics

            compilationContext.RegisterCompilationEndAction(ctx =>
            {
                foreach (var taskDiag in taskDiagnostics)
                {
                    // Only report warnings and info (errors are handled by MSBuild Task)
                    if (IsErrorDiagnostic(taskDiag.Id)) { continue; }

                    var descriptor = _supportedDiagnostics.FirstOrDefault(d => d.Id == taskDiag.Id);
                    if (descriptor == null) { continue; }

                    var location = GetLocation(ctx.Compilation, taskDiag);
                    var diagnostic = Diagnostic.Create(descriptor, location, taskDiag.MessageArgs ?? Array.Empty<object>());

                    ctx.ReportDiagnostic(diagnostic);
                }
            });
        });
    }

    private static bool IsErrorDiagnostic(string diagnosticId)
        => diagnosticId == DiagnosticDescriptors.MethodMustBeStatic.Id ||
           diagnosticId == DiagnosticDescriptors.MethodContainingTypeNotFound.Id ||
           diagnosticId == DiagnosticDescriptors.GenericMethodNotSupported.Id ||
           diagnosticId == DiagnosticDescriptors.ExecutionError.Id ||
           diagnosticId == DiagnosticDescriptors.MethodExecutionTimeoutError.Id;

    private static Location GetLocation(Compilation compilation, TaskDiagnosticData taskDiag)
    {
        if (string.IsNullOrEmpty(taskDiag.FilePath)) { return Location.None; }

        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(tree =>
            string.Equals(tree.FilePath, taskDiag.FilePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(tree.FilePath), Path.GetFullPath(taskDiag.FilePath), StringComparison.OrdinalIgnoreCase)
        );

        if (syntaxTree == null) { return Location.None; }

        try
        {
            var span = new TextSpan(taskDiag.StartPosition, taskDiag.Length);
            return Location.Create(syntaxTree, span);
        }
        catch
        {
            return Location.None;
        }
    }
}
