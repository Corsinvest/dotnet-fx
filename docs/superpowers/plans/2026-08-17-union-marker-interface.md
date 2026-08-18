# Union Marker Interface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace both union attributes with a marker interface `IUnion<T1..T8>`, so a union declares its cases as ordinary type arguments in its base list.

**Architecture:** A union root is an `abstract partial record` that implements `IUnion<C1..Cn>`. The generator finds roots by that interface, reads the case types straight off it, and emits one `sealed partial record` wrapper per case, each deriving from the root. Because the interface is part of the type declaration rather than metadata, a case may reference the root's own type parameters — which is exactly what `Option<T>` and `ResultOf<T,E>` need and what no attribute form can express.

**Tech Stack:** C# 12 / netstandard2.0 source generator, Roslyn 4.7.0, xunit.v3 4.0.0, .NET 8/9 targets.

**Spec:** This document. Every design fact below was confirmed by compiling a probe project during design; they are not assumptions.

## Global Constraints

- Generator project targets `netstandard2.0`: **collection expressions with `ImmutableArray<T>` produce `error CS9210`** — use `ImmutableArray.Create(...)` / `.ToImmutableArray()`.
- **CS1573**: in the generator project, a method documented with `<param>` needs a `<param>` for EVERY parameter, or the build fails.
- `TreatWarningsAsErrors=true` (`Directory.Build.props:18`); `EnforceExtendedAnalyzerRules=true` on the generator project. `CS1591` is in `NoWarn`, so missing XML docs do not fire.
- Tests run by executing the built assembly (`bin/Debug/net8.0/*.Tests.exe`), NOT `dotnet test`. Filters must start with `/`, e.g. `-filter "/*/*/UnionGeneratorTests/*"`.
- Always pass `-nodeReuse:false` to `dotnet build`; a lingering MSBuild node locks the generator DLL. If a build fails with "file in use", find the `/nodemode:2` MSBuild process and kill it.
- Diagnostics keep the `UNION###` prefix. UNION001–UNION009 are already allocated; new ones start at UNION010.

## Verified Design Facts

Each confirmed by compiling. Do not re-litigate:

1. `record Root<T,E> : IUnion<Ok<T>, Fail<E>>` compiles. The interface is part of the declaration, so `T`/`E` are in scope — no `CS8968`.
2. Roslyn returns the case types **already bound**: `root.Interfaces.Single(...).TypeArguments` yields `global::Ok<T>` and `global::Fail<E>` with the root's own type parameters substituted. No substitution logic is needed anywhere.
3. A generator can discover roots by interface alone, with no attribute, and correctly ignores types implementing a similarly-shaped decoy interface.
4. No clash with C# 15: theirs is `IUnion` (arity 0) in `System.Runtime.CompilerServices`; ours is `IUnion<T1..T8>` in `Corsinvest.Fx.Functional`. A type can implement both simultaneously.
5. Wrappers must **inherit** from the root (`sealed partial record Name(CaseType Value) : Root`). That is what makes a plain `switch` work and what lets the existing `UnionSymbolHelper.GetVariants` find them unchanged. A non-inheriting wrapper fails pattern matching with `CS8121`.
6. Any type works as a case: class, sealed class, record, struct, record struct, enum, interface, string, primitives, arrays, closed generics, tuples, delegates. Value-type cases live in typed fields, so nothing is boxed.
7. Two cases that collapse to one CLR type (e.g. `(int X,int Y)` and `(int Row,int Col)`) make duplicate implicit conversions illegal — `CS0557`.
8. The end-to-end shape runs: `ResultOf<T,E>`, `Option<T>` and a non-generic `Pet` all produced correct output through hand-written equivalents of the generated code.

## Naming Rules (normative, unchanged from the existing `UnionCaseNaming`)

1. Simple named type → its short name. `Cat` → `Pet.Cat`.
2. Colliding short names → prefix with the containing namespace. `Farm.Cat`/`Wild.Cat` → `FarmCat`/`WildCat`.
3. Closed generic → `Name + Of + args`. `Some<int>` → `SomeOfInt32`.
4. Array → element + `Array`. `int[]` → `Int32Array`.
5. Tuple → `TupleOf` + elements.
6. Primitives → CLR name. `int` → `Int32`, `string` → `String`.
7. `[UnionCaseName<T>("...")]` overrides everything.
8. A surviving collision reports UNION008.

