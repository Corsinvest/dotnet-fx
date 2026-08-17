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

    // ---- ported from the deleted GenericUnionGeneratorTests.cs (attribute form) --------------
    //
    // These cover generator logic that is unchanged by the attribute-to-interface pivot
    // (GenerateMatch, GetCanonicalClrType/UNION009, UNION012, the hint-name/nesting regressions,
    // and the decoy-attribute-namespace guard) - only the union declarations below were converted
    // from `[Union<...>]` to `: IUnion<...>`. Two tests from the original file are NOT ported; see
    // the note above GenericRoot_DefaultWrapperNames_NoLongerShadowTheCaseType_UnlikeTheAttributeForm
    // below for why.

    [Fact]
    public void Generates_ImplicitConversion_PerCaseType()
    {
        var generated = Generate($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;
            """);

        Assert.Contains("public static implicit operator Pet(global::Cat value)", generated);
        Assert.Contains("public static implicit operator Pet(global::Dog value)", generated);
    }

    [Fact]
    public void Generates_MatchThatHandsOverTheCaseType_NotTheWrapper()
    {
        var generated = Generate($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;
            """);

        Assert.Contains("Func<global::Cat, TResult> onCat", generated);
        Assert.Contains("Cat wrapped => onCat(wrapped.Value)", generated);
    }

    // ============================================
    // Restored members: void MatchAsync + {Root}UnionExtensions on Task<TRoot>
    // ============================================
    // These two members (GenerateMatch's void-returning MatchAsync overload, and
    // GenerateUnionExtensions' Task<TRoot> companion class) were emitted by the retired
    // attribute-driven generator but dropped in c5f2ce2 when generation moved onto IUnion<...>.
    // ResultOf<T,E> temporarily patched the gap with hand-written extensions
    // (ResultOfExtensions.cs) until the generator grew them back; these tests pin the restored
    // shape directly against the generator's own output for both a non-generic and a generic root,
    // so a future regression is caught here rather than only via ResultOf's hand-written-turned-
    // generated call sites.

    [Fact]
    public void Generates_VoidMatchAsync_ForNonGenericRoot()
    {
        var generated = Generate($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;
            """);

        Assert.Contains("public async Task MatchAsync(", generated);
        Assert.Contains("Func<global::Cat, Task> onCat", generated);
        Assert.Contains("Func<global::Dog, Task> onDog", generated);
        Assert.Contains("case Cat wrapped: await onCat(wrapped.Value); break;", generated);
        Assert.Contains("case Dog wrapped: await onDog(wrapped.Value); break;", generated);
        Assert.Contains("default: throw new InvalidOperationException(\"Invalid union state\");", generated);
    }

    [Fact]
    public void Generates_UnionExtensionsClass_ForNonGenericRoot()
    {
        var generated = Generate($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;
            """);

        Assert.Contains("public static class PetUnionExtensions", generated);

        // Async handlers returning Task<TResult>.
        Assert.Contains("public static async Task<TResult> MatchAsync<TResult>(", generated);
        Assert.Contains("this Task<Pet> task,", generated);
        Assert.Contains("Func<global::Cat, Task<TResult>> onCat", generated);

        // Async handlers returning Task (void).
        Assert.Contains("public static async Task MatchAsync(", generated);
        Assert.Contains("Func<global::Cat, Task> onCat", generated);

        // Sync handlers returning TResult.
        Assert.Contains("Func<global::Cat, TResult> onCat", generated);
    }

    [Fact]
    public void GeneratedVoidMatchAsyncAndUnionExtensions_CompileAndDispatch_ForNonGenericRoot()
    {
        // Not just text-matching: the restored members must actually compile and dispatch to the
        // right handler, including through the Task<TRoot> extension.
        var diagnostics = CompileWithGenerator($$"""
            using Corsinvest.Fx.Functional;
            using System.Threading.Tasks;

            public record Cat(string Name, int Lives);
            public record Dog(string Name);

            public abstract partial record Pet : IUnion<Cat, Dog>;

            public static class Usage
            {
                public static async Task<string> DescribeAsync(Pet pet)
                {
                    string result = "";
                    await pet.MatchAsync(
                        async cat => { await Task.Delay(1); result = $"cat:{cat.Name}"; },
                        async dog => { await Task.Delay(1); result = $"dog:{dog.Name}"; }
                    );
                    return result;
                }

                public static Task<string> DescribeViaTaskExtensionAsync(Task<Pet> petTask)
                    => petTask.MatchAsync(
                        cat => $"cat:{cat.Name}",
                        dog => $"dog:{dog.Name}"
                    );
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_VoidMatchAsyncAndUnionExtensions_ForGenericRoot()
    {
        // The generic-root shape matters here: both members must thread the root's own type
        // parameters (T, E) through correctly, plus TResult where the overload needs it.
        var generated = Generate("""
            using Corsinvest.Fx.Functional;

            public record Good<T>(T Value);
            public record Bad<E>(E Value);

            public abstract partial record Res<T, E> : IUnion<Good<T>, Bad<E>>;
            """);

        // Wrapper names are derived from each case type's simple name (UnionCaseNaming): the open
        // generic Good<T>/Bad<E> - T and E being the root's own type parameters - derive to
        // GoodOfT/BadOfE rather than plain Good/Bad, since nothing here overrides them via
        // [UnionCaseName<...>].

        // Instance void MatchAsync, generic over the root's own T/E.
        Assert.Contains("public async Task MatchAsync(", generated);
        Assert.Contains("Func<global::Good<T>, Task> onGoodOfT", generated);
        Assert.Contains("Func<global::Bad<E>, Task> onBadOfE", generated);
        Assert.Contains("case GoodOfT wrapped: await onGoodOfT(wrapped.Value); break;", generated);
        Assert.Contains("case BadOfE wrapped: await onBadOfE(wrapped.Value); break;", generated);

        // ResUnionExtensions class, generic over <T, E> (plus TResult for the two overloads that
        // need it), operating on Task<Res<T, E>>.
        Assert.Contains("public static class ResUnionExtensions", generated);
        Assert.Contains("public static async Task<TResult> MatchAsync<T, E, TResult>(", generated);
        Assert.Contains("this Task<Res<T, E>> task,", generated);
        Assert.Contains("public static async Task MatchAsync<T, E>(", generated);
        Assert.Contains("Func<global::Good<T>, Task<TResult>> onGoodOfT", generated);
        Assert.Contains("Func<global::Good<T>, TResult> onGoodOfT", generated);
    }

    [Fact]
    public void Reports_UNION008_WhenTwoCasesCannotBeNamedApart()
    {
        var diagnostics = GetGeneratorDiagnostics("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);

            public abstract partial record Pet : IUnion<Cat, Cat>;
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

            [UnionCaseName<(int X, int Y)>("Point")]
            [UnionCaseName<(int Row, int Col)>("Cell")]
            public abstract partial record Geo : IUnion<(int X, int Y), (int Row, int Col)>;
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

            [UnionCaseName<(int X, (int A, int B) Y)>("Outer")]
            [UnionCaseName<(int Row, (int C, int D) Col)>("Inner")]
            public abstract partial record Nested
                : IUnion<(int X, (int A, int B) Y), (int Row, (int C, int D) Col)>;
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

            public abstract partial record Mixed : IUnion<(int, int), (int, string)>;
            """;

        var diagnostics = GetGeneratorDiagnostics(source);
        Assert.Empty(diagnostics.Where(d => d.Id == "UNION009"));

        var generated = Generate(source);
        Assert.Contains("implicit operator", generated);
    }

    [Fact]
    public void Reports_UNION012_WhenACaseTypeIsAnInterface()
    {
        var diagnostics = GetGeneratorDiagnostics("""
            using Corsinvest.Fx.Functional;

            public interface IShape;
            public record Cat(string Name);

            public abstract partial record Pet : IUnion<IShape, Cat>;
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION012"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("IShape", diagnostic.GetMessage());
    }

    // ---- Regression coverage: namespaced / nested union roots, and attribute look-alikes -------
    //
    // Two of the four blockers fixed in the original attribute-based generator (bare-name hint
    // collisions, and a nested root producing a phantom top-level type) survived eleven prior
    // commits and every per-task review because no existing test put a union root inside a
    // namespace or another type. These tests close that gap for the interface path too; do not
    // remove them.

    [Fact]
    public void NamespacedRoot_CompilesEndToEnd_WithSwitchAndImplicitConversion()
    {
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            namespace Billing
            {
                public record Cat(string Name);
                public record Dog(string Name);

                public abstract partial record Pet : IUnion<Cat, Dog>;

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

                public abstract partial record Pet : IUnion<Cat, Dog>;
            }

            namespace Shipping
            {
                public record Cat(string Name);
                public record Dog(string Name);

                public abstract partial record Pet : IUnion<Cat, Dog>;
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
                public abstract partial record Pet : IUnion<Cat, Dog>;
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
    public void NestedRoot_InsideGenericContainingType_CompilesEndToEnd()
    {
        // Regression for a code-review finding on the Task 6 restoration of MatchAsync/
        // {Root}UnionExtensions: both used to build the containing-type chain from the ancestor's
        // bare name only, dropping its own type parameters, so a root nested inside Outer<T>
        // referenced plain "Outer.Pet" instead of "Outer<T>.Pet" - CS0305 ("Outer<T> requires 1
        // type argument(s)") the moment either restored member's Task<Outer.Pet> tried to compile.
        // This exercises both restored members, not just the wrapper (which never had this bug).
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;
            using System.Threading.Tasks;

            public record Cat(string Name);
            public record Dog(string Name);

            public partial class Outer<T>
            {
                public abstract partial record Pet : IUnion<Cat, Dog>;
            }

            public static class Usage
            {
                public static async Task<string> DescribeAsync(Outer<int>.Pet pet)
                {
                    string result = "";
                    await pet.MatchAsync(
                        async cat => { await Task.Delay(1); result = cat.Name; },
                        async dog => { await Task.Delay(1); result = dog.Name; }
                    );
                    return result;
                }

                public static Task<string> DescribeViaTaskExtensionAsync(Task<Outer<int>.Pet> petTask)
                    => petTask.MatchAsync(
                        cat => cat.Name,
                        dog => dog.Name
                    );
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void TwoSameNamedRoots_InDifferentContainingTypes_UnionExtensionsClassesDoNotCollide()
    {
        // Regression for the same code-review finding: the {Root}UnionExtensions class is a
        // top-level static class (never nested, unlike the wrapper types), so two roots both named
        // Pet, nested in two different containing types in the same namespace, would both try to
        // emit a top-level PetUnionExtensions and collide on CS0101 unless the class name is
        // disambiguated by the containing-type chain. OuterA and OuterB have the same arity (both
        // non-generic) so this also confirms the containing type's own *name*, not just arity,
        // feeds the disambiguation - GetArity alone would not be enough to tell them apart.
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;
            using System.Threading.Tasks;

            public record Cat(string Name);
            public record Dog(string Name);

            public partial class OuterA
            {
                public abstract partial record Pet : IUnion<Cat, Dog>;
            }

            public partial class OuterB
            {
                public abstract partial record Pet : IUnion<Cat, Dog>;
            }

            public static class Usage
            {
                public static Task<string> DescribeA(Task<OuterA.Pet> petTask)
                    => petTask.MatchAsync(cat => cat.Name, dog => dog.Name);

                public static Task<string> DescribeB(Task<OuterB.Pet> petTask)
                    => petTask.MatchAsync(cat => cat.Name, dog => dog.Name);
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS0101");
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

                [Evil.UnionCaseName<Cat>("ShouldNotApply")]
                public abstract partial record Pet : IUnion<Cat, Dog>;
            }
            """);

        // The decoy attribute must not rename the Cat case: only the real
        // Corsinvest.Fx.Functional.UnionCaseNameAttribute<T> should be honoured.
        Assert.Contains("public sealed partial record Cat(", generated);
        Assert.DoesNotContain("ShouldNotApply", generated);
    }

    // ---- Tests from the deleted file that could NOT be ported, and why -----------------------
    //
    // GenericRoot_WrapperNameCollidingWithCaseTypeName_FailsToCompile pinned down a
    // generator-independent C# name-resolution bug: [Union<Cat, Dog>] on a generic Box<T> put the
    // attribute's own type-argument list in the SAME scope as the merged partial declaration's
    // nested wrapper, so a wrapper named "Cat" (the common case - a case type's default-derived
    // wrapper name equals its own simple name) shadowed the top-level Cat for the attribute's own
    // lookup, and the compiler rejected it with CS8968. UNION010 existed to warn about exactly
    // this under the attribute form.
    //
    // With `: IUnion<Cat, Dog>` there is no attribute type-argument list to shadow: IUnion<...>'s
    // type arguments are resolved once, at the base-list position, before any nested wrapper type
    // exists. Reproducing the same shape here does NOT fail to compile - confirmed below - so the
    // regression test for "fails to compile" has no interface-form equivalent: there is nothing
    // left to pin down.
    //
    // UNION010 (GenericRootNameShadowingDescriptor and the loop that reported it in
    // UnionGenerator.GenerateUnion) has been removed entirely, because under the interface form
    // its firing condition (a generic root's wrapper name equal to its case type's simple name)
    // no longer indicates a real problem - the shape below compiles clean, 0 warnings, 0 errors -
    // yet the diagnostic would still have fired on it as a false positive. Left in place, it would
    // have raised a false warning on Option<T> itself once Option<T> migrates to
    // [UnionCaseName<Some<int>>("Some")], which the build treats as an error via
    // TreatWarningsAsErrors. This test remains as the regression proof that removing the
    // diagnostic is safe: the shape below must compile cleanly and must NOT produce a UNION010
    // diagnostic (which can no longer exist).
    [Fact]
    public void GenericRoot_DefaultWrapperNames_NoLongerShadowTheCaseType_UnlikeTheAttributeForm()
    {
        // The exact shape that produced CS8968 under [Union<Cat, Dog>] on Box<T>. Under
        // : IUnion<Cat, Dog> it compiles, and there is no UNION010 diagnostic anymore to false-fire
        // on it - the diagnostic was removed (see the note above).
        var diagnostics = CompileWithGenerator("""
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);
            public record Dog(string Name);

            public abstract partial record Box<T> : IUnion<Cat, Dog>;

            public static class Usage
            {
                public static string Describe<T>(Box<T> box) => box switch
                {
                    Box<T>.Cat(var cat) => cat.Name,
                    Box<T>.Dog(var dog) => dog.Name
                };

                public static Box<int> Make() => new Cat("Whiskers");
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.DoesNotContain(diagnostics, d => d.Id == "UNION010");
    }

    [Fact]
    public void Analyzer_ReportsMissingCase_OnIUnionRoot()
    {
        var diagnostics = CompileWithGenerator($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;

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
    public void Suppressor_SuppressesCS8509_OnCompleteIUnionSwitch()
    {
        var diagnostics = CompileWithGenerator($$"""
            {{Cases}}

            public abstract partial record Pet : IUnion<Cat, Dog>;

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
        => string.Join("\n", RunGenerator(source).Select(t => t.ToString()));

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
            "UnionTest",
            [CSharpSyntaxTree.ParseText(source)],
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .Append(MetadataReference.CreateFromFile(typeof(UnionCaseNaming).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
