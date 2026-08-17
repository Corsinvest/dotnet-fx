using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional;

/// <summary>
/// Everything the generator needs about one <c>[Union&lt;...&gt;]</c> declaration.
/// </summary>
internal sealed class GenericUnionInfo(
    string @namespace,
    string typeName,
    string typeParameters,
    ImmutableArray<ITypeSymbol> caseTypes,
    ImmutableArray<string> caseNames,
    bool emitImplicitConversions,
    bool hasNameCollision,
    ImmutableArray<ContainingTypeInfo> containingTypes,
    Location? location)
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
}

/// <summary>
/// Enough about one ancestor type declaration to re-emit its opening line so a generated partial
/// can be nested back inside it: its declaration keyword (<c>class</c>, <c>record</c>,
/// <c>struct</c>, <c>record struct</c>, ...), name, and type parameter list.
/// </summary>
internal sealed record ContainingTypeInfo(string Keyword, string Name, string TypeParameters);