**A case whose type argument is the root's own type parameter** (e.g. `Some<T>` on `Option<T>`) resolves by rule 3 to `SomeOfT`, which is wrong for the public API. Tasks 5 and 6 pin those names with `[UnionCaseName<T>]`.

## File Structure

**Modified:**
- `src/Functional/Corsinvest.Fx.Functional/UnionAttributeGeneric.cs` → replaced by `IUnion.cs` (interfaces) plus the retained `UnionCaseNameAttribute<T>`.
- `src/Functional/Corsinvest.Fx.Functional.Generators/UnionGenerator.cs` → single interface-driven path; both attribute paths removed.
- `src/Functional/Corsinvest.Fx.Functional.Generators/UnionSymbolHelper.cs` → `IsUnionRoot` tests for the interface.
- `src/Functional/Corsinvest.Fx.Functional/Option.cs`, `ResultOf.cs` and their extensions.
- `examples/`, all `tests/Functional/**/Union*.cs`, docs.

**Deleted:**
- `src/Functional/Corsinvest.Fx.Functional/UnionAttribute.cs`
- The nested-case and generic-attribute code paths in `UnionGenerator.cs`.

---

### Task 1: The IUnion marker interfaces

**Files:**
- Create: `src/Functional/Corsinvest.Fx.Functional/IUnion.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional/UnionAttributeGeneric.cs` (keep only `UnionCaseNameAttribute<T>`; delete the eight `UnionAttribute<...>` declarations)
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionAttributeTests.cs` (rewrite)

**Interfaces:**
- Produces: `Corsinvest.Fx.Functional.IUnion<T1>` … `IUnion<T1..T8>`, all `public interface`, empty. `UnionCaseNameAttribute<T>` survives unchanged from the previous work.

- [ ] **Step 1: Write the failing test**

Replace the contents of `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionAttributeTests.cs`:

```csharp
using System.Reflection;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Covers the union marker interfaces and the case-name override attribute.
/// </summary>
public class UnionMarkerTests
{
    [Fact]
    public void IUnion_ExistsForArities_One_To_Eight()
    {
        var assembly = typeof(UnionCaseNameAttribute<>).Assembly;

        for (var arity = 1; arity <= 8; arity++)
        {
            var name = $"Corsinvest.Fx.Functional.IUnion`{arity}";
            Assert.NotNull(assembly.GetType(name));
        }
    }

    [Fact]
    public void IUnion_IsAnEmptyMarker()
    {
        // The interface carries case types, not behaviour: a member would force every
        // union root to implement it.
        Assert.Empty(typeof(IUnion<,>).GetMembers());
    }

    [Fact]
    public void IUnion_DoesNotClashWithTheBclUnionInterface()
    {
        // C# 15 ships System.Runtime.CompilerServices.IUnion with arity 0; ours is generic,
        // so the metadata names differ and both can be referenced together.
        Assert.Equal("Corsinvest.Fx.Functional", typeof(IUnion<>).Namespace);
        Assert.True(typeof(IUnion<>).IsGenericTypeDefinition);
    }

    [Fact]
    public void UnionCaseNameAttribute_CarriesTheOverrideName()
    {
        var attribute = new UnionCaseNameAttribute<string>("Text");

        Assert.Equal("Text", attribute.Name);
    }

