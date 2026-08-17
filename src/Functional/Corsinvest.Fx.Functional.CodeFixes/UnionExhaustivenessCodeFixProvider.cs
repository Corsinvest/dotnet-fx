using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Corsinvest.Fx.Functional;

/// <summary>
/// Fixes UNION004 by adding an arm for every union case the switch does not handle.
/// </summary>
/// <remarks>
/// Each generated arm deconstructs the case, so its data is immediately in scope, and throws
/// <see cref="System.NotImplementedException"/> so that an unfinished arm fails loudly rather
/// than returning a plausible default.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnionExhaustivenessCodeFixProvider))]
[Shared]
public class UnionExhaustivenessCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "UNION004";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => Microsoft.CodeAnalysis.CodeFixes.WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) { return; }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            // UNION004 is reported on the `switch` keyword, so walk out to the construct itself.
            var switchExpression = node.FirstAncestorOrSelf<SwitchExpressionSyntax>();
            if (switchExpression is not null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Add missing union cases",
                        createChangedDocument: ct => AddMissingArmsAsync(context.Document, switchExpression, ct),
                        equivalenceKey: "AddMissingUnionCases"),
                    diagnostic);
                continue;
            }

            var switchStatement = node.FirstAncestorOrSelf<SwitchStatementSyntax>();
            if (switchStatement is not null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Add missing union cases",
                        createChangedDocument: ct => AddMissingSectionsAsync(context.Document, switchStatement, ct),
                        equivalenceKey: "AddMissingUnionCases"),
                    diagnostic);
            }
        }
    }

    // ---- switch expression --------------------------------------------------

    private static async Task<Document> AddMissingArmsAsync(Document document,
                                                            SwitchExpressionSyntax switchExpression,
                                                            CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null) { return document; }

        var handled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var arm in switchExpression.Arms)
        {
            if (arm.WhenClause is not null) { continue; }

            UnionSymbolHelper.CollectMatchedVariants(arm.Pattern, semanticModel, cancellationToken, handled);
        }

        var missing = GetMissingVariants(switchExpression.GoverningExpression, semanticModel, handled, cancellationToken);
        if (missing.Count == 0) { return document; }

        var newArms = switchExpression.Arms;

        // Keep an existing discard arm last: it must stay the final arm to remain reachable.
        var discardIndex = IndexOfDiscardArm(switchExpression.Arms);
        var insertAt = discardIndex >= 0 ? discardIndex : switchExpression.Arms.Count;

        foreach (var variant in missing)
        {
            newArms = newArms.Insert(insertAt, CreateArm(variant, semanticModel, switchExpression.SpanStart));
            insertAt++;
        }

        var newSwitch = switchExpression.WithArms(newArms).WithAdditionalAnnotations(Formatter.Annotation);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(switchExpression, newSwitch));
    }

    private static SwitchExpressionArmSyntax CreateArm(INamedTypeSymbol variant,
                                                       SemanticModel semanticModel,
                                                       int position)
        => SwitchExpressionArm(
            CreatePattern(variant, semanticModel, position),
            ThrowExpression(NotImplemented()));

    private static int IndexOfDiscardArm(SeparatedSyntaxList<SwitchExpressionArmSyntax> arms)
    {
        for (var i = 0; i < arms.Count; i++)
        {
            if (arms[i].Pattern is DiscardPatternSyntax) { return i; }
        }

        return -1;
    }

    // ---- switch statement ---------------------------------------------------

    private static async Task<Document> AddMissingSectionsAsync(Document document,
                                                                SwitchStatementSyntax switchStatement,
                                                                CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null) { return document; }

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

        var missing = GetMissingVariants(switchStatement.Expression, semanticModel, handled, cancellationToken);
        if (missing.Count == 0) { return document; }

        var newSections = switchStatement.Sections;

        // A `default:` section stays last so the added cases remain reachable.
        var defaultIndex = IndexOfDefaultSection(switchStatement.Sections);
        var insertAt = defaultIndex >= 0 ? defaultIndex : switchStatement.Sections.Count;

        foreach (var variant in missing)
        {
            newSections = newSections.Insert(insertAt, CreateSection(variant, semanticModel, switchStatement.SpanStart));
            insertAt++;
        }

        var newSwitch = switchStatement.WithSections(newSections).WithAdditionalAnnotations(Formatter.Annotation);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(switchStatement, newSwitch));
    }

    private static SwitchSectionSyntax CreateSection(INamedTypeSymbol variant,
                                                     SemanticModel semanticModel,
                                                     int position)
        => SwitchSection(
            List<SwitchLabelSyntax>(
            [
                CasePatternSwitchLabel(
                    CreatePattern(variant, semanticModel, position),
                    Token(SyntaxKind.ColonToken))
            ]),
            List<StatementSyntax>([ThrowStatement(NotImplemented())]));

    private static int IndexOfDefaultSection(SyntaxList<SwitchSectionSyntax> sections)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i].Labels.Any(l => l is DefaultSwitchLabelSyntax)) { return i; }
        }

        return -1;
    }

    // ---- shared -------------------------------------------------------------

    private static List<INamedTypeSymbol> GetMissingVariants(ExpressionSyntax governingExpression,
                                                             SemanticModel semanticModel,
                                                             HashSet<INamedTypeSymbol> handled,
                                                             CancellationToken cancellationToken)
    {
        var unionRoot = UnionSymbolHelper.ResolveUnionRoot(governingExpression, semanticModel, cancellationToken, out _);
        if (unionRoot is null) { return []; }

        return [.. UnionSymbolHelper.GetVariants(unionRoot)
                                    .Where(v => !handled.Contains(v.OriginalDefinition))];
    }

    /// <summary>
    /// Builds <c>Union.Case(var a, var b)</c>, falling back to a bare type pattern for a case
    /// with no positional members - there is nothing to deconstruct there.
    /// </summary>
    /// <remarks>
    /// A generic-union wrapper (see <c>UnionAttribute&lt;T1..T8&gt;</c>) has exactly one positional
    /// member, always called <c>Value</c>. Naming the local <c>value</c> in that case would be
    /// uninformative, so the variable is named after the wrapped type instead, e.g.
    /// <c>Pet.Cat(var cat)</c> rather than <c>Pet.Cat(var value)</c>.
    /// </remarks>
    private static PatternSyntax CreatePattern(INamedTypeSymbol variant, SemanticModel semanticModel, int position)
    {
        var typeSyntax = ParseTypeName(variant.ToMinimalDisplayString(semanticModel, position));
        var parameters = GetPrimaryConstructorParameters(variant);

        if (parameters.Count == 0) { return TypePattern(typeSyntax); }

        IEnumerable<SubpatternSyntax> subpatterns;
        if (parameters.Count == 1 && parameters[0].Name == "Value")
        {
            subpatterns = new[]
            {
                Subpattern(VarPattern(SingleVariableDesignation(
                    Identifier(SafeIdentifier(parameters[0].Type.Name)))))
            };
        }
        else
        {
            subpatterns = parameters.Select(p =>
                Subpattern(VarPattern(SingleVariableDesignation(Identifier(SafeIdentifier(p.Name))))));
        }

        return RecursivePattern(
            typeSyntax,
            PositionalPatternClause(SeparatedList(subpatterns)),
            propertyPatternClause: null,
            designation: null);
    }

    /// <summary>
    /// Returns the positional members of a record case, which is what its Deconstruct exposes.
    /// </summary>
    private static IReadOnlyList<IParameterSymbol> GetPrimaryConstructorParameters(INamedTypeSymbol variant)
    {
        var deconstruct = variant.GetMembers("Deconstruct")
                                 .OfType<IMethodSymbol>()
                                 .FirstOrDefault(m => m.Parameters.All(p => p.RefKind == RefKind.Out));

        if (deconstruct is not null) { return deconstruct.Parameters; }

        // No Deconstruct: fall back to the longest instance constructor, skipping the
        // compiler-generated record copy constructor.
        var constructor = variant.InstanceConstructors
            .Where(c => c.Parameters.Length > 0
                        && !(c.Parameters.Length == 1
                             && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type.OriginalDefinition,
                                                                      variant.OriginalDefinition)))
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        return constructor?.Parameters ?? (IReadOnlyList<IParameterSymbol>)[];
    }

    /// <summary>
    /// Lower-cases the member name for use as a local, escaping it when it collides with a keyword.
    /// </summary>
    private static string SafeIdentifier(string name)
    {
        var candidate = char.ToLowerInvariant(name[0]) + name.Substring(1);

        return SyntaxFacts.GetKeywordKind(candidate) == SyntaxKind.None
               && SyntaxFacts.GetContextualKeywordKind(candidate) == SyntaxKind.None
                    ? candidate
                    : "@" + candidate;
    }

    private static ObjectCreationExpressionSyntax NotImplemented()
        => ObjectCreationExpression(ParseTypeName("System.NotImplementedException"))
            .WithArgumentList(ArgumentList());
}
