/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional;

/// <summary>
/// Shared logic for recognising <c>IUnion&lt;...&gt;</c> types and enumerating their variants.
/// Used by the exhaustiveness analyzer, the CS8509 suppressor, and the code fix
/// that fills in missing switch arms.
/// </summary>
public static class UnionSymbolHelper
{
    /// <summary>
    /// Returns true when <paramref name="type"/> is a union root, i.e. implements any arity of
    /// <c>IUnion&lt;...&gt;</c>.
    /// </summary>
    public static bool IsUnionRoot(ITypeSymbol? type)
        => type is INamedTypeSymbol named
           && named.Interfaces.Any(i => i.Name == "IUnion"
                                        && i.ContainingNamespace?.ToDisplayString() == "Corsinvest.Fx.Functional");

    /// <summary>
    /// Walks up the containing-type chain to find the union root that declares
    /// <paramref name="type"/>, or the type itself when it is already a root.
    /// </summary>
    public static INamedTypeSymbol? FindUnionRoot(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (IsUnionRoot(current)) { return current; }
        }

        return null;
    }

    /// <summary>
    /// Returns the variants of a union root: the nested sealed types that derive from it.
    /// Returns an empty array when <paramref name="unionRoot"/> is not a union.
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetVariants(INamedTypeSymbol? unionRoot)
    {
        if (unionRoot is null || !IsUnionRoot(unionRoot)) { return ImmutableArray<INamedTypeSymbol>.Empty; }

        return unionRoot.GetTypeMembers()
                        .Where(nested => nested.IsSealed
                                         && SymbolEqualityComparer.Default.Equals(nested.BaseType?.OriginalDefinition,
                                                                                  unionRoot.OriginalDefinition))
                        .ToImmutableArray();
    }

    /// <summary>
    /// True when the union hierarchy is provably closed: the root is abstract-by-construction
    /// (no accessible constructor outside the hierarchy) and every variant is sealed.
    /// The generator always emits this shape, so this is a sanity check rather than a heuristic.
    /// </summary>
    public static bool IsProvablyClosed(INamedTypeSymbol unionRoot, ImmutableArray<INamedTypeSymbol> variants)
    {
        if (variants.IsDefaultOrEmpty) { return false; }

        // Every instance constructor of the root must be inaccessible from outside the
        // hierarchy, otherwise an external type could derive from it.
        var hasExternallyUsableConstructor = unionRoot.InstanceConstructors
            .Any(c => c.DeclaredAccessibility is not (Accessibility.Private or Accessibility.NotApplicable)
                      // The compiler-generated record copy constructor is protected but cannot
                      // be used to derive a new type from outside the assembly.
                      && !IsRecordCopyConstructor(c, unionRoot));

        return !hasExternallyUsableConstructor && variants.All(v => v.IsSealed);
    }

    private static bool IsRecordCopyConstructor(IMethodSymbol constructor, INamedTypeSymbol containingType)
        => constructor.Parameters.Length == 1
           && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type.OriginalDefinition,
                                                    containingType.OriginalDefinition);

    /// <summary>
    /// Collects the variant types a pattern matches unconditionally.
    /// </summary>
    /// <remarks>
    /// A bare <c>Union.Variant</c> arm parses as a <see cref="ConstantPatternSyntax"/> rather than a
    /// type pattern, so the expression has to be resolved through the semantic model to tell a type
    /// reference from an actual constant.
    /// <para>
    /// <c>or</c> contributes both sides. <c>and</c> and <c>not</c> are deliberately ignored: they
    /// narrow the match, so they do not prove the whole variant is covered.
    /// </para>
    /// </remarks>
    public static void CollectMatchedVariants(PatternSyntax pattern,
                                              SemanticModel semanticModel,
                                              CancellationToken cancellationToken,
                                              HashSet<INamedTypeSymbol> into)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax declaration:
                AddType(declaration.Type, semanticModel, cancellationToken, into);
                break;

            case TypePatternSyntax typePattern:
                AddType(typePattern.Type, semanticModel, cancellationToken, into);
                break;

            case ConstantPatternSyntax constant:
                // `Union.Variant` with no designation lands here.
                CollectMatchedVariant(constant.Expression, semanticModel, cancellationToken, into);
                break;

            case RecursivePatternSyntax recursive when IsWholeTypeMatch(recursive):
                if (recursive.Type is { } recursiveType)
                {
                    AddType(recursiveType, semanticModel, cancellationToken, into);
                }
                break;

            case BinaryPatternSyntax binary when binary.IsKind(SyntaxKind.OrPattern):
                CollectMatchedVariants(binary.Left, semanticModel, cancellationToken, into);
                CollectMatchedVariants(binary.Right, semanticModel, cancellationToken, into);
                break;

            case ParenthesizedPatternSyntax parenthesized:
                CollectMatchedVariants(parenthesized.Pattern, semanticModel, cancellationToken, into);
                break;
        }
    }

    /// <summary>
    /// Adds the variant referenced by a bare type expression, as used by
    /// <c>case Union.Variant:</c> labels and by constant patterns.
    /// Ignores expressions that resolve to anything other than a type.
    /// </summary>
    public static void CollectMatchedVariant(ExpressionSyntax expression,
                                             SemanticModel semanticModel,
                                             CancellationToken cancellationToken,
                                             HashSet<INamedTypeSymbol> into)
    {
        if (semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol is INamedTypeSymbol type)
        {
            into.Add(type.OriginalDefinition);
        }
    }

    /// <summary>
    /// True when a recursive pattern matches every value of its type.
    /// </summary>
    /// <remarks>
    /// That covers <c>Variant { }</c> and <c>Variant()</c>, but also deconstructions whose
    /// subpatterns are all irrefutable - <c>Variant(var a, var b)</c> or <c>Variant { P: _ }</c>.
    /// A subpattern that can fail, such as <c>Variant { Number: "x" }</c>, matches only a subset.
    /// </remarks>
    private static bool IsWholeTypeMatch(RecursivePatternSyntax recursive)
    {
        var propertySubpatterns = recursive.PropertyPatternClause?.Subpatterns;
        var positionalSubpatterns = recursive.PositionalPatternClause?.Subpatterns;

        return (propertySubpatterns?.All(s => IsIrrefutable(s.Pattern)) != false)
               && (positionalSubpatterns?.All(s => IsIrrefutable(s.Pattern)) != false);
    }

    /// <summary>
    /// True for patterns that always match: <c>var x</c>, <c>_</c>, and nestings of those.
    /// </summary>
    private static bool IsIrrefutable(PatternSyntax pattern)
        => pattern switch
        {
            VarPatternSyntax => true,
            DiscardPatternSyntax => true,
            ParenthesizedPatternSyntax parenthesized => IsIrrefutable(parenthesized.Pattern),
            _ => false
        };

    private static void AddType(TypeSyntax typeSyntax,
                                SemanticModel semanticModel,
                                CancellationToken cancellationToken,
                                HashSet<INamedTypeSymbol> into)
    {
        if (semanticModel.GetTypeInfo(typeSyntax, cancellationToken).Type is INamedTypeSymbol type)
        {
            into.Add(type.OriginalDefinition);
        }
    }

    /// <summary>
    /// Resolves the union root behind a switch governing expression, unwrapping <c>Nullable&lt;T&gt;</c>.
    /// Returns null when the expression is not a union.
    /// </summary>
    public static INamedTypeSymbol? ResolveUnionRoot(ExpressionSyntax governingExpression,
                                                     SemanticModel semanticModel,
                                                     CancellationToken cancellationToken,
                                                     out bool isNullable)
    {
        isNullable = false;

        var type = semanticModel.GetTypeInfo(governingExpression, cancellationToken).Type;
        if (type is null) { return null; }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            isNullable = true;
            type = nullable.TypeArguments[0];
        }

        return FindUnionRoot(type);
    }
}