    [Fact]
    public void UnionAttribute_IsGone()
    {
        // Both attribute forms were removed in favour of the interface.
        var assembly = typeof(UnionCaseNameAttribute<>).Assembly;

        Assert.Null(assembly.GetType("Corsinvest.Fx.Functional.UnionAttribute"));
        Assert.Null(assembly.GetType("Corsinvest.Fx.Functional.UnionAttribute`2"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd K:/source/repos/OpenSource/CSharp/Corsinvest.Fx
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
```

Expected: FAIL — `IUnion<>` does not exist.

- [ ] **Step 3: Create the interfaces**

Create `src/Functional/Corsinvest.Fx.Functional/IUnion.cs`:

```csharp
namespace Corsinvest.Fx.Functional;

/// <summary>
/// Marks a partial record as a discriminated union whose cases are the supplied type arguments.
/// </summary>
/// <remarks>
/// <para>
/// The case types are ordinary standalone types, so the same type can take part in several
/// unions. The generator emits one sealed nested wrapper per case, deriving from the union root,
/// which keeps the hierarchy closed and lets a plain <c>switch</c> match on it.
/// </para>
/// <para>
/// Because the marker is part of the type's declaration rather than an attribute, a case may
/// reference the root's own type parameters - <c>Option&lt;T&gt; : IUnion&lt;Some&lt;T&gt;, None&gt;</c>.
/// An attribute cannot express that: its arguments are metadata, resolved before the decorated
/// type is bound.
/// </para>
/// <para>
/// Any type can be a case: classes, records, structs, enums, interfaces, primitives, arrays,
/// closed generics and tuples. Value-type cases are stored in typed fields, so nothing is boxed.
/// </para>
/// </remarks>
/// <typeparam name="T1">The first case type.</typeparam>
/// <example>
/// <code>
/// public record Cat(string Name);
/// public record Dog(string Name);
///
/// public abstract partial record Pet : IUnion&lt;Cat, Dog&gt;;
///
/// Pet pet = new Cat("Whiskers");
/// var name = pet switch
/// {
///     Pet.Cat(var cat) =&gt; cat.Name,
///     Pet.Dog(var dog) =&gt; dog.Name
/// };
/// </code>
/// </example>
public interface IUnion<T1>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
public interface IUnion<T1, T2>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
public interface IUnion<T1, T2, T3>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
public interface IUnion<T1, T2, T3, T4>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
/// <typeparam name="T5">The fifth case type.</typeparam>
public interface IUnion<T1, T2, T3, T4, T5>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
/// <typeparam name="T5">The fifth case type.</typeparam>
/// <typeparam name="T6">The sixth case type.</typeparam>
public interface IUnion<T1, T2, T3, T4, T5, T6>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
/// <typeparam name="T5">The fifth case type.</typeparam>
/// <typeparam name="T6">The sixth case type.</typeparam>
/// <typeparam name="T7">The seventh case type.</typeparam>
public interface IUnion<T1, T2, T3, T4, T5, T6, T7>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
/// <typeparam name="T5">The fifth case type.</typeparam>
/// <typeparam name="T6">The sixth case type.</typeparam>
/// <typeparam name="T7">The seventh case type.</typeparam>
/// <typeparam name="T8">The eighth case type.</typeparam>
public interface IUnion<T1, T2, T3, T4, T5, T6, T7, T8>;
```

Then edit `UnionAttributeGeneric.cs`: delete the eight `UnionAttribute<...>` classes, keep only `UnionCaseNameAttribute<T>`, and rename the file to `UnionCaseNameAttribute.cs`.

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/UnionMarkerTests/*"
```

Expected: 5/5 pass. Other suites will still fail to build at this point — that is expected until Task 2.

- [ ] **Step 5: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional/ tests/Functional/Corsinvest.Fx.Functional.Tests/UnionAttributeTests.cs
git commit -m "feat(functional): replace union attributes with IUnion marker interfaces"
```

---

### Task 2: Generator reads the interface

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/UnionGenerator.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/GenericUnionInfo.cs` → rename to `UnionInfo.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/UnionSymbolHelper.cs`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs` → rewrite as `UnionGeneratorTests.cs`

**Interfaces:**
- Consumes: `IUnion<...>` (Task 1), `UnionCaseNaming.ResolveNames` (already present).
- Produces: for a root `R` implementing `IUnion<C1..Cn>` with names `N1..Nn`:
  `public sealed partial record Ni(Ci Value) : R;`, `public bool IsNi { get; }`,
  `public bool TryGetNi(out Ci value)`, `public static implicit operator R(Ci value)`
  (only when all CLR types are distinct), `Match`, `Match(void)`, `MatchAsync`.
  `Match` handlers receive the **case type**, dispatched as `Ni wrapped => onNi(wrapped.Value)`.

**How to find roots:** the previous implementation used `ForAttributeWithMetadataName`, which has no interface equivalent. Use `CreateSyntaxProvider` with a cheap syntactic predicate (a `RecordDeclarationSyntax` with a non-empty `BaseList`) and do the real check in the transform: resolve the symbol and look for an interface named `IUnion` in namespace `Corsinvest.Fx.Functional`. One registration replaces the eight.

- [ ] **Step 1: Write the failing test**

Rewrite the generator test file. Keep the existing helper methods (`Generate`, `CompileWithGenerator`, `CreateCompilation`) — only the union declarations change:

```csharp
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
```

Delete the old `GenericUnionGeneratorTests.cs`.

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
```

Expected: FAIL — the generator still looks for attributes.

- [ ] **Step 3: Rewrite the generator's discovery and emission**

In `UnionGenerator.cs`, replace the whole `Initialize` body's provider registrations with one:

```csharp
        var unionRoots = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is RecordDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => BuildUnionInfo(ctx, ct))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(unionRoots, static (spc, info) => GenerateUnion(spc, info!));
```

Add the builder, which is where the design pays off — no substitution logic:

```csharp
    private const string UnionInterfaceName = "IUnion";
    private const string UnionNamespace = "Corsinvest.Fx.Functional";

    private static UnionInfo? BuildUnionInfo(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) is not INamedTypeSymbol root)
        {
            return null;
        }

        var marker = root.Interfaces.FirstOrDefault(
            i => i.Name == UnionInterfaceName
                 && i.ContainingNamespace?.ToDisplayString() == UnionNamespace);

        if (marker is null) { return null; }

        // Roslyn has already substituted the root's type parameters into the case types:
        // for Option<T> : IUnion<Some<T>, None> these arrive as Some<T> and None.
        var caseTypes = marker.TypeArguments;
        if (caseTypes.Length == 0) { return null; }

        var overrides = ReadCaseNameOverrides(root);
        var names = UnionCaseNaming.ResolveNames(caseTypes, overrides, out var hasNameCollision);

        var distinctClrTypes = caseTypes.Distinct(SymbolEqualityComparer.Default).Count();

        return new UnionInfo(
            @namespace: root.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : root.ContainingNamespace.ToDisplayString(),
            typeName: root.Name,
            typeParameters: root.TypeParameters.Length == 0
                ? string.Empty
                : "<" + string.Join(", ", root.TypeParameters.Select(p => p.Name)) + ">",
            caseTypes: caseTypes,
            caseNames: names,
            emitImplicitConversions: distinctClrTypes == caseTypes.Length,
            hasNameCollision: hasNameCollision,
            location: root.Locations.FirstOrDefault());
    }
```

Keep `ReadCaseNameOverrides`, `GenerateUnion`, `GenerateMatch`, `GenerateTryGet` and the
`FullyQualifiedNoKeywordsFormat` from the previous implementation — they are unchanged by this
pivot. Rename `GenericUnionInfo` to `UnionInfo` and `GenerateGenericUnion` to `GenerateUnion`,
deleting the old nested-record versions of those names.

Delete from `UnionGenerator.cs`: `UnionMustBePartialDescriptor`, `VariantMustBePartialDescriptor`,
`IsUnionCandidate`, `GetUnionGenerationContext`, `IsUnionAttribute`, `ProcessUnionGeneration`,
`GenerateUnionSource`, `GenerateVariant`, `GenerateUnionExtensions`, `GenerateMatchMethods`,
`GenerateAsyncMatchMethods`, `GenerateTryGetMethods`, and the `UnionInfo`/`VariantInfo`/`ParamInfo`/
`UnionGenerationContext` records that belonged to the nested path.

In `UnionSymbolHelper.cs`, replace `IsUnionRoot`:

```csharp
    public static bool IsUnionRoot(ITypeSymbol? type)
        => type is INamedTypeSymbol named
           && named.Interfaces.Any(i => i.Name == "IUnion"
                                        && i.ContainingNamespace?.ToDisplayString() == "Corsinvest.Fx.Functional");
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet build src/Functional/Corsinvest.Fx.Functional.Generators -v q --nologo -nodeReuse:false
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/UnionGeneratorTests/*"
```

Expected: 8/8 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Functional/ tests/Functional/
git commit -m "feat(generators): drive union generation from the IUnion interface"
```

---

### Task 3: Delete the old attribute and its diagnostics

**Files:**
- Delete: `src/Functional/Corsinvest.Fx.Functional/UnionAttribute.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/AnalyzerReleases.Unshipped.md`
- Delete: `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionGeneratorDiagnosticsTests.cs`

- [ ] **Step 1: Confirm nothing still references it**

```bash
grep -rn "\[Union\]\|\[Union<" --include="*.cs" . | grep -v obj/ | grep -v "/bin/"
```

Every hit must be in a file that Tasks 4-6 rewrite. If a hit is outside those files, fix it now.

- [ ] **Step 2: Delete**

```bash
git rm src/Functional/Corsinvest.Fx.Functional/UnionAttribute.cs
git rm tests/Functional/Corsinvest.Fx.Functional.Tests/UnionGeneratorDiagnosticsTests.cs
```

`UnionGeneratorDiagnosticsTests` covers UNION002 and UNION003, which only exist for the nested
model. Both rules are retired with it.

- [ ] **Step 3: Retire the rules**

In `AnalyzerReleases.Unshipped.md`, move UNION002 and UNION003 into a `### Removed Rules` section:

```
### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UNION002 | Design | Error | Union type must be partial
UNION003 | Design | Error | Union variant must be partial
```

- [ ] **Step 4: Verify**

```bash
dotnet build src/Functional/Corsinvest.Fx.Functional.Generators -v q --nologo -nodeReuse:false
```

Expected: clean. The library and tests will not build until Tasks 4-6 — that is expected.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor!: remove the nested-case Union attribute"
```

---

### Task 4: Migrate Option<T>

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional/Option.cs`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionCoreTypesTests.cs` (create)

**Interfaces:**
- Produces: `Corsinvest.Fx.Functional.None`, `Some<T>(T Value)`, and `Option<T>` with
  `Option<T>.Some` / `Option<T>.None` wrappers. `Match` hands `Some<T>` and `None` to its
  handlers, so existing `some.Value` call sites are unaffected.

- [ ] **Step 1: Write the failing test**

Create `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionCoreTypesTests.cs`:

```csharp
namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Locks the public shape of Option and ResultOf after the move to the IUnion marker.
/// </summary>
public class UnionCoreTypesTests
{
    [Fact]
    public void Option_Some_CarriesTheValue()
    {
        var option = Option.Some(42);

        Assert.True(option.IsSome);
        Assert.Equal(42, option.Match(some => some.Value, none => 0));
    }

    [Fact]
    public void Option_None_IsRecognised()
    {
        var option = Option.None<int>();

        Assert.True(option.IsNone);
        Assert.Equal(0, option.Match(some => some.Value, none => 0));
    }

    [Fact]
    public void Option_SupportsNativeSwitch()
    {
        Option<int> option = Option.Some(7);

        var result = option switch
        {
            Option<int>.Some(var some) => some.Value,
            Option<int>.None => 0
        };

        Assert.Equal(7, result);
    }

    [Fact]
    public void Option_Map_StillChains()
    {
        Assert.Equal(10, Option.Some(5).Map(x => x * 2).GetValueOr(0));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
```

Expected: FAIL — `Option<T>` still uses `[Union]`, which no longer exists.

- [ ] **Step 3: Rewrite the declaration**

In `src/Functional/Corsinvest.Fx.Functional/Option.cs`, replace the `[Union]` block with:

```csharp
/// <summary>Represents the absence of a value.</summary>
public sealed record None;

/// <summary>Represents a present value.</summary>
/// <typeparam name="T">The type of the value</typeparam>
/// <param name="Value">The contained value</param>
public sealed record Some<T>(T Value);

/// <summary>
/// Represents an optional value that may or may not be present.
/// </summary>
/// <typeparam name="T">The type of the optional value</typeparam>
/// <remarks>
/// A discriminated union with two cases, <see cref="Some{T}"/> and <see cref="None"/>, declared
/// through <see cref="IUnion{T1,T2}"/>. The cases are standalone types; the generated wrappers
/// <c>Option&lt;T&gt;.Some</c> and <c>Option&lt;T&gt;.None</c> are what a <c>switch</c> matches on.
/// </remarks>
/// <example>
/// <code>
/// var name = FindUser(42) switch
/// {
///     Option&lt;User&gt;.Some(var some) =&gt; some.Value.Name,
///     Option&lt;User&gt;.None =&gt; "unknown"
/// };
/// </code>
/// </example>
[UnionCaseName<Some<int>>("Some")]
public abstract partial record Option<T> : IUnion<Some<T>, None>;
```

> **On the `[UnionCaseName]` override — this needs a generator change first.**
>
> Naming rule 3 renders the closed generic `Some<T>` as `SomeOfT`, but the public API is
> `Option<T>.Some`. An attribute argument cannot mention `T` (`CS8968`), so the override names a
> closed stand-in, `Some<int>`.
>
> `ReadCaseNameOverrides` currently keys the dictionary on `attributeClass.TypeArguments[0]` with
> `SymbolEqualityComparer.Default`. `Some<int>` and `Some<T>` are different symbols under that
> comparer, so the lookup in `UnionCaseNaming.ResolveNames` would miss and the wrapper would still
> be called `SomeOfT`. **Verified by reading `UnionGenerator.cs:870-886`.**
>
> Fix both sides to compare original definitions. In `ReadCaseNameOverrides`:
>
> ```csharp
>             // Key on the original definition: the attribute must name a closed type
>             // (Some<int>), while the case type arrives constructed over the root's own
>             // parameter (Some<T>). Both share one original definition.
>             overrides[attributeClass.TypeArguments[0].OriginalDefinition] = name;
> ```
>
> and in `UnionCaseNaming.ResolveNames`, look up `caseTypes[i].OriginalDefinition` rather than
> `caseTypes[i]` — in both the initial name assignment and the `overrides.ContainsKey` guard in
> the collision loop.
>
> Add this test to `UnionGeneratorTests` before making the change, and watch it fail:
>
> ```csharp
>     [Fact]
>     public void CaseNameOverride_MatchesAcrossConstructedAndOpenGenerics()
>     {
>         var generated = Generate("""
>             using Corsinvest.Fx.Functional;
>
>             public record Some<T>(T Value);
>             public record None;
>
>             [UnionCaseName<Some<int>>("Some")]
>             public abstract partial record Maybe<T> : IUnion<Some<T>, None>;
>             """);
>
>         Assert.Contains("record Some(global::Some<T> Value)", generated);
>         Assert.DoesNotContain("SomeOfT", generated);
>     }
> ```

Update the factories:

```csharp
    public static Option<T> Some<T>(T value) => new Option<T>.Some(new Some<T>(value));

    public static Option<T> None<T>() => new Option<T>.None(new None());
```

- [ ] **Step 4: Build and fix the extensions**

```bash
dotnet build src/Functional/Corsinvest.Fx.Functional -v q --nologo -nodeReuse:false
```

`OptionExtensions.cs` reads `some.Value` in 13 places; `Some<T>.Value` keeps that name, so those
compile untouched. Fix only what the compiler flags.

- [ ] **Step 5: Run tests**

```bash
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/UnionCoreTypesTests/*"
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/OptionTests/*"
```

Expected: pass. Update any `OptionTests` assertion that reaches into the old wrapper shape.

- [ ] **Step 6: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional/ tests/Functional/
git commit -m "refactor(functional): express Option<T> through IUnion"
```

---

### Task 5: Migrate ResultOf<T,E>

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional/ResultOf.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional/ResultOfExtensions.cs`, `TryHelper.cs` as the compiler requires
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionCoreTypesTests.cs` (append)

- [ ] **Step 1: Write the failing test**

Append to `UnionCoreTypesTests.cs`:

```csharp
    [Fact]
    public void ResultOf_Ok_CarriesTheValue()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.True(result.IsOk);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Match(ok => ok.Value, fail => 0));
    }

    [Fact]
    public void ResultOf_Fail_CarriesTheError()
    {
        var result = ResultOf.Fail<int, string>("boom");

        Assert.True(result.IsFail);
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Match(ok => "", fail => fail.ErrorValue));
    }

    [Fact]
    public void ResultOf_SupportsNativeSwitch()
    {
        ResultOf<int, string> result = ResultOf.Ok<int, string>(7);

        var value = result switch
        {
            ResultOf<int, string>.Ok(var ok) => ok.Value,
            ResultOf<int, string>.Fail => -1
        };

        Assert.Equal(7, value);
    }

    [Fact]
    public void ResultOf_BindStillShortCircuits()
    {
        var result = ResultOf.Ok<int, string>(1)
                             .Bind(x => ResultOf.Fail<int, string>("stop"))
                             .Bind(x => ResultOf.Ok<int, string>(x + 1));

        Assert.Equal("stop", result.Match(ok => "", fail => fail.ErrorValue));
    }
```

- [ ] **Step 2: Run to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
```

- [ ] **Step 3: Rewrite the declaration**

In `src/Functional/Corsinvest.Fx.Functional/ResultOf.cs`, replace the `[Union]` block with:

```csharp
/// <summary>Represents a successful outcome.</summary>
/// <typeparam name="T">The type of the success value</typeparam>
/// <param name="Value">The success value</param>
public sealed record Ok<T>(T Value);

/// <summary>Represents a failed outcome.</summary>
/// <typeparam name="E">The type of the error value</typeparam>
/// <param name="ErrorValue">The error value</param>
public sealed record Fail<E>(E ErrorValue);

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
/// <typeparam name="E">The type of the error value</typeparam>
/// <remarks>
/// A discriminated union with two cases, <see cref="Ok{T}"/> and <see cref="Fail{E}"/>, declared
/// through <see cref="IUnion{T1,T2}"/>. Note that each case closes over a different type parameter
/// of the root - a shape no attribute form can express, because attribute arguments are metadata
/// and cannot reference the decorated type's own type parameters.
/// </remarks>
[UnionCaseName<Ok<int>>("Ok")]
[UnionCaseName<Fail<int>>("Fail")]
public abstract partial record ResultOf<T, E> : IUnion<Ok<T>, Fail<E>>
{
    /// <summary>Alias for <c>IsOk</c>, for FluentResults-style code.</summary>
    public bool IsSuccess => IsOk;

    /// <summary>Alias for <c>IsFail</c>, for FluentResults-style code.</summary>
    public bool IsFailure => IsFail;
}
```

> The aliases delegate to the generated `IsOk`/`IsFail` rather than writing `this is Ok`: inside
> the record body the bare name `Ok` is ambiguous between the nested wrapper and the external
> case type.

Update the factories:

```csharp
    public static ResultOf<T, E> Ok<T, E>(T value) => new ResultOf<T, E>.Ok(new Ok<T>(value));

    public static ResultOf<T, E> Fail<T, E>(E error) => new ResultOf<T, E>.Fail(new Fail<E>(error));

    public static ResultOf<T, string> Ok<T>(T value) => new ResultOf<T, string>.Ok(new Ok<T>(value));

    public static ResultOf<T, string> Fail<T>(string error)
        => new ResultOf<T, string>.Fail(new Fail<string>(error));
```

- [ ] **Step 4: Build and fix the fallout**

```bash
dotnet build src/Functional/Corsinvest.Fx.Functional -v q --nologo -nodeReuse:false
```

`ResultOfExtensions.cs` uses `ok.Value` / `error.ErrorValue` in 38 places and `TryHelper.cs` in 4;
both property names survive, so most compile untouched.

- [ ] **Step 5: Run the full suite**

```bash
dotnet build Corsinvest.Fx.sln -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe
```

- [ ] **Step 6: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional/ tests/Functional/
git commit -m "refactor(functional): express ResultOf<T,E> through IUnion"
```

---

### Task 6: Migrate examples and remaining tests

**Files:**
- Modify: `examples/04_UnionTypes.cs`, `examples/01_OptionBasics.cs`, `examples/02_ResultOfValidation.cs`
- Modify: `tests/Functional/**/UnionTests.cs`, `SimpleTest.cs`, `UnionExhaustivenessTests.cs`, `UnionCodeFixTests.cs`, `UnionGeneratorRobustnessTests.cs`

- [ ] **Step 1: Convert the examples**

In `examples/04_UnionTypes.cs` every union becomes the interface form:

```csharp
public record CreditCard(string Number, string ExpiryDate);
public record PayPal(string Email);
public record BankTransfer(string Iban, string Bic);

public abstract partial record PaymentMethod : IUnion<CreditCard, PayPal, BankTransfer>;

public record Loading;
public record Success(UserData User);
public record Failure(string Message);

public abstract partial record ApiResponse : IUnion<Loading, Success, Failure>;

public record Circle(double Radius);
public record Rectangle(double Width, double Height);
public record Triangle(double SideA, double SideB, double SideC);

public abstract partial record Shape : IUnion<Circle, Rectangle, Triangle>;
```

The existing `AppError` union (class + enum + struct + string) already uses external types — only
its declaration line changes. Update every `Match` and `switch`: handlers receive the case type,
so `Shape.Circle(var circle) => circle.Radius`.

- [ ] **Step 2: Run the examples**

```bash
dotnet build examples/Corsinvest.Fx.Examples.csproj -v q --nologo -nodeReuse:false
dotnet run --project examples/Corsinvest.Fx.Examples.csproj --no-build -nodeReuse:false
```

Expected: clean build, every section prints as before.

- [ ] **Step 3: Convert the test fixtures**

Rewrite the union declarations in each remaining test file to the interface form. The assertions
themselves mostly survive — only the declarations change.

- [ ] **Step 4: Full verification**

```bash
dotnet build Corsinvest.Fx.sln -v q --nologo -nodeReuse:false
for exe in $(find tests -name "*.Tests.exe" -path "*/bin/Debug/*" | grep -v "ref/"); do "./$exe"; done
```

Expected: `Avvisi: 0, Errori: 0` and zero test failures.

- [ ] **Step 5: Bump the major version**

Removing both attributes breaks every consumer. In `Directory.Build.props`:

```xml
    <Version>2.0.0</Version>
    <AssemblyVersion>2.0.0.0</AssemblyVersion>
    <FileVersion>2.0.0.0</FileVersion>
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor!: move examples and tests onto the IUnion marker interface"
```

---

### Task 7: Documentation

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional/docs/Union.md`, `Option.md`, `ResultOf.md`
- Modify: `src/Functional/Corsinvest.Fx.Functional/README.md`, `README.md`

- [ ] **Step 1: Rewrite the declaration sections**

Replace every `[Union]` example with the interface form. Keep the existing "Switch Expressions",
"Comparison with C# 15 union types" and "Diagnostics" sections, with these changes:

- In the C# 15 comparison, flip "Same type in several unions" from ❌ to ✅ and rewrite the
  surrounding paragraph: the model now composes external types the way C# 15 does, while keeping
  the closed hierarchy, no boxing, and no invalid `default` state. Add a row noting that a case
  may close over the root's own type parameter — which the `union` keyword cannot express either,
  since its cases are independent types.
- Add a "Why an interface and not an attribute" section: attribute arguments are metadata,
  resolved before the decorated type is bound, so they cannot mention the root's `T`
  (`CS8968`/`CS0416`); an interface is part of the declaration, so Roslyn hands the generator
  `Some<T>` already bound.
- Document the naming rules and `[UnionCaseName<T>]`, including that its type argument must be
  closed and is matched by original definition.

- [ ] **Step 2: Verify every documented snippet compiles**

Create a scratch project referencing the built library, paste each snippet, build. A snippet that
does not compile is a documentation bug.

- [ ] **Step 3: Check local links**

```bash
cd K:/source/repos/OpenSource/CSharp/Corsinvest.Fx
for f in README.md src/Functional/Corsinvest.Fx.Functional/README.md src/Functional/Corsinvest.Fx.Functional/docs/*.md; do
  d=$(dirname "$f")
  grep -oE '\]\([^)#]+\.(md|cs)[^)]*\)' "$f" | sed 's/](//; s/)$//; s/#.*//' | while read -r l; do
    case "$l" in http*) continue;; esac
    [ -e "$d/$l" ] || [ -e "$l" ] || echo "BROKEN: $f -> $l"
  done
done
```

Expected: no output.

- [ ] **Step 4: Commit**

```bash
git add README.md src/Functional/Corsinvest.Fx.Functional/
git commit -m "docs(union): document the IUnion marker interface"
```
