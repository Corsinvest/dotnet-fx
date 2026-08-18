using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Corsinvest.Fx.Functional;

/// <summary>
/// Turns a union's case types into the names of the nested wrappers the generator emits.
/// </summary>
/// <remarks>
/// Kept free of any Roslyn generator plumbing so the rules can be unit-tested directly.
/// </remarks>
public static class UnionCaseNaming
{
    /// <summary>
    /// Derives a wrapper name from a single case type, ignoring collisions with other cases.
    /// </summary>
    public static string GetSimpleName(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return GetSimpleName(array.ElementType) + "Array";

            case INamedTypeSymbol { IsTupleType: true } tuple:
                var elements = new StringBuilder("TupleOf");
                foreach (var element in tuple.TupleElements)
                {
                    elements.Append(GetSimpleName(element.Type));
                }
                return elements.ToString();

            case INamedTypeSymbol { IsGenericType: true } generic:
                // The bare name, not Name+Of+args: a union usually has one case per generic
                // definition, and Option<T>.Some reads better than Option<T>.SomeOfT. When a
                // union really does carry two constructions of the same definition, ResolveNames
                // falls back to the argument-qualified form for the ones that clash.
                return generic.Name;

            default:
                // Use the CLR name so that `int` becomes Int32 rather than an illegal identifier.
                return type.Name;
        }
    }

    /// <summary>
    /// The argument-qualified form of a generic case name, e.g. <c>SomeOfInt32</c>, used only to
    /// separate two constructions of one generic definition within the same union.
    /// </summary>
    /// <param name="generic">The constructed generic case type.</param>
    private static string GetArgumentQualifiedName(INamedTypeSymbol generic)
    {
        var builder = new StringBuilder(generic.Name).Append("Of");

        foreach (var argument in generic.TypeArguments)
        {
            builder.Append(GetSimpleName(argument));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Resolves one wrapper name per case type, applying overrides and disambiguating collisions
    /// by prefixing the containing namespace.
    /// </summary>
    /// <param name="caseTypes">The union's case types, in declaration order.</param>
    /// <param name="overrides">Explicit wrapper names keyed by case type; wins over the derived name.</param>
    /// <param name="rootTypeParameters">
    /// The union root's own type parameters (empty for a non-generic root). Used only to decide
    /// when an override may match by original definition instead of exact identity - see
    /// <see cref="FindOverride"/>.
    /// </param>
    /// <param name="hasUnresolvedCollision">
    /// True when two cases still share a name after prefixing, which the caller reports as UNION008.
    /// </param>
    public static ImmutableArray<string> ResolveNames(ImmutableArray<ITypeSymbol> caseTypes,
                                                      IReadOnlyDictionary<ITypeSymbol, string> overrides,
                                                      ImmutableArray<ITypeParameterSymbol> rootTypeParameters,
                                                      out bool hasUnresolvedCollision)
    {
        var names = new string[caseTypes.Length];
        for (var i = 0; i < caseTypes.Length; i++)
        {
            names[i] = FindOverride(caseTypes[i], overrides, rootTypeParameters, out var custom)
                ? custom
                : GetSimpleName(caseTypes[i]);
        }

        // A generic case keeps its bare name, so two constructions of one definition
        // (Some<int> and Some<string>) would both want "Some" - even when an override has already
        // renamed one of them and hidden the clash from a name-only comparison. Qualify by type
        // argument whenever a definition appears more than once, then fall back to the namespace
        // prefix for whatever still collides by name.
        var repeatedDefinitions = caseTypes
            .OfType<INamedTypeSymbol>()
            .Where(t => t.IsGenericType)
            .GroupBy(t => t.OriginalDefinition, SymbolEqualityComparer.Default)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToImmutableHashSet(SymbolEqualityComparer.Default);

        for (var i = 0; i < names.Length; i++)
        {
            if (caseTypes[i] is INamedTypeSymbol { IsGenericType: true } repeated
                && repeatedDefinitions.Contains(repeated.OriginalDefinition)
                && !FindOverride(caseTypes[i], overrides, rootTypeParameters, out _))
            {
                names[i] = GetArgumentQualifiedName(repeated);
            }
        }

        // Prefix the namespace only for the names that actually clash.
        var clashing = names.GroupBy(n => n)
                            .Where(g => g.Count() > 1)
                            .Select(g => g.Key)
                            .ToImmutableHashSet();

        for (var i = 0; i < names.Length; i++)
        {
            if (clashing.Contains(names[i]) && !FindOverride(caseTypes[i], overrides, rootTypeParameters, out _))
            {
                var prefix = caseTypes[i].ContainingNamespace is { IsGlobalNamespace: false } ns
                    ? ns.Name
                    : string.Empty;

                names[i] = prefix + names[i];
            }
        }

        hasUnresolvedCollision = names.Distinct().Count() != names.Length;

        return names.ToImmutableArray();
    }

    /// <summary>
    /// Looks up the <c>[UnionCaseName&lt;T&gt;]</c> override for one case type.
    /// </summary>
    /// <remarks>
    /// An attribute type argument cannot mention the root's own type parameter (CS8968), so an
    /// override for a case that closes over it must name a closed stand-in instead - e.g.
    /// <c>[UnionCaseName&lt;Some&lt;int&gt;&gt;("Some")]</c> for a case that actually arrives as the
    /// open <c>Some&lt;T&gt;</c>. Matching by original definition alone would also collapse two
    /// genuinely distinct closed cases onto the same key - <c>Some&lt;int&gt;</c> and
    /// <c>Some&lt;string&gt;</c> in <c>IUnion&lt;Some&lt;int&gt;, Some&lt;string&gt;&gt;</c> share one
    /// original definition (<c>Some&lt;&gt;</c>) but must never share an override.
    /// <para>
    /// So: try an exact match first. Only fall back to matching by original definition when the
    /// case type is not fully closed with respect to the root - i.e. it still mentions at least one
    /// of the root's own type parameters somewhere in its type argument tree. A case that mentions
    /// none of the root's type parameters is fully closed and must be named exactly, never by
    /// original-definition fallback.
    /// </para>
    /// </remarks>
    /// <param name="caseType">The case type to find an override for.</param>
    /// <param name="overrides">Explicit wrapper names keyed by the override attribute's own (closed) type argument.</param>
    /// <param name="rootTypeParameters">The union root's own type parameters.</param>
    /// <param name="name">The matched override name, if any.</param>
    private static bool FindOverride(ITypeSymbol caseType,
                                     IReadOnlyDictionary<ITypeSymbol, string> overrides,
                                     ImmutableArray<ITypeParameterSymbol> rootTypeParameters,
                                     out string name)
    {
        if (overrides.TryGetValue(caseType, out name!)) { return true; }

        // The fallback below cannot be a dictionary lookup: overrides is keyed on the exact type
        // the attribute names (e.g. Some<int>), not on that type's original definition, since a
        // second override for a genuinely different closed case (Some<string>) must not collapse
        // onto the same key. So this scans for an override key that shares one original definition
        // with the case type - safe only because it is additionally guarded by
        // MentionsAnyTypeParameter, which limits the scan to cases that are open with respect to
        // the root (there is at most one such case per distinct case shape in practice, and a
        // genuine ambiguity here would mean two overrides for the same open generic, which
        // UnionCaseNameAttribute's [AttributeUsage(AllowMultiple = true)] cannot prevent but which
        // no test in this codebase exercises).
        if (!rootTypeParameters.IsDefaultOrEmpty && MentionsAnyTypeParameter(caseType, rootTypeParameters))
        {
            foreach (var pair in overrides)
            {
                if (SymbolEqualityComparer.Default.Equals(pair.Key.OriginalDefinition, caseType.OriginalDefinition))
                {
                    name = pair.Value;
                    return true;
                }
            }
        }

        name = null!;
        return false;
    }

    /// <summary>
    /// True when <paramref name="type"/> is one of <paramref name="typeParameters"/>, or a
    /// constructed generic type with one of them somewhere in its type argument tree (recursively,
    /// so a case like <c>Wrapper&lt;Some&lt;T&gt;&gt;</c> still counts).
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT recurse into <see cref="IArrayTypeSymbol"/>: unlike a generic named
    /// type, an array type's <see cref="ITypeSymbol.OriginalDefinition"/> is the array type itself
    /// (<c>T[].OriginalDefinition</c> is <c>T[]</c>, not some unbound array shape), so the
    /// <see cref="FindOverride"/> fallback this method gates - matching by original definition -
    /// could never succeed for an array case even if this returned true for it. A case type of
    /// <c>T[]</c> on a generic root therefore cannot currently be renamed via
    /// <c>[UnionCaseName&lt;int[]&gt;]</c> or similar; it keeps its derived name (e.g. <c>TArray</c>).
    /// Fixing that needs a structural (rank + element shape) comparison in <see cref="FindOverride"/>
    /// instead of original-definition equality, which is a separate piece of work.
    /// </remarks>
    private static bool MentionsAnyTypeParameter(ITypeSymbol type,
                                                  ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        if (type is ITypeParameterSymbol && typeParameters.Contains(type, SymbolEqualityComparer.Default))
        {
            return true;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named)
        {
            return named.TypeArguments.Any(a => MentionsAnyTypeParameter(a, typeParameters));
        }

        return false;
    }
}
