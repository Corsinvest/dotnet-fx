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
    [InlineData("System.Collections.Generic.List<string>", "ListOfString")]
    [InlineData("System.Collections.Generic.Dictionary<string, int>", "DictionaryOfStringInt32")]
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
            ImmutableArray.Create(cat), overrides, out var unresolved);

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
            out var unresolved);

        Assert.True(unresolved);
    }

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
