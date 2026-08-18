# Generic Union Attribute Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the nested-record `[Union]` attribute with `[Union<T1..T8>]`, which composes external, independently declared types as union cases.

**Architecture:** The union root stays an `abstract partial record` with a private constructor, and the generator emits one `sealed partial record` wrapper per type argument. Each wrapper *contains* its case type in a `Value` property rather than being it, which removes the single-base-class limit while keeping the hierarchy closed. Because the wrappers are still sealed nested subtypes of the root, the existing analyzer, suppressor and code fix keep working unchanged.

**Tech Stack:** C# 12 / netstandard2.0 source generator, Roslyn 4.7.0, xunit.v3 4.0.0, .NET 8/9 targets.

**Spec:** This document (spec and plan combined — the design was validated experimentally across the preceding session; findings are recorded in "Validated Design Facts" below).

## Global Constraints

- Generator project targets `netstandard2.0`; **collection expressions (`[...]`) do not work with its `ImmutableArray<T>`** — use `ImmutableArray.Create(...)` / `.ToImmutableArray()`.
- Library targets `net8.0`; generic attributes require C# 11, already available via `<LangVersion>latest</LangVersion>`.
- `TreatWarningsAsErrors=true` in `Directory.Build.props:18` — any new warning fails the build.
- `EnforceExtendedAnalyzerRules=true` — every new `DiagnosticDescriptor` **must** be registered in `AnalyzerReleases.Unshipped.md` or the build fails.
- Analyzer diagnostics keep the `UNION###` prefix. Currently allocated: UNION001–UNION007.
- Tests run via the built executable (`bin/Debug/net8.0/*.Tests.exe`), not `dotnet test` — the VSTest path is broken under the .NET 10 SDK.
- Build with `-nodeReuse:false`; a lingering MSBuild node locks the generator DLL.
- Roslyn loads analyzers at solution open: after changing generator code, a running Visual Studio keeps the old DLL.

## Validated Design Facts

Each of these was confirmed by compiling a probe project during design. Do not re-litigate them:

1. A nested wrapper **may carry the same name as the type it wraps** (`Pet.Cat` wrapping `global::Cat`); the generated code qualifies the wrapped type.
2. Any case type works — `class`, `sealed class`, `record`, `struct`, `record struct`, `enum`, `interface`, `string`, `int`, arrays, closed generics, tuples, delegates. `sealed` is not a limit because the wrapper contains rather than inherits.
3. Value-type cases are stored in a **typed field, so nothing is boxed**.
4. The private constructor on the root keeps the hierarchy closed: an external `record Rogue : Pet` fails with `CS0122`.
5. Two case types that collapse to the same CLR type (e.g. `(int X, int Y)` and `(int Row, int Col)`, both `ValueTuple<int,int>`) make duplicate implicit conversions illegal — `CS0557`.
6. `Option<T>` and `ResultOf<T,E>` are expressible in this model, implicit conversions carry the type parameters correctly.
7. Switch, `Match`, void `Match`, `MatchAsync`, `TryGet*` and `Is*` all work against the wrappers.

## Known Risks

**1. `ForAttributeWithMetadataName` needs one registration per arity.** The API takes a
compile-time constant, so Task 3 writes eight registrations rather than a loop. If that proves
unwieldy, the fallback is `CreateSyntaxProvider` with a predicate that matches any attribute whose
name starts with `Union` and a semantic check in the transform — slower, but arity-agnostic.

**2. Generic case types need explicit names.** `Some<T>` would otherwise become `SomeOfT`. Tasks 6
and 7 pin the names with `[UnionCaseName<T>]`. Any other generic case type in user code hits the
same rule, which is why rule 7 exists.

**3. `Option<T>.Match` handler types change.** Today the `Some` handler receives
`Option<T>.Some` (which exposes `Value`); afterwards it receives `Some<T>` (which also exposes
`Value`). Call sites reading `some.Value` are unaffected — that is the reason for keeping the
property name — but anything that names the handler's *type* explicitly must be updated.

**4. This is a breaking change to a published package.** `[Union]` disappears in Task 9. The
version in `Directory.Build.props:28` should move to 2.0.0 as part of that commit.

## File Structure

**New files:**
- `src/Functional/Corsinvest.Fx.Functional/UnionAttributeGeneric.cs` — `UnionAttribute<T1>` … `UnionAttribute<T1..T8>`, plus `UnionCaseNameAttribute` for name overrides.
- `src/Functional/Corsinvest.Fx.Functional.Generators/GenericUnionInfo.cs` — the model the generator builds from an attribute's type arguments (root type, case list, wrapper names).
- `src/Functional/Corsinvest.Fx.Functional.Generators/UnionCaseNaming.cs` — the naming rules and collision handling, isolated so it can be unit-tested without running a generator.
- `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionCaseNamingTests.cs`
- `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs`
- `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionCoreTypesTests.cs`

**Modified files:**
- `src/Functional/Corsinvest.Fx.Functional.Generators/UnionGenerator.cs` — add the generic-attribute path alongside the existing one, then remove the old one in Task 9.
- `src/Functional/Corsinvest.Fx.Functional.Generators/UnionSymbolHelper.cs` — recognise the generic attribute in `IsUnionRoot`.
- `src/Functional/Corsinvest.Fx.Functional/Option.cs`, `ResultOf.cs` — migrate to the new model.
- `src/Functional/Corsinvest.Fx.Functional/OptionExtensions.cs`, `ResultOfExtensions.cs`, `TryHelper.cs` — follow the `Match` signature change.
- `examples/04_UnionTypes.cs`, `examples/01_OptionBasics.cs`, `examples/02_ResultOfValidation.cs`
- All `tests/Functional/**/Union*.cs`
- `src/Functional/Corsinvest.Fx.Functional/docs/Union.md`, `Option.md`, `ResultOf.md`
- `src/Functional/Corsinvest.Fx.Functional.Generators/AnalyzerReleases.Unshipped.md`

