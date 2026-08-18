/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional;

/// <summary>
/// Suppresses the compiler's non-exhaustiveness warning on switches over <c>IUnion&lt;...&gt;</c>
/// types when every variant is handled.
/// </summary>
/// <remarks>
/// The compiler treats reference-type hierarchies as open and so demands a discard arm.
/// An <c>IUnion&lt;...&gt;</c> hierarchy is closed by construction - the generator emits a private
/// constructor on the root and seals every variant - which makes that discard unreachable.
/// Requiring it is worse than noise: once written, it silently swallows variants added later.
/// Paired with <see cref="UnionExhaustivenessAnalyzer"/>, which names the variants that are missing.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnionExhaustivenessSuppressor : DiagnosticSuppressor
{
    private const string SuppressedDiagnosticId = "CS8509";

    private const string PopulateSwitchStatementId = "IDE0010";
    private const string PopulateSwitchExpressionId = "IDE0072";

    private const string Justification =
        "All variants of the union are handled and the hierarchy is closed, "
        + "so the switch cannot fall through and a default arm would be unreachable.";

    // One descriptor per suppressed diagnostic: a SuppressionDescriptor targets exactly one id.
    private static readonly SuppressionDescriptor NonExhaustiveRule =
        new("UNION005", SuppressedDiagnosticId, Justification);

    private static readonly SuppressionDescriptor PopulateStatementRule =
        new("UNION006", PopulateSwitchStatementId, Justification);

    private static readonly SuppressionDescriptor PopulateExpressionRule =
        new("UNION007", PopulateSwitchExpressionId, Justification);

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions
        => ImmutableArray.Create(NonExhaustiveRule, PopulateStatementRule, PopulateExpressionRule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var rule = diagnostic.Id switch
            {
                SuppressedDiagnosticId => NonExhaustiveRule,
                PopulateSwitchStatementId => PopulateStatementRule,
                PopulateSwitchExpressionId => PopulateExpressionRule,
                _ => null
            };

            if (rule is null) { continue; }
            if (diagnostic.Location.SourceTree is not { } tree) { continue; }

            var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan);
            var semanticModel = context.GetSemanticModel(tree);

            if (IsFullyHandled(node, semanticModel, context.CancellationToken))
            {
                context.ReportSuppression(Suppression.Create(rule, diagnostic));
            }
        }
    }

    /// <summary>
    /// Resolves the switch construct the diagnostic points at - expression or statement - and
    /// reports whether it covers every case of a closed union.
    /// </summary>
    private static bool IsFullyHandled(SyntaxNode node,
                                       SemanticModel semanticModel,
                                       System.Threading.CancellationToken cancellationToken)
    {
        var switchExpression = node as SwitchExpressionSyntax
                               ?? node.FirstAncestorOrSelf<SwitchExpressionSyntax>()
                               ?? node.DescendantNodes().OfType<SwitchExpressionSyntax>().FirstOrDefault();

        if (switchExpression is not null)
        {
            return IsFullyHandled(switchExpression, semanticModel, cancellationToken);
        }

        var switchStatement = node as SwitchStatementSyntax
                              ?? node.FirstAncestorOrSelf<SwitchStatementSyntax>()
                              ?? node.DescendantNodes().OfType<SwitchStatementSyntax>().FirstOrDefault();

        return switchStatement is not null
               && IsFullyHandled(switchStatement, semanticModel, cancellationToken);
    }

    private static bool IsFullyHandled(SwitchStatementSyntax switchStatement,
                                       SemanticModel semanticModel,
                                       System.Threading.CancellationToken cancellationToken)
    {
        var variants = ResolveClosedUnionVariants(switchStatement.Expression, semanticModel, cancellationToken);
        if (variants.IsDefaultOrEmpty) { return false; }

        var handled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var label in switchStatement.Sections.SelectMany(section => section.Labels))
        {
            switch (label)
            {
                case CasePatternSwitchLabelSyntax { WhenClause: not null }:
                    break;

                case CasePatternSwitchLabelSyntax patternLabel:
                    UnionSymbolHelper.CollectMatchedVariants(patternLabel.Pattern, semanticModel, cancellationToken, handled);
                    break;

                case CaseSwitchLabelSyntax constantLabel:
                    UnionSymbolHelper.CollectMatchedVariant(constantLabel.Value, semanticModel, cancellationToken, handled);
                    break;
            }
        }

        return variants.All(variant => handled.Contains(variant.OriginalDefinition));
    }

    /// <summary>
    /// Returns the variants of the union being switched over, or an empty array when the
    /// governing expression is not a union whose hierarchy is provably closed.
    /// </summary>
    private static ImmutableArray<INamedTypeSymbol> ResolveClosedUnionVariants(
        ExpressionSyntax governingExpression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var unionRoot = UnionSymbolHelper.ResolveUnionRoot(governingExpression, semanticModel, cancellationToken, out var isNullable);

        // A nullable union still needs an explicit null arm, so leave the diagnostic in place.
        if (unionRoot is null || isNullable) { return ImmutableArray<INamedTypeSymbol>.Empty; }

        var variants = UnionSymbolHelper.GetVariants(unionRoot);

        return variants.IsDefaultOrEmpty || !UnionSymbolHelper.IsProvablyClosed(unionRoot, variants)
            ? ImmutableArray<INamedTypeSymbol>.Empty
            : variants;
    }

    private static bool IsFullyHandled(SwitchExpressionSyntax switchExpression,
                                       SemanticModel semanticModel,
                                       System.Threading.CancellationToken cancellationToken)
    {
        var variants = ResolveClosedUnionVariants(switchExpression.GoverningExpression, semanticModel, cancellationToken);
        if (variants.IsDefaultOrEmpty) { return false; }

        var handled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var arm in switchExpression.Arms)
        {
            if (arm.WhenClause is not null) { continue; }

            UnionSymbolHelper.CollectMatchedVariants(arm.Pattern, semanticModel, cancellationToken, handled);
        }

        return variants.All(variant => handled.Contains(variant.OriginalDefinition));
    }
}
