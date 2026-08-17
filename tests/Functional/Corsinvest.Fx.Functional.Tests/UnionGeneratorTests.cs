using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Covers generation from <c>IUnion&lt;...&gt;</c> over external case types.
/// </summary>
public class UnionGeneratorTests
{
    private const string Cases = """
        using Corsinvest.Fx.Functional;

        public record Cat(string Name, int Lives);
        public record Dog(string Name);
        """;

    [Fact]
    public void Generates_SealedWrapper_PerCaseType()
    {
        var generated = Generate($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;
            """);

        Assert.Contains("public sealed partial record Cat(global::Cat Value) : Pet;", generated);
        Assert.Contains("public sealed partial record Dog(global::Dog Value) : Pet;", generated);
    }

    [Fact]
    public void Generates_PrivateConstructor_ToCloseTheHierarchy()
    {
        var generated = Generate($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;
            """);

        Assert.Contains("private Pet() { }", generated);
    }

    [Fact]
    public void GeneratedCode_Compiles_AndSwitchWorks()
    {
        var diagnostics = CompileWithGenerator($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;

            public static class Usage
            {
                public static string Describe(Pet pet) => pet switch
                {
                    Pet.Cat(var cat) => $"{cat.Name} {cat.Lives}",
                    Pet.Dog(var dog) => dog.Name
                };

                public static Pet Make() => new Cat("Whiskers", 9);
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GenericRoot_WithCaseClosingOverItsOwnTypeParameter_Compiles()
    {
        // The shape no attribute form can express: the case type mentions the root's own T.
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            public record Some<T>(T Value);
            public record None;

            [UnionCaseName<Some<int>>("Some")]
            public abstract partial record Maybe<T> : IUnion<Some<T>, None>;

            public static class Usage
            {
                public static string Describe(Maybe<int> maybe) => maybe switch
                {
                    Maybe<int>.Some(var some) => some.Value.ToString(),
                    Maybe<int>.None => "none"
                };

                public static Maybe<int> Make() => new Some<int>(42);
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void CaseNameOverride_MatchesAcrossConstructedAndOpenGenerics()
    {
        // [UnionCaseName<T>] cannot mention the root's own type parameter (CS8968), so an override
        // for a case that closes over it must name a closed stand-in instead - Some<int> here.
        // The actual case type arriving from IUnion<Some<T>, None> is the open Some<T>. Both must
        // resolve to the same override by comparing original definitions.
        var generated = Generate("""
            using Corsinvest.Fx.Functional;

            public record Some<T>(T Value);
            public record None;

            [UnionCaseName<Some<int>>("Some")]
            public abstract partial record Maybe<T> : IUnion<Some<T>, None>;
            """);

        Assert.Contains("record Some(global::Some<T> Value)", generated);
        Assert.DoesNotContain("SomeOfT", generated);
    }

    [Fact]
    public void SameCaseType_CanBelongToTwoUnions()
    {
        var diagnostics = CompileWithGenerator($$"""
            {{Cases}}

            public record Cow(string Name);

            public abstract partial record Pet : IUnion<Cat, Dog>;
            public abstract partial record Animal : IUnion<Cat, Cow>;

            public static class Usage
            {
                public static (Pet, Animal) Both() => (new Cat("x", 9), new Cat("x", 9));
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void ValueTypeCases_AreStoredWithoutBoxing()
    {
        var generated = Generate("""
            using Corsinvest.Fx.Functional;

            public abstract partial record Value : IUnion<int, string>;
            """);

        Assert.Contains("record Int32(global::System.Int32 Value)", generated);
        Assert.DoesNotContain("object Value", generated);
    }

    [Fact]
    public void MixedCaseKinds_Compile()
    {
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            public sealed class Db { public int Code; }
            public enum Net { Timeout, Refused }
            public readonly struct Validation { public int Line { get; init; } }

            public abstract partial record AppError : IUnion<Db, Net, Validation, string>;
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void DoesNotGenerate_ForUnrelatedInterface()
    {
        var generated = Generate($$"""
            {{Cases}}

            public interface INotAUnion<T1, T2>;
            public abstract partial record NotAUnion : INotAUnion<Cat, Dog>;
            """);

        Assert.DoesNotContain("record NotAUnion", generated);
    }

    // ---- infrastructure ----------------------------------------------------

    private static string Generate(string source)
        => string.Join("\n", RunGenerator(source).Select(t => t.ToString()));

    private static ImmutableArray<Diagnostic> CompileWithGenerator(string source)
    {
        CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGeneratorsAndUpdateCompilation(CreateCompilation(source), out var output, out _);

        return output.GetDiagnostics();
    }

    private static ImmutableArray<SyntaxTree> RunGenerator(string source)
        => CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGenerators(CreateCompilation(source))
            .GetRunResult()
            .Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SyntaxTree)
            .ToImmutableArray();

    private static CSharpCompilation CreateCompilation(string source)
        => CSharpCompilation.Create(
            "UnionTest",
            [CSharpSyntaxTree.ParseText(source)],
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .Append(MetadataReference.CreateFromFile(typeof(UnionCaseNaming).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
