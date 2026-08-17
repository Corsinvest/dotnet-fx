using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Tests covering generator robustness: identifier collisions in the generated code
/// and correct recognition of the [Union] attribute.
/// </summary>
public class UnionGeneratorRobustnessTests
{
    [Fact]
    public async Task UnionGenerator_ProducesValidCode_WhenVariantsDifferOnlyByCase()
    {
        // Variant-derived local names are lowercased, so Error/ERROR both yield "error".
        // Each occurrence lives in its own switch arm or method scope, so this is legal C#.
        // This test locks that in against future changes to name generation.
        var source = """
            using Corsinvest.Fx.Functional;

            [Union]
            public partial record Outcome
            {
                public partial record Error(string Message);
                public partial record ERROR(int Code);
            }
            """;

        var (_, generatedTrees, compilationErrors) = await GetGeneratorOutputAsync(source);

        var outcomeTree = generatedTrees.FirstOrDefault(t => t.ToString().Contains("public partial record Outcome"));
        Assert.NotNull(outcomeTree);

        Assert.Empty(compilationErrors.Where(d => d.Id is "CS0128" or "CS0136" or "CS0102"));
    }

    [Fact]
    public async Task UnionGenerator_DoesNotGenerate_ForUnrelatedAttributeContainingUnionInName()
    {
        var source = """
            using System;

            public sealed class MyUnionHelperAttribute : Attribute { }

            [MyUnionHelper]
            public partial record NotAUnion
            {
                public partial record Alpha(int Value);
            }
            """;

        var (_, generatedTrees, _) = await GetGeneratorOutputAsync(source);

        // A type carrying an unrelated attribute whose name merely contains "Union"
        // must not be treated as a discriminated union.
        var generated = generatedTrees.FirstOrDefault(t => t.ToString().Contains("public partial record NotAUnion"));
        Assert.Null(generated);
    }

    [Fact]
    public async Task UnionGenerator_Generates_WhenAttributeUsedWithExplicitSuffix()
    {
        var source = """
            using Corsinvest.Fx.Functional;

            [UnionAttribute]
            public partial record Shape
            {
                public partial record Circle(double Radius);
            }
            """;

        var (_, generatedTrees, _) = await GetGeneratorOutputAsync(source);

        var generated = generatedTrees.FirstOrDefault(t => t.ToString().Contains("public partial record Shape"));
        Assert.NotNull(generated);
    }

    [Fact]
    public async Task UnionGenerator_Generates_WhenAttributeIsFullyQualified()
    {
        var source = """
            [Corsinvest.Fx.Functional.Union]
            public partial record Shape
            {
                public partial record Circle(double Radius);
            }
            """;

        var (_, generatedTrees, _) = await GetGeneratorOutputAsync(source);

        var generated = generatedTrees.FirstOrDefault(t => t.ToString().Contains("public partial record Shape"));
        Assert.NotNull(generated);
    }

    private static async Task<(ImmutableArray<Diagnostic> Diagnostics,
                               ImmutableArray<SyntaxTree> GeneratedTrees,
                               ImmutableArray<Diagnostic> CompilationErrors)> GetGeneratorOutputAsync(string source)
    {
        await Task.CompletedTask;

        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var functionalAssembly = typeof(UnionAttribute).Assembly;
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