**Deleted in Task 9:**
- `src/Functional/Corsinvest.Fx.Functional/UnionAttribute.cs`

## Naming Rules (normative)

The generator derives a wrapper name from each case type:

1. **Simple named type** → the type's own short name. `Cat` → `Pet.Cat`.
2. **Name collision between two case types** (e.g. `Farm.Cat` and `Wild.Cat`) → prefix each with its immediate containing namespace segment: `FarmCat`, `WildCat`.
3. **Closed generic** → concatenate: `List<string>` → `ListOfString`; `Dictionary<string, int>` → `DictionaryOfStringInt`.
4. **Array** → element name + `Array`: `int[]` → `Int32Array`.
5. **Tuple** → `TupleOf` + element type names: `(int X, int Y)` → `TupleOfInt32Int32`.
6. **Primitive/BCL keyword types** → the CLR name, not the keyword: `int` → `Int32`, `string` → `String`.
7. **Explicit override always wins:** `[UnionCaseName<Farm.Cat>("Domestic")]` on the root.
8. If rule 2 still leaves a collision, report **UNION008** and skip generation for that root.

Implicit conversion operators are emitted **only** when every case type maps to a distinct CLR type. If two cases share one CLR type, conversions are omitted for the whole union and **UNION009** is reported as a warning (the union still works through its factory methods).

---

### Task 1: Generic union attributes

**Files:**
- Create: `src/Functional/Corsinvest.Fx.Functional/UnionAttributeGeneric.cs`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionAttributeTests.cs` (modify — file exists)

**Interfaces:**
- Consumes: nothing.
- Produces: `Corsinvest.Fx.Functional.UnionAttribute<T1>` … `UnionAttribute<T1,T2,T3,T4,T5,T6,T7,T8>`, all `sealed`, `AttributeTargets.Class`, `AllowMultiple = false`. Also `UnionCaseNameAttribute<T>` with a `string Name` constructor parameter and `AllowMultiple = true`.

- [ ] **Step 1: Write the failing test**

Append to `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionAttributeTests.cs`:

```csharp
[Fact]
public void GenericUnionAttribute_ExistsForArities_One_To_Eight()
{
    var assembly = typeof(UnionAttribute<>).Assembly;

    for (var arity = 1; arity <= 8; arity++)
    {
        var name = $"Corsinvest.Fx.Functional.UnionAttribute`{arity}";
        Assert.NotNull(assembly.GetType(name));
    }
}

[Fact]
public void GenericUnionAttribute_TargetsClassesOnly_AndIsNotMultiple()
{
    var usage = typeof(UnionAttribute<,>).GetCustomAttribute<AttributeUsageAttribute>();

    Assert.NotNull(usage);
    Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
    Assert.False(usage.AllowMultiple);
}

[Fact]
public void UnionCaseNameAttribute_CarriesTheOverrideName()
{
    var attribute = new UnionCaseNameAttribute<string>("Text");

    Assert.Equal("Text", attribute.Name);
}
```

Add `using System.Reflection;` at the top of the file if not present.

- [ ] **Step 2: Run test to verify it fails**

```bash
cd K:/source/repos/OpenSource/CSharp/Corsinvest.Fx
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
```

Expected: FAIL — `error CS0246: The type or namespace name 'UnionAttribute<>' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `src/Functional/Corsinvest.Fx.Functional/UnionAttributeGeneric.cs`:

```csharp
namespace Corsinvest.Fx.Functional;

/// <summary>
/// Marks a partial record as a discriminated union whose cases are the supplied type arguments.
/// </summary>
/// <remarks>
/// <para>
/// Unlike a nested-case declaration, the case types are ordinary standalone types, so the same
/// type can take part in several unions. The generator emits one sealed nested wrapper per case
/// that derives from the union root, which is what keeps the hierarchy closed and lets
/// <c>switch</c> work directly on the union.
/// </para>
/// <para>
/// Any type can be a case: classes, sealed classes, records, structs, enums, interfaces,
/// primitives, arrays, closed generics and tuples. Value-type cases are stored in typed fields,
/// so nothing is boxed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public record Cat(string Name);
/// public record Dog(string Name);
///
/// [Union&lt;Cat, Dog&gt;]
/// public abstract partial record Pet;
///
/// Pet pet = new Cat("Whiskers");
/// var name = pet switch
/// {
///     Pet.Cat(var cat) =&gt; cat.Name,
///     Pet.Dog(var dog) =&gt; dog.Name
/// };
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4, T5> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4, T5, T6> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4, T5, T6, T7> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4, T5, T6, T7, T8> : Attribute;

/// <summary>
/// Overrides the generated wrapper name for one case type of a union.
/// </summary>
/// <typeparam name="T">The case type whose wrapper is being renamed.</typeparam>
/// <example>
/// <code>
/// [Union&lt;Farm.Cat, Wild.Cat&gt;]
/// [UnionCaseName&lt;Farm.Cat&gt;("Domestic")]
/// [UnionCaseName&lt;Wild.Cat&gt;("Feral")]
/// public abstract partial record Feline;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class UnionCaseNameAttribute<T>(string name) : Attribute
{
    /// <summary>The wrapper name to use for <typeparamref name="T"/>.</summary>
    public string Name { get; } = name;
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/UnionAttributeTests/*"
```

