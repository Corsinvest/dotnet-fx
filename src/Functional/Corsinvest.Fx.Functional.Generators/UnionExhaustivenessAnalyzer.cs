using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional;

/// <summary>
/// Reports union variants that a switch does not handle.
/// </summary>
/// <remarks>
/// The C# compiler cannot do this on its own: it treats reference-type hierarchies as open, so
/// CS8509 only ever asks for a discard arm - and once that arm is present, a variant added later
/// silently falls into it. This analyzer names the missing variants instead.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnionExhaustivenessAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor MissingVariantDescriptor = new(
        id: "UNION004",
        title: "Switch does not handle all union variants",
        messageFormat: "Switch on union '{0}' does not handle {1}",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Every variant of a [Union] type should be handled explicitly, so that adding "
                   + "a new variant surfaces every switch that needs updating.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(MissingVariantDescriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeSwitchExpression, SyntaxKind.SwitchExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchStatement, SyntaxKind.SwitchStatement);
    }

    private static void AnalyzeSwitchExpression(SyntaxNodeAnalysisContext context)
    {
        var switchExpression = (SwitchExpressionSyntax)context.Node;

        var handled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var arm in switchExpression.Arms)
        {
            // A `when` clause makes the arm conditional, so it does not fully cover the variant.
            if (arm.WhenClause is not null) { continue; }

            UnionSymbolHelper.CollectMatchedVariants(arm.Pattern,
                                                     context.SemanticModel,
                                                     context.CancellationToken,
                                                     handled);
        }

        Report(context, switchExpression.GoverningExpression, handled, switchExpression.SwitchKeyword.GetLocation());
    }

    private static void AnalyzeSwitchStatement(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;

        var handled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var label in switchStatement.Sections.SelectMany(section => section.Labels))
        {
            switch (label)
            {
                // `case Union.Variant when ...:` - conditional, so not full coverage.
                case CasePatternSwitchLabelSyntax { WhenClause: not null }:
                    break;

                case CasePatternSwitchLabelSyntax patternLabel:
                    UnionSymbolHelper.CollectMatchedVariants(patternLabel.Pattern,
                                                             context.SemanticModel,
                                                             context.CancellationToken,
                                                             handled);
                    break;

                // `case Union.Variant:` with no designation parses as a constant label.
                case CaseSwitchLabelSyntax constantLabel:
                    UnionSymbolHelper.CollectMatchedVariant(constantLabel.Value,
                                                            context.SemanticModel,
                                                            context.CancellationToken,
                                                            handled);
                    break;
            }
        }

        Report(context, switchStatement.Expression, handled, switchStatement.SwitchKeyword.GetLocation());
    }

    private static void Report(SyntaxNodeAnalysisContext context,
                               ExpressionSyntax governingExpression,
                               HashSet<INamedTypeSymbol> handled,
                               Location location)
    {
        var unionRoot = UnionSymbolHelper.ResolveUnionRoot(governingExpression,
                                                           context.SemanticModel,
                                                           context.CancellationToken,
                                                           out _);
        if (unionRoot is null) { return; }

        var variants = UnionSymbolHelper.GetVariants(unionRoot);
        if (variants.IsDefaultOrEmpty) { return; }

        var missing = variants.Where(variant => !handled.Contains(variant.OriginalDefinition))
                              .Select(variant => variant.Name)
                              .ToList();

        if (missing.Count == 0) { return; }

        var missingText = missing.Count == 1
            ? $"variant '{missing[0]}'"
            : $"variants {string.Join(", ", missing.Select(name => $"'{name}'"))}";

        context.ReportDiagnostic(Diagnostic.Create(MissingVariantDescriptor, location, unionRoot.Name, missingText));
    }
}
