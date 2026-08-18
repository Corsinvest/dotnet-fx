/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Covers the rules that turn a case type into a nested wrapper name.
/// </summary>
public class UnionCaseNamingTests
{
    [Theory]
    [InlineData("Cat", "Cat")]
    [InlineData("int", "Int32")]
    [InlineData("string", "String")]
    [InlineData("int[]", "Int32Array")]
    [InlineData("System.Collections.Generic.List<string>", "List")]
    [InlineData("System.Collections.Generic.Dictionary<string, int>", "Dictionary")]
    [InlineData("(int X, int Y)", "TupleOfInt32Int32")]
    public void GetSimpleName_DerivesWrapperName(string typeExpression, string expected)
    {
        var type = ResolveType(typeExpression);

        Assert.Equal(expected, UnionCaseNaming.GetSimpleName(type));
    }

    [Fact]
    public void ResolveNames_PrefixesNamespace_WhenShortNamesCollide()
    {
        var farmCat = ResolveType("Farm.Cat");
        var wildCat = ResolveType("Wild.Cat");

        var names = UnionCaseNaming.ResolveNames(
            ImmutableArray.Create(farmCat, wildCat),
            new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default),
            ImmutableArray<ITypeParameterSymbol>.Empty,
            out var unresolved);

        Assert.False(unresolved);
        Assert.Equal(["FarmCat", "WildCat"], names);
    }

    [Fact]
    public void ResolveNames_HonoursExplicitOverride()
    {
        var cat = ResolveType("Cat");
        var overrides = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default)
        {
            [cat] = "Domestic"
        };

        var names = UnionCaseNaming.ResolveNames(
            ImmutableArray.Create(cat), overrides, ImmutableArray<ITypeParameterSymbol>.Empty, out var unresolved);

        Assert.False(unresolved);
        Assert.Equal(["Domestic"], names);
    }

    [Fact]
    public void ResolveNames_FlagsCollisionThatNamespacePrefixCannotFix()
    {
        // Two identical types cannot be told apart by name at all.
        var cat = ResolveType("Cat");

        UnionCaseNaming.ResolveNames(
            ImmutableArray.Create(cat, cat),
            new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default),
            ImmutableArray<ITypeParameterSymbol>.Empty,
            out var unresolved);

        Assert.True(unresolved);
    }

    [Fact]
    public void ResolveNames_OverrideOnClosedStandIn_MatchesCaseThatClosesOverRootsOwnTypeParameter()
    {
        // [UnionCaseName<Some<int>>("Some")] on Maybe<T> : IUnion<Some<T>, None> - the override
        // names a closed stand-in (Some<int>) because an attribute type argument cannot mention the
        // root's own <T>, but the actual case type arrives as the open Some<T>. Some<T> mentions the
        // root's own type parameter, so the override must still apply.
        //
        // Both symbols are resolved from one compilation (as the real generator sees them:
        // ReadCaseNameOverrides and BuildUnionInfo both read off the same root symbol) so
        // SymbolEqualityComparer identity is meaningful between them.
        var (someOfT, someOfIntOverrideKey, rootTypeParameters) = ResolveOverrideScenario(
            "public abstract partial record Maybe<T> : IUnion<Some<T>, None>;",
            "Maybe`1", // GetTypeByMetadataName needs the CLR arity suffix for a generic type.
            "[UnionCaseName<Some<int>>(\"unused\")]");

        var overrides = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default)
        {
            [someOfIntOverrideKey] = "Some"
        };

        var names = UnionCaseNaming.ResolveNames(
            ImmutableArray.Create(someOfT), overrides, rootTypeParameters, out var unresolved);

        Assert.False(unresolved);
        Assert.Equal(["Some"], names);
    }

    [Fact]
    public void ResolveNames_OverrideForOneClosedGeneric_DoesNotLeakToADifferentClosedGeneric()
    {
        // [UnionCaseName<Some<int>>("IntCase")] on a root with IUnion<Some<int>, Some<string>> must
        // rename only the Some<int> wrapper. Some<int> and Some<string> share one original
        // definition (Some<>) but neither mentions any root type parameter (the root here is
        // non-generic), so the override must match by exact identity only - never leak across two
        // distinct fully-closed cases.
        const string source = """
            using Corsinvest.Fx.Functional;

            public record Some<T>(T Value);

            [UnionCaseName<Some<int>>("unused")]
            public abstract partial record Mixed : IUnion<Some<int>, Some<string>>;
            """;

        var compilation = CreateProbeCompilation(source);
        var root = compilation.GetTypeByMetadataName("Mixed")!;
        var marker = root.Interfaces.Single(i => i.Name == "IUnion");
        var someOfInt = marker.TypeArguments[0];
        var someOfString = marker.TypeArguments[1];

        var overrideKey = root.GetAttributes()
            .Single(a => a.AttributeClass!.Name == "UnionCaseNameAttribute")
            .AttributeClass!.TypeArguments[0];

        var overrides = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default)
        {
            [overrideKey] = "IntCase"
        };

        var names = UnionCaseNaming.ResolveNames(
            ImmutableArray.Create(someOfInt, someOfString),
            overrides,
            root.TypeParameters,
            out var unresolved);

        Assert.False(unresolved);
        Assert.Equal("IntCase", names[0]);
        // The override pins Some<int>; Some<string> falls back to the argument-qualified
        // form, which is what separates two constructions of one generic definition.
        Assert.Equal("SomeOfString", names[1]);
    }

    /// <summary>
    /// Builds one compilation containing a generic root, its <c>[UnionCaseName&lt;...&gt;]</c>
    /// override, and <c>Some&lt;T&gt;</c>, then resolves the case type exactly as the generator
    /// sees it (via <c>IUnion&lt;...&gt;</c>'s type arguments) alongside the override attribute's
    /// own type argument - so both symbols come from the same compilation, matching what
    /// <c>BuildUnionInfo</c>/<c>ReadCaseNameOverrides</c> actually compare.
    /// </summary>
    /// <param name="rootDeclaration">The root's declaration line, e.g. <c>public abstract partial record Maybe&lt;T&gt; : IUnion&lt;Some&lt;T&gt;, None&gt;;</c>.</param>
    /// <param name="rootName">The root's simple name, to look it up after compiling.</param>
    /// <param name="overrideAttribute">The <c>[UnionCaseName&lt;...&gt;]</c> attribute line to place above the root.</param>
    private static (ITypeSymbol CaseType, ITypeSymbol OverrideKey, ImmutableArray<ITypeParameterSymbol> RootTypeParameters)
        ResolveOverrideScenario(string rootDeclaration, string rootName, string overrideAttribute)
    {
        var source = $$"""
            using Corsinvest.Fx.Functional;

            public record Some<T>(T Value);
            public record None;

            {{overrideAttribute}}
            {{rootDeclaration}}
            """;

        var compilation = CreateProbeCompilation(source);

        var root = compilation.GetTypeByMetadataName(rootName)!;
        var marker = root.Interfaces.Single(i => i.Name == "IUnion");
        var overrideKey = root.GetAttributes()
            .Single(a => a.AttributeClass!.Name == "UnionCaseNameAttribute")
            .AttributeClass!.TypeArguments[0];

        return (marker.TypeArguments[0], overrideKey, root.TypeParameters);
    }

    private static CSharpCompilation CreateProbeCompilation(string source)
        => CSharpCompilation.Create(
            "GenericCaseProbe",
            [CSharpSyntaxTree.ParseText(source)],
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .Append(MetadataReference.CreateFromFile(typeof(UnionCaseNaming).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static ITypeSymbol ResolveType(string typeExpression)
    {
        var source = $$"""
            namespace Farm { public record Cat(string Name); }
            namespace Wild { public record Cat(string Species); }
            public record Cat(string Name);

            public class Probe { public {{typeExpression}} Field = default!; }
            """;

        var compilation = CSharpCompilation.Create(
            "NamingProbe",
            [CSharpSyntaxTree.ParseText(source)],
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var probe = compilation.GetTypeByMetadataName("Probe")!;

        return ((IFieldSymbol)probe.GetMembers("Field").Single()).Type;
    }
}