Expected: PASS, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional/UnionAttributeGeneric.cs tests/Functional/Corsinvest.Fx.Functional.Tests/UnionAttributeTests.cs
git commit -m "feat(functional): add generic Union attributes for external case types"
```

---

### Task 2: Wrapper naming rules

**Files:**
- Create: `src/Functional/Corsinvest.Fx.Functional.Generators/UnionCaseNaming.cs`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionCaseNamingTests.cs`

**Interfaces:**
- Consumes: nothing (pure symbol-to-string logic).
- Produces: `public static class UnionCaseNaming` with
  `public static string GetSimpleName(ITypeSymbol type)` and
  `public static ImmutableArray<string> ResolveNames(ImmutableArray<ITypeSymbol> caseTypes, IReadOnlyDictionary<ITypeSymbol, string> overrides, out bool hasUnresolvedCollision)`.
  Task 3 calls `ResolveNames`.

- [ ] **Step 1: Write the failing test**

Create `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionCaseNamingTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
```

Expected: FAIL — `error CS0103: The name 'UnionCaseNaming' does not exist`.

- [ ] **Step 3: Write minimal implementation**

Create `src/Functional/Corsinvest.Fx.Functional.Generators/UnionCaseNaming.cs`:

```csharp
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
            names[i] = overrides.TryGetValue(caseTypes[i], out var custom)
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
            if (clashing.Contains(names[i]) && !overrides.ContainsKey(caseTypes[i]))
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
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/UnionCaseNamingTests/*"
```

Expected: PASS, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional.Generators/UnionCaseNaming.cs tests/Functional/Corsinvest.Fx.Functional.Tests/UnionCaseNamingTests.cs
git commit -m "feat(generators): derive union wrapper names from case types"
```

---

### Task 3: Generator reads the generic attribute

**Files:**
- Create: `src/Functional/Corsinvest.Fx.Functional.Generators/GenericUnionInfo.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/UnionGenerator.cs`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs`

**Interfaces:**
- Consumes: `UnionCaseNaming.ResolveNames` (Task 2); `UnionAttribute<...>` and `UnionCaseNameAttribute<T>` (Task 1).
- Produces: for a root `R` with cases `C1..Cn` named `N1..Nn`, generated members
  `public sealed partial record Ni(Ci Value) : R;`,
  `public bool IsNi { get; }`,
  `public bool TryGetNi(out Ci value)`,
  `public static implicit operator R(Ci value)` (only when all CLR types are distinct),
  `public TResult Match<TResult>(Func<C1,TResult> onN1, …)`,
  `public void Match(Action<C1> onN1, …)`,
  `public async Task<TResult> MatchAsync<TResult>(Func<C1,Task<TResult>> onN1, …)`.
  Note the `Match` handlers receive the **case type**, not the wrapper.

- [ ] **Step 1: Write the failing test**

Create `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    // ---- infrastructure ----------------------------------------------------

    private static string Generate(string source)
    {
        var trees = RunGenerator(source);

        return string.Join("\n", trees.Select(t => t.ToString()));
    }

    private static ImmutableArray<Diagnostic> CompileWithGenerator(string source)
    {
        var compilation = CreateCompilation(source);

        CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

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
            "GenericUnionTest",
            [CSharpSyntaxTree.ParseText(source)],
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .Append(MetadataReference.CreateFromFile(typeof(UnionCaseNaming).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/GenericUnionGeneratorTests/*"
```

Expected: FAIL — the generator ignores the generic attribute, so nothing is generated.

- [ ] **Step 3: Write the model type**

Create `src/Functional/Corsinvest.Fx.Functional.Generators/GenericUnionInfo.cs`:

```csharp
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

    public Location? Location { get; } = location;
}
```

- [ ] **Step 4: Add the generic path to the generator**

In `src/Functional/Corsinvest.Fx.Functional.Generators/UnionGenerator.cs`, add these members to the `UnionGenerator` class and register the new provider inside `Initialize`.

Add to `Initialize`, right after the existing `context.RegisterSourceOutput(unionDeclarations, ...)` call:

```csharp
        // Generic attribute path: [Union<T1, T2>] on a root with no nested cases.
        var genericUnions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Corsinvest.Fx.Functional.UnionAttribute`2",
                predicate: static (node, _) => node is RecordDeclarationSyntax,
                transform: static (ctx, _) => BuildGenericUnionInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(
            genericUnions,
            static (spc, info) => GenerateGenericUnion(spc, info!));
```

> Note: `ForAttributeWithMetadataName` matches one arity at a time. Repeat the block for
> `` `1 `` through `` `8 ``, or collect the eight providers and combine them; the straightforward
> approach is a loop is not possible here because the API needs a compile-time constant, so write
> eight registrations that all point at the same `BuildGenericUnionInfo` / `GenerateGenericUnion`.

Add the builder:

```csharp
    private static GenericUnionInfo? BuildGenericUnionInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol root) { return null; }

        var attribute = context.Attributes[0];
        if (attribute.AttributeClass is not { } attributeClass) { return null; }

        var caseTypes = attributeClass.TypeArguments;
        if (caseTypes.Length == 0) { return null; }

        var overrides = ReadCaseNameOverrides(root);
        var names = UnionCaseNaming.ResolveNames(caseTypes, overrides, out var unresolvedCollision);
        if (unresolvedCollision) { return null; }   // reported as UNION008 in GenerateGenericUnion

        // Duplicate CLR types make duplicate conversion operators illegal (CS0557).
        var distinctClrTypes = caseTypes.Distinct(SymbolEqualityComparer.Default).Count();

        return new GenericUnionInfo(
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
            location: root.Locations.FirstOrDefault());
    }

    private static IReadOnlyDictionary<ITypeSymbol, string> ReadCaseNameOverrides(INamedTypeSymbol root)
    {
        var overrides = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);

        foreach (var attribute in root.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "UnionCaseNameAttribute" } attributeClass) { continue; }
            if (attributeClass.TypeArguments.Length != 1) { continue; }
            if (attribute.ConstructorArguments.Length != 1) { continue; }
            if (attribute.ConstructorArguments[0].Value is not string name) { continue; }

            overrides[attributeClass.TypeArguments[0]] = name;
        }

        return overrides;
    }
