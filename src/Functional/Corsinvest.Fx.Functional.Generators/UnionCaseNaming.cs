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
                var builder = new StringBuilder(generic.Name).Append("Of");
                foreach (var argument in generic.TypeArguments)
                {
                    builder.Append(GetSimpleName(argument));
                }
                return builder.ToString();

            default:
                // Use the CLR name so that `int` becomes Int32 rather than an illegal identifier.
                return type.Name;
        }
    }

    /// <summary>
    /// Resolves one wrapper name per case type, applying overrides and disambiguating collisions
    /// by prefixing the containing namespace.
    /// </summary>
    /// <param name="caseTypes">The union's case types, in declaration order.</param>
    /// <param name="overrides">Explicit wrapper names keyed by case type; wins over the derived name.</param>
    /// <param name="hasUnresolvedCollision">
    /// True when two cases still share a name after prefixing, which the caller reports as UNION008.
    /// </param>
    public static ImmutableArray<string> ResolveNames(ImmutableArray<ITypeSymbol> caseTypes,
                                                      IReadOnlyDictionary<ITypeSymbol, string> overrides,
                                                      out bool hasUnresolvedCollision)
    {
        var names = new string[caseTypes.Length];
        for (var i = 0; i < caseTypes.Length; i++)
        {
            // Overrides are keyed on the original definition (see ReadCaseNameOverrides): the
            // attribute names a closed stand-in (Some<int>) because it cannot mention the root's
            // own type parameter, while a case type that closes over that parameter arrives here
            // still open (Some<T>). Both share one original definition.
            names[i] = overrides.TryGetValue(caseTypes[i].OriginalDefinition, out var custom)
                ? custom
                : GetSimpleName(caseTypes[i]);
        }

        // Prefix the namespace only for the names that actually clash.
        var clashing = names.GroupBy(n => n)
                            .Where(g => g.Count() > 1)
                            .Select(g => g.Key)
                            .ToImmutableHashSet();

        for (var i = 0; i < names.Length; i++)
        {
            if (clashing.Contains(names[i]) && !overrides.ContainsKey(caseTypes[i].OriginalDefinition))
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
}
