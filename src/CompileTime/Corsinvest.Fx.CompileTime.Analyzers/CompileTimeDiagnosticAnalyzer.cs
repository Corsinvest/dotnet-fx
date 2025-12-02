using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Operations;
using Corsinvest.Fx.CompileTime.Diagnostics;

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
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        // Register action to find and process CompileTimeDiagnostic attributes
        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.Attribute);
    }

    private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AttributeSyntax attribute) { return; }

        // Get the attribute's symbol information
        var attributeSymbol = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol as IMethodSymbol;
        if (attributeSymbol?.ContainingType?.Name != "CompileTimeDiagnosticAttribute") { return; }

        var fullName = attributeSymbol.ContainingType.ContainingNamespace?.ToString() + "." + attributeSymbol.ContainingType.Name;
        if (fullName != "Corsinvest.Fx.CompileTime.CompileTimeDiagnosticAttribute") { return; }

        // Get the operation for this attribute to extract constructor arguments

        var operation = context.SemanticModel.GetOperation(attribute, context.CancellationToken);
        if (operation is not IObjectCreationOperation objectCreation) { return; }

        // Extract constructor arguments from the object creation operation
        if (objectCreation.Arguments.Length < 5) { return; }

        var idObj = GetArgumentValue(objectCreation.Arguments[0]);
        var filePathObj = GetArgumentValue(objectCreation.Arguments[1]);
        var startPositionObj = GetArgumentValue(objectCreation.Arguments[2]);
        var lengthObj = GetArgumentValue(objectCreation.Arguments[3]);
        var messageArgsArrayObj = GetArgumentValue(objectCreation.Arguments[4]);

        var id = idObj as string ?? string.Empty;
        var filePath = filePathObj as string ?? string.Empty;
        var startPosition = Convert.ToInt32(startPositionObj ?? 0);
        var length = Convert.ToInt32(lengthObj ?? 0);

        // Extract message args array from the last argument
        var messageArgsArray = messageArgsArrayObj as object[] ?? Array.Empty<object>();
        var messageArgs = new object[messageArgsArray.Length];
        Array.Copy(messageArgsArray, messageArgs, messageArgsArray.Length);

        // Only report warnings and info (errors are handled by MSBuild Task)
        if (IsErrorDiagnostic(id)) { return; }

        var descriptor = _supportedDiagnostics.FirstOrDefault(d => d.Id == id);
        if (descriptor == null) { return; }

        // Get location in the target file
        var location = GetLocation(context.Compilation, filePath, startPosition, length);
        var diagnostic = Diagnostic.Create(descriptor, location, messageArgs);

        context.ReportDiagnostic(diagnostic);
    }

    private static object? GetArgumentValue(IArgumentOperation argument)
    {
        if (argument.Value is ILiteralOperation literal)
        {
            return literal.ConstantValue.Value;
        }
        else if (argument.Value is IArrayCreationOperation arrayCreation)
        {
            // Handle array creation for message args
            var values = new List<object?>();  // Changed to allow nulls
            if (arrayCreation.Initializer is not null)
            {
                foreach (var element in arrayCreation.Initializer.ElementValues)
                {
                    if (element is ILiteralOperation elemLiteral)
                    {
                        values.Add(elemLiteral.ConstantValue.Value);
                    }
                    else
                    {
                        // For non-literal values, try to get the constant value
                        var constValue = element.ConstantValue;
                        values.Add(constValue.HasValue ? constValue.Value : string.Empty);
                    }
                }
            }
            return values.Where(v => v != null).Cast<object>().ToArray();  // Filter out nulls and cast back
        }
        else
        {
            // For other cases, try to get the constant value
            var constValue = argument.Value.ConstantValue;
            return constValue.HasValue ? constValue.Value : string.Empty;
        }
    }

    private static bool IsErrorDiagnostic(string diagnosticId)
        => diagnosticId == DiagnosticDescriptors.MethodMustBeStatic.Id ||
           diagnosticId == DiagnosticDescriptors.MethodContainingTypeNotFound.Id ||
           diagnosticId == DiagnosticDescriptors.GenericMethodNotSupported.Id ||
           diagnosticId == DiagnosticDescriptors.ExecutionError.Id ||
           diagnosticId == DiagnosticDescriptors.MethodExecutionTimeoutError.Id;

    private static Location GetLocation(Compilation compilation, string filePath, int startPosition, int length)
    {
        if (string.IsNullOrEmpty(filePath)) { return Location.None; }

        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(tree =>
            string.Equals(tree.FilePath, filePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(tree.FilePath), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase)
        );

        if (syntaxTree == null) { return Location.None; }

        try
        {
            var span = new TextSpan(startPosition, length);
            return Location.Create(syntaxTree, span);
        }
        catch
        {
            return Location.None;
        }
    }
}