```

Add the emitter:

```csharp
    private static void GenerateGenericUnion(SourceProductionContext context, GenericUnionInfo info)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine($"namespace {info.Namespace};");
            sb.AppendLine();
        }

        var root = info.TypeName + info.TypeParameters;

        sb.AppendLine($"public abstract partial record {root}");
        sb.AppendLine("{");
        sb.AppendLine($"    private {info.TypeName}() {{ }}");
        sb.AppendLine();

        var qualified = info.CaseTypes
            .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToImmutableArray();

        // Wrappers
        for (var i = 0; i < info.CaseNames.Length; i++)
        {
            sb.AppendLine($"    public sealed partial record {info.CaseNames[i]}({qualified[i]} Value) : {root};");
        }
        sb.AppendLine();

        // Implicit conversions
        if (info.EmitImplicitConversions)
        {
            for (var i = 0; i < info.CaseNames.Length; i++)
            {
                sb.AppendLine($"    public static implicit operator {root}({qualified[i]} value) => new {info.CaseNames[i]}(value);");
            }
            sb.AppendLine();
        }

        // Is* properties
        foreach (var name in info.CaseNames)
        {
            sb.AppendLine($"    public bool Is{name} => this is {name};");
        }
        sb.AppendLine();

        GenerateGenericMatch(sb, info, qualified, root);
        GenerateGenericTryGet(sb, info, qualified);

        sb.AppendLine("}");

        context.AddSource($"{info.TypeName}.Union.g.cs", sb.ToString());
    }

    private static void GenerateGenericMatch(StringBuilder sb,
                                             GenericUnionInfo info,
                                             ImmutableArray<string> qualified,
                                             string root)
    {
        // Match with a result
        sb.AppendLine("    public TResult Match<TResult>(");
        for (var i = 0; i < info.CaseNames.Length; i++)
        {
            var comma = i < info.CaseNames.Length - 1 ? "," : string.Empty;
            sb.AppendLine($"        Func<{qualified[i]}, TResult> on{info.CaseNames[i]}{comma}");
        }
        sb.AppendLine("    )");
        sb.AppendLine("        => this switch");
        sb.AppendLine("        {");
        foreach (var name in info.CaseNames)
        {
            sb.AppendLine($"            {name} wrapped => on{name}(wrapped.Value),");
        }
        sb.AppendLine("            _ => throw new InvalidOperationException(\"Invalid union state\")");
        sb.AppendLine("        };");
        sb.AppendLine();

        // Match without a result
        sb.AppendLine("    public void Match(");
        for (var i = 0; i < info.CaseNames.Length; i++)
        {
            var comma = i < info.CaseNames.Length - 1 ? "," : string.Empty;
            sb.AppendLine($"        Action<{qualified[i]}> on{info.CaseNames[i]}{comma}");
        }
        sb.AppendLine("    )");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (this)");
        sb.AppendLine("        {");
        foreach (var name in info.CaseNames)
        {
            sb.AppendLine($"            case {name} wrapped: on{name}(wrapped.Value); break;");
        }
        sb.AppendLine("            default: throw new InvalidOperationException(\"Invalid union state\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Async match
        sb.AppendLine("    public async Task<TResult> MatchAsync<TResult>(");
        for (var i = 0; i < info.CaseNames.Length; i++)
        {
            var comma = i < info.CaseNames.Length - 1 ? "," : string.Empty;
            sb.AppendLine($"        Func<{qualified[i]}, Task<TResult>> on{info.CaseNames[i]}{comma}");
        }
        sb.AppendLine("    )");
        sb.AppendLine("        => this switch");
        sb.AppendLine("        {");
        foreach (var name in info.CaseNames)
        {
            sb.AppendLine($"            {name} wrapped => await on{name}(wrapped.Value),");
        }
        sb.AppendLine("            _ => throw new InvalidOperationException(\"Invalid union state\")");
        sb.AppendLine("        };");
        sb.AppendLine();
    }

    private static void GenerateGenericTryGet(StringBuilder sb,
                                              GenericUnionInfo info,
                                              ImmutableArray<string> qualified)
    {
        for (var i = 0; i < info.CaseNames.Length; i++)
        {
            var name = info.CaseNames[i];
            sb.AppendLine($"    public bool TryGet{name}(out {qualified[i]} value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (this is {name} wrapped) {{ value = wrapped.Value; return true; }}");
            sb.AppendLine("        value = default!;");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }
```

Add `using System.Collections.Immutable;` and `using System.Collections.Generic;` to the file's usings if missing.

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet build src/Functional/Corsinvest.Fx.Functional.Generators -v q --nologo -nodeReuse:false
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/GenericUnionGeneratorTests/*"
```

Expected: PASS, 0 failed. Old tests must still pass — run the whole file set:

```bash
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe
```

- [ ] **Step 6: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional.Generators/ tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs
git commit -m "feat(generators): generate unions from generic Union attribute"
```

---

### Task 4: Diagnostics for naming collisions and duplicate CLR types

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/UnionGenerator.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/AnalyzerReleases.Unshipped.md`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs` (append)

**Interfaces:**
- Consumes: `GenericUnionInfo` (Task 3).
- Produces: diagnostics `UNION008` (error, unresolved case-name collision) and `UNION009` (warning, implicit conversions omitted).

- [ ] **Step 1: Write the failing test**

Append to `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs`:

```csharp
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
        Assert.Single(diagnostics.Where(d => d.Id == "UNION009"));

        // Without conversions the generated code still compiles.
        var generated = Generate(source);
        Assert.DoesNotContain("implicit operator", generated);
    }

    private static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(string source)
        => CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGenerators(CreateCompilation(source))
            .GetRunResult()
            .Results
            .SelectMany(r => r.Diagnostics)
            .ToImmutableArray();
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/GenericUnionGeneratorTests/*"
```

Expected: FAIL — no UNION008/UNION009 reported.

- [ ] **Step 3: Add the descriptors and report them**

In `UnionGenerator.cs`, add next to the existing descriptors:

```csharp
    private static readonly DiagnosticDescriptor CaseNameCollisionDescriptor = new(
        id: "UNION008",
        title: "Union case names collide",
        messageFormat: "Union '{0}' has case types that resolve to the same wrapper name; "
                     + "use [UnionCaseName<T>(\"...\")] to disambiguate",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateCaseTypeDescriptor = new(
        id: "UNION009",
        title: "Implicit conversions omitted",
        messageFormat: "Union '{0}' has case types that share one CLR type, so implicit "
                     + "conversions were not generated; construct the case wrappers directly",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
```

Change `BuildGenericUnionInfo` to carry the collision flag instead of returning null, by replacing:

```csharp
        var names = UnionCaseNaming.ResolveNames(caseTypes, overrides, out var unresolvedCollision);
        if (unresolvedCollision) { return null; }   // reported as UNION008 in GenerateGenericUnion
```

with:

```csharp
        var names = UnionCaseNaming.ResolveNames(caseTypes, overrides, out var unresolvedCollision);
```

and add `unresolvedCollision` as a field on `GenericUnionInfo`. In `GenericUnionInfo.cs` add a
constructor parameter `bool hasNameCollision` and a matching
`public bool HasNameCollision { get; }` property; pass `unresolvedCollision` from the builder.

At the top of `GenerateGenericUnion`, before emitting anything:

```csharp
        if (info.HasNameCollision)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CaseNameCollisionDescriptor, info.Location ?? Location.None, info.TypeName));
            return;
        }

        if (!info.EmitImplicitConversions)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DuplicateCaseTypeDescriptor, info.Location ?? Location.None, info.TypeName));
        }
```

- [ ] **Step 4: Register the rules**

In `src/Functional/Corsinvest.Fx.Functional.Generators/AnalyzerReleases.Unshipped.md`, add under `### New Rules`:

```
UNION008 | Design | Error | Union case names collide
UNION009 | Design | Warning | Implicit conversions omitted for duplicate CLR case types
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet build src/Functional/Corsinvest.Fx.Functional.Generators -v q --nologo -nodeReuse:false
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/GenericUnionGeneratorTests/*"
```

Expected: PASS, 0 failed.

- [ ] **Step 6: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional.Generators/ tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs
git commit -m "feat(generators): diagnose union case name collisions and duplicate CLR types"
```

---

### Task 5: Analyzer, suppressor and code fix recognise generic unions

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/UnionSymbolHelper.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional.CodeFixes/UnionExhaustivenessCodeFixProvider.cs`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionGeneratorTests.cs` (append)

**Interfaces:**
- Consumes: generated wrappers from Task 3.
- Produces: `UnionSymbolHelper.IsUnionRoot` returns true for a type carrying any `UnionAttribute<...>`; `GetVariants` is unchanged because the wrappers are already sealed nested subtypes.

**Why the code fix needs changing:** it currently builds `Variant(var a, var b)` from a record's positional members. A generic-union wrapper has exactly one member (`Value`), so the fix must emit `Pet.Cat(var cat)` with a name derived from the case type.

- [ ] **Step 1: Write the failing test**

Append to `GenericUnionGeneratorTests.cs`:

```csharp
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
```

Extend the `CompileWithGenerator` helper with the analyzer path:

```csharp
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
```

Add `using Microsoft.CodeAnalysis.Diagnostics;` to the file.

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/GenericUnionGeneratorTests/*"
```

Expected: FAIL — `IsUnionRoot` only matches the non-generic attribute.

- [ ] **Step 3: Recognise the generic attribute**

In `UnionSymbolHelper.cs`, replace the body of `IsUnionRoot`:

```csharp
    public static bool IsUnionRoot(ITypeSymbol? type)
        => type is not null
           && type.GetAttributes().Any(a => IsUnionAttribute(a.AttributeClass));

    /// <summary>
    /// True for the non-generic <c>[Union]</c> and for any arity of <c>[Union&lt;...&gt;]</c>.
    /// </summary>
    private static bool IsUnionAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null) { return false; }

        if (attributeClass.ToDisplayString() == UnionAttributeMetadataName) { return true; }

        return attributeClass is { Name: "UnionAttribute", IsGenericType: true }
               && attributeClass.ContainingNamespace?.ToDisplayString() == "Corsinvest.Fx.Functional";
    }
```

- [ ] **Step 4: Teach the code fix about single-member wrappers**

In `UnionExhaustivenessCodeFixProvider.cs`, replace `SafeIdentifier` usage inside `CreatePattern` so
a one-member wrapper names its variable after the wrapped type rather than after `Value`:

```csharp
    private static PatternSyntax CreatePattern(INamedTypeSymbol variant, SemanticModel semanticModel, int position)
    {
        var typeSyntax = ParseTypeName(variant.ToMinimalDisplayString(semanticModel, position));
        var parameters = GetPrimaryConstructorParameters(variant);

        if (parameters.Count == 0) { return TypePattern(typeSyntax); }

        // A generic-union wrapper has a single member called Value; naming the variable after the
        // wrapped type reads better than `var value`.
        var subpatterns = parameters.Count == 1 && parameters[0].Name == "Value"
            ? [Subpattern(VarPattern(SingleVariableDesignation(
                  Identifier(SafeIdentifier(parameters[0].Type.Name)))))]
            : parameters.Select(p =>
                  Subpattern(VarPattern(SingleVariableDesignation(Identifier(SafeIdentifier(p.Name))))));

        return RecursivePattern(
            typeSyntax,
            PositionalPatternClause(SeparatedList(subpatterns)),
            propertyPatternClause: null,
            designation: null);
    }
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet build Corsinvest.Fx.sln -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe
```

Expected: all tests pass, including the pre-existing UNION004/005 and code fix suites.

- [ ] **Step 6: Commit**

```bash
git add src/Functional/
git commit -m "feat(analyzers): support generic unions in analyzer, suppressor and code fix"
```

---

### Task 6: Migrate Option<T>

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional/Option.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional/OptionExtensions.cs`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionCoreTypesTests.cs` (create)

**Interfaces:**
- Consumes: `UnionAttribute<T1,T2>` (Task 1), generator (Task 3).
- Produces: `Corsinvest.Fx.Functional.None` (parameterless record), `Corsinvest.Fx.Functional.Some<T>(T Value)`, and `Option<T>` with `Option<T>.Some` / `Option<T>.None` wrappers. `Option<T>.Match` now hands `Some<T>` and `None` to its handlers, so `some.Value` keeps working unchanged.

- [ ] **Step 1: Write the failing test**

Create `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionCoreTypesTests.cs`:

```csharp
namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Locks the public shape of Option and ResultOf after the move to generic unions.
/// </summary>
public class GenericUnionCoreTypesTests
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
        var result = Option.Some(5).Map(x => x * 2).GetValueOr(0);

        Assert.Equal(10, result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
```

Expected: FAIL — `Option<int>.Some` deconstructs to the raw value today, not to a `Some<T>`.

- [ ] **Step 3: Rewrite Option.cs**

Replace the `[Union]` declaration in `src/Functional/Corsinvest.Fx.Functional/Option.cs` (lines 32–45) with:

```csharp
/// <summary>Represents the absence of a value.</summary>
public sealed record None;

/// <summary>Represents a present value.</summary>
/// <typeparam name="T">The type of the value</typeparam>
/// <param name="Value">The contained value</param>
public sealed record Some<T>(T Value);

/// <summary>
/// Represents an optional value that may or may not be present.
/// Use this type to make null handling explicit and type-safe.
/// </summary>
/// <typeparam name="T">The type of the optional value</typeparam>
/// <remarks>
/// A discriminated union with two cases: <see cref="Some{T}"/> and <see cref="None"/>.
/// The cases are standalone types, so they can be reused; the generated wrappers
/// <c>Option&lt;T&gt;.Some</c> and <c>Option&lt;T&gt;.None</c> are what a <c>switch</c> matches on.
/// </remarks>
/// <example>
/// <code>
/// Option&lt;User&gt; FindUser(int id)
///     =&gt; _db.Find(id) is { } user ? Option.Some(user) : Option.None&lt;User&gt;();
///
/// var name = FindUser(42) switch
/// {
///     Option&lt;User&gt;.Some(var some) =&gt; some.Value.Name,
///     Option&lt;User&gt;.None =&gt; "unknown"
/// };
/// </code>
/// </example>
[Union<Some<T>, None>]
[UnionCaseName<Some<T>>("Some")]
public abstract partial record Option<T>;
```

> **Why the override:** naming rule 3 turns the closed generic `Some<T>` into `SomeOfT`.
> The public API has always exposed `Option<T>.Some`, so the name is pinned explicitly.
> `None` is not generic and needs no override.

Update the factory methods further down the same file:

```csharp
    public static Option<T> Some<T>(T value) => new Option<T>.Some(new Some<T>(value));

    public static Option<T> None<T>() => new Option<T>.None(new None());
```

- [ ] **Step 4: Follow the Match signature in OptionExtensions.cs**

The handlers already receive something with a `.Value` property, so the existing bodies
(`some => some.Value`) compile unchanged. The `None` handler previously took `Option<T>.None`
and now takes `None`; both are only used as a discard, so no body changes are required.

Verify by building — do not pre-emptively edit.

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet build Corsinvest.Fx.sln -v q --nologo -nodeReuse:false
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/GenericUnionCoreTypesTests/*"
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe -filter "/*/*/OptionTests/*"
```

Expected: PASS. If `OptionTests` fails on `Option<T>.Some` deconstruction, update those assertions
to the new shape — the wrapper now yields a `Some<T>`.

- [ ] **Step 6: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional/Option.cs src/Functional/Corsinvest.Fx.Functional/OptionExtensions.cs tests/Functional/Corsinvest.Fx.Functional.Tests/
git commit -m "refactor(functional): express Option<T> with the generic union attribute"
```

---

### Task 7: Migrate ResultOf<T,E>

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional/ResultOf.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional/ResultOfExtensions.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional/TryHelper.cs`
- Test: `tests/Functional/Corsinvest.Fx.Functional.Tests/GenericUnionCoreTypesTests.cs` (append)

**Interfaces:**
- Consumes: Task 1, Task 3.
- Produces: `Corsinvest.Fx.Functional.Ok<T>(T Value)`, `Corsinvest.Fx.Functional.Fail<E>(E ErrorValue)`, and `ResultOf<T,E>` with `Ok` / `Fail` wrappers. `IsSuccess` / `IsFailure` aliases are preserved.

- [ ] **Step 1: Write the failing test**

Append to `GenericUnionCoreTypesTests.cs`:

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

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet build tests/Functional/Corsinvest.Fx.Functional.Tests -v q --nologo -nodeReuse:false
```

Expected: FAIL — the wrapper shape does not match yet.

- [ ] **Step 3: Rewrite ResultOf.cs**

Replace the `[Union]` declaration in `src/Functional/Corsinvest.Fx.Functional/ResultOf.cs`
(lines 54–82) with:

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
/// <para>
/// A discriminated union with two cases: <see cref="Ok{T}"/> and <see cref="Fail{E}"/>.
/// The generated wrappers <c>ResultOf&lt;T,E&gt;.Ok</c> and <c>ResultOf&lt;T,E&gt;.Fail</c> are what
/// a <c>switch</c> matches on.
/// </para>
/// <para>
/// <strong>Dual Naming:</strong> both concise names (<c>IsOk</c>) and explicit ones
/// (<c>IsSuccess</c>) are available; the aliases compile to identical IL.
/// </para>
/// </remarks>
[Union<Ok<T>, Fail<E>>]
[UnionCaseName<Ok<T>>("Ok")]
[UnionCaseName<Fail<E>>("Fail")]
public abstract partial record ResultOf<T, E>
{
    /// <summary>Alias for <c>IsOk</c>, for FluentResults-style code.</summary>
    public bool IsSuccess => IsOk;

    /// <summary>Alias for <c>IsFail</c>, for FluentResults-style code.</summary>
    public bool IsFailure => IsFail;
}
```

> **Why the overrides:** `Ok<T>` and `Fail<E>` are closed generics, which naming rule 3 would
> render as `OkOfT` and `FailOfE`. The public API is `ResultOf<T,E>.Ok` / `.Fail`, so both names
> are pinned. This also keeps `IsOk` / `IsFail` — and therefore the `IsSuccess` / `IsFailure`
> aliases above — spelled as they are today.

Update the factory methods in the same file:

```csharp
    public static ResultOf<T, E> Ok<T, E>(T value) => new ResultOf<T, E>.Ok(new Ok<T>(value));

    public static ResultOf<T, E> Fail<T, E>(E error) => new ResultOf<T, E>.Fail(new Fail<E>(error));

    public static ResultOf<T, string> Ok<T>(T value) => new ResultOf<T, string>.Ok(new Ok<T>(value));

    public static ResultOf<T, string> Fail<T>(string error)
        => new ResultOf<T, string>.Fail(new Fail<string>(error));
```

- [ ] **Step 4: Build and fix the fallout**

```bash
dotnet build Corsinvest.Fx.sln -v q --nologo -nodeReuse:false
```

`ResultOfExtensions.cs` uses `ok.Value` and `error.ErrorValue` in 38 places; both property names
survive the move, so most call sites compile untouched. Fix only what the compiler flags.
`TryHelper.cs` has 4 such uses.

- [ ] **Step 5: Run tests to verify they pass**

```bash
./tests/Functional/Corsinvest.Fx.Functional.Tests/bin/Debug/net8.0/Corsinvest.Fx.Functional.Tests.exe
```

Expected: all pass. Update any `ResultOf*Tests` assertions that reach into the old wrapper shape.

- [ ] **Step 6: Commit**

```bash
git add src/Functional/Corsinvest.Fx.Functional/ tests/Functional/
git commit -m "refactor(functional): express ResultOf<T,E> with the generic union attribute"
```

---

### Task 8: Migrate examples and remaining tests

**Files:**
- Modify: `examples/04_UnionTypes.cs`, `examples/01_OptionBasics.cs`, `examples/02_ResultOfValidation.cs`
- Modify: `tests/Functional/Corsinvest.Fx.Functional.Tests/UnionTests.cs`, `SimpleTest.cs`, `UnionGeneratorDiagnosticsTests.cs`, `UnionGeneratorRobustnessTests.cs`, `UnionExhaustivenessTests.cs`, `UnionCodeFixTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–7.
- Produces: no new API; this task moves existing call sites onto the generic attribute.

- [ ] **Step 1: Convert the examples**

In `examples/04_UnionTypes.cs`, replace the three `[Union]` declarations (lines 5–30) with:

```csharp
// Payment methods
public record CreditCard(string Number, string ExpiryDate);
public record PayPal(string Email);
public record BankTransfer(string Iban, string Bic);

[Union<CreditCard, PayPal, BankTransfer>]
public abstract partial record PaymentMethod;

// API response states
public record Loading;
public record Success(UserData User);
public record Failure(string Message);

[Union<Loading, Success, Failure>]
public abstract partial record ApiResponse;

// Geometric shapes
public record Circle(double Radius);
public record Rectangle(double Width, double Height);
public record Triangle(double SideA, double SideB, double SideC);

[Union<Circle, Rectangle, Triangle>]
public abstract partial record Shape;
```

Then update every `Match` and `switch` in the file: handlers now receive the case type directly,
so `creditCard.Number` still works, and the switch arms become
`PaymentMethod.CreditCard(var card) => card.Number`.

- [ ] **Step 2: Run the examples**

```bash
dotnet build examples/Corsinvest.Fx.Examples.csproj -v q --nologo -nodeReuse:false
dotnet run --project examples/Corsinvest.Fx.Examples.csproj --no-build -nodeReuse:false
```

Expected: build clean, every example section prints as before.

- [ ] **Step 3: Convert the union test fixtures**

In each of `UnionTests.cs`, `SimpleTest.cs`, `UnionGeneratorDiagnosticsTests.cs`,
`UnionGeneratorRobustnessTests.cs`, `UnionExhaustivenessTests.cs`, `UnionCodeFixTests.cs`,
replace nested-case declarations with external types plus `[Union<...>]`.

`UnionGeneratorDiagnosticsTests` tests UNION002/UNION003, which are specific to the nested model.
Those two rules disappear in Task 9, so delete the tests that assert on them and keep only
`UnionGenerator_GeneratesCode...`-style tests, rewritten against the generic attribute.

- [ ] **Step 4: Run the full suite**

```bash
dotnet build Corsinvest.Fx.sln -v q --nologo -nodeReuse:false
for exe in $(find tests -name "*.Tests.exe" -path "*/bin/Debug/*" | grep -v "ref/"); do "./$exe"; done
```

Expected: 0 failures across all suites.

- [ ] **Step 5: Commit**

```bash
git add examples/ tests/
git commit -m "refactor: move examples and tests onto the generic union attribute"
```

---

### Task 9: Remove the nested-case attribute

**Files:**
- Delete: `src/Functional/Corsinvest.Fx.Functional/UnionAttribute.cs`
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/UnionGenerator.cs` — remove the nested path
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/UnionSymbolHelper.cs` — drop `UnionAttributeMetadataName`
- Modify: `src/Functional/Corsinvest.Fx.Functional.Generators/AnalyzerReleases.Unshipped.md`

**Interfaces:**
- Consumes: nothing new.
- Produces: `[Union]` no longer exists; UNION002 and UNION003 are retired.

- [ ] **Step 1: Verify nothing still uses it**

```bash
grep -rn "\[Union\]" --include="*.cs" . | grep -v obj/ | grep -v "/bin/"
```

Expected: no hits outside comments. Fix any that remain before continuing.

- [ ] **Step 2: Delete the attribute and the nested generator path**

```bash
git rm src/Functional/Corsinvest.Fx.Functional/UnionAttribute.cs
```

In `UnionGenerator.cs` remove: `UnionMustBePartialDescriptor`, `VariantMustBePartialDescriptor`,
`IsUnionCandidate`, `GetUnionGenerationContext`, `IsUnionAttribute`, `ProcessUnionGeneration`,
`GenerateUnion`, `GenerateUnionSource`, `GenerateVariant`, `GenerateUnionExtensions`,
`GenerateMatchMethods`, `GenerateAsyncMatchMethods`, `GenerateTryGetMethods`, and the
`UnionInfo` / `VariantInfo` / `ParamInfo` / `UnionGenerationContext` records — along with the
`unionDeclarations` provider registration in `Initialize`.

In `UnionSymbolHelper.cs` remove the `UnionAttributeMetadataName` constant and the branch in
`IsUnionAttribute` that compares against it.

- [ ] **Step 3: Retire the diagnostics**

In `AnalyzerReleases.Unshipped.md`, move UNION002 and UNION003 from `### New Rules` to a new
`### Removed Rules` section:

```
### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UNION002 | Design | Error | Union type must be partial
UNION003 | Design | Error | Union variant must be partial
```

- [ ] **Step 4: Bump the major version**

Removing `[Union]` breaks every consumer, so in `Directory.Build.props` change:

```xml
    <Version>2.0.0</Version>
    <AssemblyVersion>2.0.0.0</AssemblyVersion>
    <FileVersion>2.0.0.0</FileVersion>
```

- [ ] **Step 5: Verify the whole solution**

```bash
dotnet build Corsinvest.Fx.sln -v q --nologo -nodeReuse:false
for exe in $(find tests -name "*.Tests.exe" -path "*/bin/Debug/*" | grep -v "ref/"); do "./$exe"; done
dotnet run --project examples/Corsinvest.Fx.Examples.csproj --no-build -nodeReuse:false
```

Expected: `Avvisi: 0, Errori: 0`, all tests pass, examples run.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor!: remove nested-case Union attribute in favour of [Union<T1..T8>]"
```

---

### Task 10: Documentation

**Files:**
- Modify: `src/Functional/Corsinvest.Fx.Functional/docs/Union.md`
- Modify: `src/Functional/Corsinvest.Fx.Functional/docs/Option.md`, `docs/ResultOf.md`
- Modify: `src/Functional/Corsinvest.Fx.Functional/README.md`, `README.md`

**Interfaces:**
- Consumes: the final API from Tasks 1–9.
- Produces: documentation only.

- [ ] **Step 1: Rewrite the Union.md declaration sections**

Replace every nested-case example with the generic form. The existing "Switch Expressions",
"Comparison with C# 15 union types" and "Diagnostics" sections stay, with these edits:

- Add UNION008 and UNION009 to the diagnostics table.
- In the C# 15 comparison, change the "Same type in several unions" row from ❌ to ✅, and rewrite
  the surrounding paragraph: the model now composes external types like C# 15 does, while keeping
  the closed hierarchy, so the remaining differences are ad hoc unions and native syntax.
- Add a "Wrapper names" section documenting the eight naming rules and `[UnionCaseName<T>]`.

- [ ] **Step 2: Verify every documented snippet compiles**

Create a scratch project referencing the built package, paste each documented snippet, and build.
Any snippet that does not compile is a documentation bug — fix the doc, not the test.

- [ ] **Step 3: Check that local links still resolve**

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
git commit -m "docs(union): document the generic union attribute and naming rules"
```
