using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Covers generation from <c>[Union&lt;...&gt;]</c> over external case types.
/// </summary>
public class GenericUnionGeneratorTests
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

            [Union<Cat, Dog>]
            public abstract partial record Pet;
            """);

        Assert.Contains("public sealed partial record Cat(global::Cat Value) : Pet;", generated);
        Assert.Contains("public sealed partial record Dog(global::Dog Value) : Pet;", generated);
    }

    [Fact]
    public void Generates_PrivateConstructor_ToCloseTheHierarchy()
    {
        var generated = Generate($$"""
            {{Cases}}

            [Union<Cat, Dog>]
            public abstract partial record Pet;
            """);

        Assert.Contains("private Pet() { }", generated);
    }

    [Fact]
    public void Generates_ImplicitConversion_PerCaseType()
    {
        var generated = Generate($$"""
            {{Cases}}

            [Union<Cat, Dog>]
            public abstract partial record Pet;
            """);

        Assert.Contains("public static implicit operator Pet(global::Cat value)", generated);
        Assert.Contains("public static implicit operator Pet(global::Dog value)", generated);
    }

    [Fact]
    public void Generates_MatchThatHandsOverTheCaseType_NotTheWrapper()
    {
        var generated = Generate($$"""
            {{Cases}}

            [Union<Cat, Dog>]
            public abstract partial record Pet;
            """);

        Assert.Contains("Func<global::Cat, TResult> onCat", generated);
        Assert.Contains("Cat wrapped => onCat(wrapped.Value)", generated);
    }

    [Fact]
    public void GeneratedCode_Compiles()
    {
        var diagnostics = CompileWithGenerator($$"""
            {{Cases}}

            [Union<Cat, Dog>]
            public abstract partial record Pet;

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
    public void SameCaseType_CanBelongToTwoUnions()
    {
        var diagnostics = CompileWithGenerator($$"""
            {{Cases}}

            public record Cow(string Name);

            [Union<Cat, Dog>]
            public abstract partial record Pet;

            [Union<Cat, Cow>]
            public abstract partial record Animal;

            public static class Usage
            {
                public static (Pet, Animal) Both()
                    => (new Cat("x", 9), new Cat("x", 9));
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void ValueTypeCases_AreStoredWithoutBoxing()
    {
        var generated = Generate("""
            using Corsinvest.Fx.Functional;

            [Union<int, string>]
            public abstract partial record Value;
            """);

        // The wrapper holds a typed int field rather than object.
        Assert.Contains("record Int32(global::System.Int32 Value)", generated);
        Assert.DoesNotContain("object Value", generated);
    }

    // NOTE ON THE "Option<T> SHAPE": the reviewer's suggested sketch —
    // `[Union<Some<T>, None>] public abstract partial record Maybe<T>;`, where a case type
    // closes over the *root's own* type parameter — does not compile in C# at all, independently
    // of this generator. The compiler rejects it with CS8968 ("attribute type argument cannot use
    // type parameters"): attribute type arguments are resolved before the type they decorate is
    // bound, so a case type cannot mention the root's own <T>. The same error fires for a
    // hand-written, non-generated `[MyAttr<T>]` on any `Container<T>` — confirmed with a minimal
    // repro outside this repo. Option<T>/ResultOf<T,E> cannot migrate to
    // `[Union<Some<T>, None>]` on `Option<T>` as literally sketched; that is a design question for
    // whoever writes the Task 6/7 brief, not something this generator can work around. Flagged
    // back to the coordinator; see the task-3 report for the full writeup.
    //
    // SECOND, RELATED BUG FOUND WHILE BUILDING THIS TEST: a generic root can ONLY compile when
    // every case type's wrapper name differs from the case type's own simple name. When a case
    // type's default-resolved wrapper name equals the case type's own name (the common case -
    // e.g. case type `Cat` naturally gets wrapper name `Cat`), the generated nested type
    // `Box<T>.Cat` shadows the top-level `Cat` for name lookup once the partial declarations are
    // merged - and since attribute type arguments on `Box<T>` are resolved against `Box<T>`'s own
    // scope, `[Union<Cat, Dog>]` ends up resolving `Cat` to the *nested* `Box<T>.Cat`, which is
    // itself open over T, hitting CS8968 for a second, unrelated reason. This reproduces
    // identically with fully hand-written code (no generator involved) - see
    // GenericRoot_WrapperNameCollidingWithCaseTypeName_FailsToCompile below, which pins the bug
    // down as a regression test. It affects ONLY generic roots; the identical shape on a
    // non-generic root (as in GeneratedCode_Compiles above) compiles fine, because there is no
    // open type parameter for the shadowed reference to drag in.
    //
    // Consequently the only way to get a *compiling* generic-root test today is to force distinct
    // wrapper names via [UnionCaseName<T>], which is exactly what the two tests below do. This is
    // a real, generator-independent limitation of attribute-driven unions on generic roots that
    // Tasks 4 (diagnostics) and 6/7 (Option<T>/ResultOf<T,E> migration) need to know about.
    [Fact]
    public void GenericRoot_CarriesTypeParameters_ThroughGeneratedMembers()
    {
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);
            public record Dog(string Name);

            [Union<Cat, Dog>]
            [UnionCaseName<Cat>("CatCase")]
            [UnionCaseName<Dog>("DogCase")]
            public abstract partial record Box<T>;

            public static class Usage
            {
                public static string Describe<T>(Box<T> box) => box switch
                {
                    Box<T>.CatCase(var cat) => cat.Name,
                    Box<T>.DogCase(var dog) => dog.Name
                };

                public static Box<int> Make() => new Cat("Whiskers");
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GenericRoot_Wrappers_And_Conversions_CarryTypeParameter()
    {
        // Verifies the actual generated text, not just that compilation succeeds: the wrapper
        // declarations, base clause, and implicit conversion operator must all mention <T>.
        var generated = Generate("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);
            public record Dog(string Name);

            [Union<Cat, Dog>]
            [UnionCaseName<Cat>("CatCase")]
            [UnionCaseName<Dog>("DogCase")]
            public abstract partial record Box<T>;
            """);

        Assert.Contains("public abstract partial record Box<T>", generated);
        Assert.Contains(": Box<T>;", generated);
        Assert.Contains("public static implicit operator Box<T>(global::Cat value)", generated);
        Assert.Contains("public static implicit operator Box<T>(global::Dog value)", generated);
    }

    [Fact]
    public void GenericRoot_WrapperNameCollidingWithCaseTypeName_FailsToCompile()
    {
        // Regression test pinning down the shadowing bug described in the comment block above.
        // This is a fundamental C# name-resolution behaviour (reproduces with zero generated code
        // involved), not something GenerateGenericUnion's emitted shape can avoid - documented
        // here so it is never mistaken for a new regression. If this test ever starts failing
        // because the diagnostic disappears, that is good news: re-evaluate whether generic-root
        // unions with default-named wrappers have become possible.
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);
            public record Dog(string Name);

            [Union<Cat, Dog>]
            public abstract partial record Box<T>;
            """);

        Assert.Contains(diagnostics, d => d.Id == "CS8968");
    }

    [Fact]
    public void ClosedGenericCaseType_OnNonGenericRoot_Compiles()
    {
        // A closed generic case type (Some<int>) is a different shape from a generic root: the
        // root has no type parameters of its own here.
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            public record Some<T>(T Value);
            public record None;

            [Union<Some<int>, None>]
            public abstract partial record MaybeInt;

            public static class Usage
            {
                public static int Unwrap(MaybeInt maybe) => maybe switch
                {
                    MaybeInt.SomeOfInt32(var some) => some.Value,
                    MaybeInt.None => 0
                };
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Arity8_RegistrationWiring_Compiles()
    {
        // Proves the UnionAttribute`8 metadata-name registration is actually wired to the
        // arity-8 attribute (a transposed digit here would otherwise be silently invisible).
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            public record C1;
            public record C2;
            public record C3;
            public record C4;
            public record C5;
            public record C6;
            public record C7;
            public record C8;

            [Union<C1, C2, C3, C4, C5, C6, C7, C8>]
            public abstract partial record Eight;

            public static class Usage
            {
                public static int Describe(Eight value) => value switch
                {
                    Eight.C1 => 1,
                    Eight.C2 => 2,
                    Eight.C3 => 3,
                    Eight.C4 => 4,
                    Eight.C5 => 5,
                    Eight.C6 => 6,
                    Eight.C7 => 7,
                    Eight.C8 => 8
                };

                public static Eight Make() => new C1();
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Reports_UNION008_WhenTwoCasesCannotBeNamedApart()
    {
        var diagnostics = GetGeneratorDiagnostics("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);

            [Union<Cat, Cat>]
            public abstract partial record Pet;
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION008"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Reports_UNION009_AndOmitsConversions_WhenCasesShareOneClrType()
    {
        // (int X, int Y) and (int Row, int Col) are both ValueTuple<int,int>.
        var source = """
            using Corsinvest.Fx.Functional;

            [Union<(int X, int Y), (int Row, int Col)>]
            [UnionCaseName<(int X, int Y)>("Point")]
            [UnionCaseName<(int Row, int Col)>("Cell")]
            public abstract partial record Geo;
            """;

        var diagnostics = GetGeneratorDiagnostics(source);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION009"));

        // The message must name the colliding cases, not just the union, so a union with several
        // cases doesn't leave the developer hunting for which pair actually collides.
        Assert.Contains("Point", diagnostic.GetMessage());
        Assert.Contains("Cell", diagnostic.GetMessage());

        // Without conversions the generated code still compiles.
        var generated = Generate(source);
        Assert.DoesNotContain("implicit operator", generated);
    }

    [Fact]
    public void Reports_UNION009_WhenNestedTuplesShareOneClrTypeAfterElementNamesAreStripped()
    {
        // (int X, (int A, int B) Y) and (int Row, (int C, int D) Col) both erase to
        // ValueTuple<int, ValueTuple<int, int>>: TupleUnderlyingType alone only strips element
        // names at the outer level, leaving the inner tuples' names intact, so this only collides
        // once canonicalization recurses into nested type arguments.
        var source = """
            using Corsinvest.Fx.Functional;

            [Union<(int X, (int A, int B) Y), (int Row, (int C, int D) Col)>]
            [UnionCaseName<(int X, (int A, int B) Y)>("Outer")]
            [UnionCaseName<(int Row, (int C, int D) Col)>("Inner")]
            public abstract partial record Nested;
            """;

        var diagnostics = GetGeneratorDiagnostics(source);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION009"));
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Outer", diagnostic.GetMessage());
        Assert.Contains("Inner", diagnostic.GetMessage());

        // Without conversions the generated code still compiles (no colliding operator overloads).
        var generated = Generate(source);
        Assert.DoesNotContain("implicit operator", generated);
    }

    [Fact]
    public void DoesNotReport_UNION009_WhenTupleShapesAreGenuinelyDifferent()
    {
        // Guards against an over-eager canonicalization: (int, int) and (int, string) must keep
        // comparing as distinct CLR types, so conversions are still generated for both.
        var source = """
            using Corsinvest.Fx.Functional;

            [Union<(int, int), (int, string)>]
            public abstract partial record Mixed;
            """;

        var diagnostics = GetGeneratorDiagnostics(source);
        Assert.Empty(diagnostics.Where(d => d.Id == "UNION009"));

        var generated = Generate(source);
        Assert.Contains("implicit operator", generated);
    }

    [Fact]
    public void Reports_UNION010_WhenGenericRootWouldShadowItsCaseType()
    {
        var diagnostics = GetGeneratorDiagnostics("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);
            public record Dog(string Name);

            [Union<Cat, Dog>]
            public abstract partial record Box<T>;
            """);

        var reported = diagnostics.Where(d => d.Id == "UNION010").ToList();

        Assert.Equal(2, reported.Count);
        Assert.Contains(reported, d => d.GetMessage().Contains("Cat"));
        Assert.Contains(reported, d => d.GetMessage().Contains("Dog"));
    }

    [Fact]
    public void DoesNotReport_UNION010_WhenGenericRootUsesExplicitCaseNames()
    {
        // GenericRoot_CarriesTypeParameters_ThroughGeneratedMembers relies on these overrides to
        // compile at all (see the shadowing bug documented above Reports_UNION010...); confirm
        // that using [UnionCaseName<T>] to pick distinct names also suppresses the diagnostic.
        var diagnostics = GetGeneratorDiagnostics("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);
            public record Dog(string Name);

            [Union<Cat, Dog>]
            [UnionCaseName<Cat>("CatCase")]
            [UnionCaseName<Dog>("DogCase")]
            public abstract partial record Box<T>;
            """);

        Assert.Empty(diagnostics.Where(d => d.Id == "UNION010"));
    }

    [Fact]
    public void Reports_UNION012_WhenACaseTypeIsAnInterface()
    {
        var diagnostics = GetGeneratorDiagnostics("""
            using Corsinvest.Fx.Functional;

            public interface IShape;
            public record Cat(string Name);

            [Union<IShape, Cat>]
            public abstract partial record Pet;
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION012"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("IShape", diagnostic.GetMessage());
    }

    // ---- Regression coverage: namespaced / nested union roots, and attribute look-alikes -------
    //
    // Two of the four blockers fixed here (bare-name hint collisions, and a nested root producing
    // a phantom top-level type) survived eleven prior commits and every per-task review because no
    // existing test put a union root inside a namespace or another type. These tests close that
    // gap; do not remove them.

    [Fact]
    public void NamespacedRoot_CompilesEndToEnd_WithSwitchAndImplicitConversion()
    {
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            namespace Billing
            {
                public record Cat(string Name);
                public record Dog(string Name);

                [Union<Cat, Dog>]
                public abstract partial record Pet;

                public static class Usage
                {
                    public static string Describe(Pet pet) => pet switch
                    {
                        Pet.Cat(var cat) => cat.Name,
                        Pet.Dog(var dog) => dog.Name
                    };

                    public static Pet Make() => new Cat("Whiskers");
                }
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void TwoSameNamedRoots_InDifferentNamespaces_BothGenerate_NoHintNameCollision()
    {
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            namespace Billing
            {
                public record Cat(string Name);
                public record Dog(string Name);

                [Union<Cat, Dog>]
                public abstract partial record Pet;
            }

            namespace Shipping
            {
                public record Cat(string Name);
                public record Dog(string Name);

                [Union<Cat, Dog>]
                public abstract partial record Pet;
            }

            public static class Usage
            {
                public static string DescribeBilling(Billing.Pet pet) => pet switch
                {
                    Billing.Pet.Cat(var cat) => cat.Name,
                    Billing.Pet.Dog(var dog) => dog.Name
                };

                public static string DescribeShipping(Shipping.Pet pet) => pet switch
                {
                    Shipping.Pet.Cat(var cat) => cat.Name,
                    Shipping.Pet.Dog(var dog) => dog.Name
                };

                public static Billing.Pet MakeBilling() => new Billing.Cat("Whiskers");
                public static Shipping.Pet MakeShipping() => new Shipping.Cat("Tom");
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // CS8785: two generated files claimed the same hint name, so the generator dropped one of
        // them silently. This is the exact failure mode the bare-name hint (TypeName.Union.g.cs)
        // produced for two same-named roots in different namespaces.
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void NestedRoot_GeneratesInsideItsContainingType_AndCompiles()
    {
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);
            public record Dog(string Name);

            public partial class Outer
            {
                [Union<Cat, Dog>]
                public abstract partial record Pet;
            }

            public static class Usage
            {
                public static string Describe(Outer.Pet pet) => pet switch
                {
                    Outer.Pet.Cat(var cat) => cat.Name,
                    Outer.Pet.Dog(var dog) => dog.Name
                };

                public static Outer.Pet Make() => new Cat("Whiskers");
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void DecoyUnionCaseNameAttribute_FromAnotherNamespace_DoesNotRenameTheCase()
    {
        var generated = Generate("""
            using Corsinvest.Fx.Functional;

            namespace Evil
            {
                [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
                public sealed class UnionCaseNameAttribute<T> : System.Attribute
                {
                    public UnionCaseNameAttribute(string name) { }
                }
            }

            namespace App
            {
                public record Cat(string Name);
                public record Dog(string Name);

                [Union<Cat, Dog>]
                [Evil.UnionCaseName<Cat>("ShouldNotApply")]
                public abstract partial record Pet;
            }
            """);

        // The decoy attribute must not rename the Cat case: only the real
        // Corsinvest.Fx.Functional.UnionCaseNameAttribute<T> should be honoured.
        Assert.Contains("public sealed partial record Cat(", generated);
        Assert.DoesNotContain("ShouldNotApply", generated);
    }

    [Fact]
    public void Analyzer_ReportsMissingCase_OnGenericUnion()
    {
        var diagnostics = CompileWithGenerator($$"""
            {{Cases}}

            [Union<Cat, Dog>]
            public abstract partial record Pet;

            public static class Usage
            {
                public static string Describe(Pet pet) => pet switch
                {
                    Pet.Cat(var cat) => cat.Name,
                    _ => "?"
                };
            }
            """,
            withAnalyzers: true);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION004"));
        Assert.Contains("Dog", diagnostic.GetMessage());
    }

    [Fact]
    public void Suppressor_SuppressesCS8509_OnCompleteGenericUnionSwitch()
    {
        var diagnostics = CompileWithGenerator($$"""
            {{Cases}}

            [Union<Cat, Dog>]
            public abstract partial record Pet;

            public static class Usage
            {
                public static string Describe(Pet pet) => pet switch
                {
                    Pet.Cat(var cat) => cat.Name,
                    Pet.Dog(var dog) => dog.Name
                };
            }
            """,
            withAnalyzers: true);

        Assert.All(diagnostics.Where(d => d.Id == "CS8509"),
                   d => Assert.True(d.IsSuppressed));
    }

    // ---- infrastructure ----------------------------------------------------

    private static string Generate(string source)
    {
        var trees = RunGenerator(source);

        return string.Join("\n", trees.Select(t => t.ToString()));
    }

    private static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(string source)
        => CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGenerators(CreateCompilation(source))
            .GetRunResult()
            .Results
            .SelectMany(r => r.Diagnostics)
            .ToImmutableArray();

    private static ImmutableArray<Diagnostic> CompileWithGenerator(string source, bool withAnalyzers = false)
    {
        var compilation = CreateCompilation(source);

        CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        if (!withAnalyzers) { return output.GetDiagnostics(); }

        return output
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new UnionExhaustivenessAnalyzer(),
                new UnionExhaustivenessSuppressor()))
            .GetAllDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();
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
            "GenericUnionTest",
            [CSharpSyntaxTree.ParseText(source)],
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .Append(MetadataReference.CreateFromFile(typeof(UnionCaseNaming).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
