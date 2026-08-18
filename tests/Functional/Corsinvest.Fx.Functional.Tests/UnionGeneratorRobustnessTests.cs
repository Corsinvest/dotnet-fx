/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Generator robustness tests that survive the move from the retired <c>[Union]</c>/
/// <c>[Union&lt;...&gt;]</c> attributes to the <see cref="IUnion{T1,T2}"/> marker interface.
/// </summary>
/// <remarks>
/// This file originally covered two concerns specific to the attribute-driven generator, neither
/// of which applies to the interface-driven one (<c>UnionGenerator.cs</c>, current
/// <c>BuildUnionInfo</c>/<c>GenerateUnion</c> pipeline):
/// <list type="bullet">
/// <item>
/// Attribute-name recognition (<c>[Union]</c> vs <c>[UnionAttribute]</c> vs
/// <c>[Corsinvest.Fx.Functional.Union]</c> vs an unrelated attribute whose name merely contains
/// "Union"). The current generator does not look at attributes at all - it matches a root by
/// walking <c>root.Interfaces</c> for one named <c>IUnion</c> in the
/// <c>Corsinvest.Fx.Functional</c> namespace (see <c>BuildUnionInfo</c>). Confirmed by
/// <c>grep -rn "UnionAttribute" src/Functional/Corsinvest.Fx.Functional.Generators</c> returning
/// no hits. These tests (<c>UnionGenerator_DoesNotGenerate_ForUnrelatedAttributeContainingUnionInName</c>,
/// <c>UnionGenerator_Generates_WhenAttributeUsedWithExplicitSuffix</c>,
/// <c>UnionGenerator_Generates_WhenAttributeIsFullyQualified</c>) tested dead code paths and were
/// removed rather than rewritten - there is no interface-form equivalent of "attribute name
/// recognition" to redirect them at. <c>DoesNotGenerate_ForUnrelatedInterface</c> in
/// <c>UnionGeneratorTests.cs</c> is the interface form's analogous "don't false-positive on an
/// unrelated marker" coverage.
/// </item>
/// <item>
/// Local-variable identifiers derived by lowercasing a case name (old generator:
/// <c>variant.Name.ToLowerInvariant()</c> for the <c>Match</c>/<c>TryGet</c> local, so
/// <c>Error</c>/<c>ERROR</c> both produced the local <c>error</c> - legal only because each
/// occurrence lived in its own method/switch-arm scope). The current generator never derives a
/// local from a case name: every generated <c>Match</c>/<c>MatchAsync</c>/<c>TryGet</c> uses the
/// fixed identifier <c>wrapped</c> (see <c>GenerateMatch</c>/<c>GenerateTryGet</c> in
/// <c>UnionGenerator.cs</c>), so two case types whose names differ only by case cannot collide on
/// a generated local - there is no lowercasing step left to collide. What CAN still collide is the
/// *wrapper type name* itself, which <c>UNION008</c> exists to catch and which
/// <c>Reports_UNION008_WhenTwoCasesCannotBeNamedApart</c> in <c>UnionGeneratorTests.cs</c> already
/// covers. <c>UnionGenerator_ProducesValidCode_WhenVariantsDifferOnlyByCase</c> was removed as
/// dead rather than rewritten into a duplicate of that test.
/// </item>
/// </list>
/// What remains below is robustness coverage that still has a real interface-form counterpart:
/// unrecognized/malformed inputs must not make the generator throw or emit garbage.
/// </remarks>
public class UnionGeneratorRobustnessTests
{
    [Fact]
    public async Task UnionGenerator_DoesNotGenerate_ForTypeWithNoBaseList()
    {
        // A partial record with no base list at all (no IUnion<...>, no anything) must not be
        // mistaken for a union root - BuildUnionInfo's syntax predicate requires a BaseList.
        var source = """
            public partial record NotAUnion
            {
                public partial record Alpha(int Value);
            }
            """;

        var (_, generatedTrees, _) = await GetGeneratorOutputAsync(source);

        var generated = generatedTrees.FirstOrDefault(t => t.ToString().Contains("public partial record NotAUnion")
                                                          || t.ToString().Contains("public abstract partial record NotAUnion"));
        Assert.Null(generated);
    }

    [Fact]
    public async Task UnionGenerator_DoesNotGenerate_ForBaseListWithUnrelatedInterface()
    {
        // A base list that exists but names something other than IUnion<...> must not trigger
        // generation either - only an interface literally named IUnion in
        // Corsinvest.Fx.Functional counts (BuildUnionInfo's marker lookup).
        var source = """
            public interface IMarker { }

            public partial record NotAUnion : IMarker
            {
                public partial record Alpha(int Value);
            }
            """;

        var (_, generatedTrees, _) = await GetGeneratorOutputAsync(source);

        var generated = generatedTrees.FirstOrDefault(t => t.ToString().Contains("record NotAUnion"));
        Assert.Null(generated);
    }

    [Fact]
    public async Task UnionGenerator_Generates_ForValidIUnionRoot()
    {
        // Sanity check that the harness itself (GetGeneratorOutputAsync) still drives real
        // generation end to end, so the two negative tests above are meaningful.
        var source = """
            using Corsinvest.Fx.Functional;

            public record Alpha(int Value);
            public record Beta(string Value);

            public abstract partial record Shape : IUnion<Alpha, Beta>;
            """;

        var (_, generatedTrees, compilationErrors) = await GetGeneratorOutputAsync(source);

        var generated = generatedTrees.FirstOrDefault(t => t.ToString().Contains("public abstract partial record Shape"));
        Assert.NotNull(generated);
        Assert.Empty(compilationErrors);
    }

    private static async Task<(ImmutableArray<Diagnostic> Diagnostics,
                               ImmutableArray<SyntaxTree> GeneratedTrees,
                               ImmutableArray<Diagnostic> CompilationErrors)> GetGeneratorOutputAsync(string source)
    {
        await Task.CompletedTask;

        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var functionalAssembly = typeof(UnionCaseNameAttribute<>).Assembly;
        var functionalReference = MetadataReference.CreateFromFile(functionalAssembly.Location);

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .Append(functionalReference)
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new UnionGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);

        var runResult = driver.GetRunResult();

        var allDiagnostics = runResult.Results
            .SelectMany(r => r.Diagnostics)
            .ToImmutableArray();

        var generatedTrees = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SyntaxTree)
            .ToImmutableArray();

        var compilationErrors = outputCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return (allDiagnostics, generatedTrees, compilationErrors);
    }
}
