/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional;

/// <summary>
/// Everything the generator needs about one <c>IUnion&lt;...&gt;</c> declaration.
/// </summary>
internal sealed class UnionInfo(
    string @namespace,
    string typeName,
    string typeParameters,
    ImmutableArray<ITypeSymbol> caseTypes,
    ImmutableArray<string> caseNames,
    bool emitImplicitConversions,
    bool hasNameCollision,
    ImmutableArray<ContainingTypeInfo> containingTypes,
    Location? location,
    ImmutableArray<string> duplicateMarkerDisplayNames = default,
    string missingModifiers = "")
{
    public string Namespace { get; } = @namespace;
    public string TypeName { get; } = typeName;

    /// <summary>Type parameter list including angle brackets, or empty for a non-generic union.</summary>
    public string TypeParameters { get; } = typeParameters;

    public ImmutableArray<ITypeSymbol> CaseTypes { get; } = caseTypes;

    /// <summary>Wrapper names, positionally aligned with <see cref="CaseTypes"/>.</summary>
    public ImmutableArray<string> CaseNames { get; } = caseNames;

    /// <summary>
    /// False when two cases share a CLR type: duplicate conversion operators would not compile
    /// (CS0557), so the union is reachable through its wrappers only.
    /// </summary>
    public bool EmitImplicitConversions { get; } = emitImplicitConversions;

    /// <summary>
    /// True when two cases still resolve to the same wrapper name after disambiguation. The
    /// generator does not emit code for such a union; a diagnostic reports this to the user.
    /// </summary>
    public bool HasNameCollision { get; } = hasNameCollision;

    /// <summary>
    /// The chain of types the union root is nested inside, outermost first. Empty for a top-level
    /// union root.
    /// </summary>
    public ImmutableArray<ContainingTypeInfo> ContainingTypes { get; } = containingTypes;

    public Location? Location { get; } = location;

    /// <summary>
    /// Display names of every <c>IUnion&lt;...&gt;</c> marker interface found on this root, when
    /// there is more than one (UNION013). Empty (default) when the root has exactly one marker,
    /// the only legal shape.
    /// </summary>
    public ImmutableArray<string> DuplicateMarkerDisplayNames { get; }
        = duplicateMarkerDisplayNames.IsDefault ? ImmutableArray<string>.Empty : duplicateMarkerDisplayNames;

    /// <summary>
    /// True when this root has more than one <c>IUnion&lt;...&gt;</c> marker interface. The
    /// generator emits nothing for such a root (UNION013) rather than guessing which marker was
    /// meant.
    /// </summary>
    public bool HasMultipleMarkers => DuplicateMarkerDisplayNames.Length > 1;

    /// <summary>
    /// The missing modifier(s) - <c>"abstract"</c>, <c>"partial"</c>, or <c>"abstract partial"</c>
    /// - when a root carrying an <c>IUnion&lt;...&gt;</c> marker is not declared both
    /// <c>abstract</c> and <c>partial</c> (UNION014); empty when the declaration is correct.
    /// </summary>
    public string MissingModifiers { get; } = missingModifiers;

    /// <summary>
    /// True when <see cref="MissingModifiers"/> is non-empty: the root is missing <c>abstract</c>,
    /// <c>partial</c>, or both. The generator emits nothing for such a root (UNION014).
    /// </summary>
    public bool IsMissingRequiredModifiers => MissingModifiers.Length > 0;
}

/// <summary>
/// Enough about one ancestor type declaration to re-emit its opening line so a generated partial
/// can be nested back inside it: its declaration keyword (<c>class</c>, <c>record</c>,
/// <c>struct</c>, <c>record struct</c>, ...), name, and type parameter list.
/// </summary>
internal sealed record ContainingTypeInfo(string Keyword, string Name, string TypeParameters);
