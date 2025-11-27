using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional.Tests;

public class UnionGeneratorDiagnosticsTests
{
    [Fact]
    public async Task UnionGenerator_ReportsError_WhenRecordIsNotPartial()
    {
        var source = """
            using Corsinvest.Fx.Functional;

            [Union]
            public record Shape  // Missing 'partial' keyword
            {
                public partial record Circle(double Radius);
                public partial record Rectangle(double Width, double Height);
            }
            """;

        var diagnostics = await GetGeneratorDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION002"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Shape", diagnostic.GetMessage());
        Assert.Contains("partial", diagnostic.GetMessage());
    }

    [Fact]
    public async Task UnionGenerator_ReportsError_WhenVariantIsNotPartial()
    {
        var source = """
            using Corsinvest.Fx.Functional;

            [Union]
            public partial record Shape
            {
                public record Circle(double Radius);  // Missing 'partial' keyword
                public partial record Rectangle(double Width, double Height);
            }
            """;

        var diagnostics = await GetGeneratorDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION003"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Circle", diagnostic.GetMessage());
        Assert.Contains("Shape", diagnostic.GetMessage());
        Assert.Contains("partial", diagnostic.GetMessage());
    }

    [Fact]
    public async Task UnionGenerator_ReportsMultipleErrors_WhenMultipleVariantsNotPartial()
    {
        var source = """
            using Corsinvest.Fx.Functional;

            [Union]
            public partial record PaymentMethod
            {
                public record CreditCard(string Number);      // Missing 'partial'
                public partial record PayPal(string Email);   // Correct
                public record BankTransfer(string Iban);      // Missing 'partial'
            }
            """;

        var diagnostics = await GetGeneratorDiagnosticsAsync(source);

        var variantErrors = diagnostics.Where(d => d.Id == "UNION003").ToList();
        Assert.Equal(2, variantErrors.Count);

        Assert.Contains(variantErrors, d => d.GetMessage().Contains("CreditCard"));
        Assert.Contains(variantErrors, d => d.GetMessage().Contains("BankTransfer"));
    }

    [Fact]
    public async Task UnionGenerator_NoErrors_WhenAllArePartial()
    {
        var source = """
            using Corsinvest.Fx.Functional;

            [Union]
            public partial record Shape
            {
                public partial record Circle(double Radius);
                public partial record Rectangle(double Width, double Height);
            }
            """;

        var diagnostics = await GetGeneratorDiagnosticsAsync(source);

        var unionErrors = diagnostics.Where(d => d.Id.StartsWith("UNION")).ToList();
        Assert.Empty(unionErrors);
    }

    [Fact]
    public async Task UnionGenerator_GeneratesCodeWithPartialVariantsOnly()
    {
        var source = """
            using Corsinvest.Fx.Functional;

            [Union]
            public partial record ApiResponse
            {
                public partial record Success(string Data);     // Will be included
                public record Error(string Message);            // Will be excluded (not partial)
                public partial record Loading();                // Will be included
            }
            """;

        var (diagnostics, generatedTrees) = await GetGeneratorOutputAsync(source);

        // Should report error for non-partial variant
        var error = Assert.Single(diagnostics.Where(d => d.Id == "UNION003"));
        Assert.Contains("Error", error.GetMessage());

        // Should still generate code with the 2 partial variants
        var apiResponseTree = generatedTrees.FirstOrDefault(t => t.ToString().Contains("public partial record ApiResponse"));
        Assert.NotNull(apiResponseTree);

        var generatedSource = apiResponseTree.ToString();
        Assert.Contains("Success", generatedSource);
        Assert.Contains("Loading", generatedSource);

        // Verify Error variant is NOT in generated code
        Assert.DoesNotContain("public sealed partial record Error", generatedSource);
    }

    [Fact]
    public async Task UnionGenerator_NoCodeGeneration_WhenMainRecordNotPartial()
    {
        var source = """
            using Corsinvest.Fx.Functional;

            [Union]
            public record Shape  // Not partial
            {
                public partial record Circle(double Radius);
            }
            """;

        var (diagnostics, generatedTrees) = await GetGeneratorOutputAsync(source);

        // Should report UNION002 error
        Assert.Contains(diagnostics, d => d.Id == "UNION002");

        // Should not generate any code for Shape
        var shapeCode = generatedTrees.FirstOrDefault(t => t.ToString().Contains("public partial record Shape"));
        Assert.Null(shapeCode);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetGeneratorDiagnosticsAsync(string source)
    {
        var (diagnostics, _) = await GetGeneratorOutputAsync(source);
        return diagnostics;
    }

    private static async Task<(ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<SyntaxTree> GeneratedTrees)> GetGeneratorOutputAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Get reference to the Functional library
        var functionalAssembly = typeof(UnionAttribute).Assembly;
        var functionalReference = MetadataReference.CreateFromFile(functionalAssembly.Location);

        // Create compilation
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                functionalReference
            },
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        // Run the generator
        var generator = new UnionGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var runResult = driver.GetRunResult();

        // Collect all diagnostics from the generator
        var allDiagnostics = runResult.Results
            .SelectMany(r => r.Diagnostics)
            .ToImmutableArray();

        // Get generated syntax trees
        var generatedTrees = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SyntaxTree)
            .ToImmutableArray();

        return (allDiagnostics, generatedTrees);
    }
}
