using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Covers the UNION004 code fix, which fills in the union cases a switch does not handle.
/// Each test asserts on the produced source, not merely that a fix was offered.
/// </summary>
public class UnionCodeFixTests
{
    private const string UnionDeclaration = """
        using Corsinvest.Fx.Functional;

        [Union]
        public partial record Payment
        {
            public partial record CreditCard(string Number, string Expiry);
            public partial record PayPal(string Email);
            public partial record Crypto();
        }
        """;

    [Fact]
    public async Task Fix_AddsMissingArm_WithDeconstruction()
    {
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Payment p) => p switch
                {
                    Payment.CreditCard(var number, var expiry) => number,
                    Payment.PayPal(var email) => email
                };
            }
            """);

        // Crypto has no positional members, so a bare type pattern is the right shape.
        Assert.Contains("Payment.Crypto => throw new System.NotImplementedException()", fixedSource);
    }

    [Fact]
    public async Task Fix_DeconstructsCaseData_UsingMemberNames()
    {
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Payment p) => p switch
                {
                    Payment.PayPal(var email) => email,
                    Payment.Crypto => "crypto"
                };
            }
            """);

        // Variable names come from the record's positional members.
        Assert.Contains("Payment.CreditCard(var number, var expiry)", fixedSource);
    }

    [Fact]
    public async Task Fix_AddsEveryMissingCase_AtOnce()
    {
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Payment p) => p switch
                {
                    Payment.PayPal(var email) => email
                };
            }
            """);

        Assert.Contains("Payment.CreditCard(var number, var expiry)", fixedSource);
        Assert.Contains("Payment.Crypto", fixedSource);
    }

    [Fact]
    public async Task Fix_KeepsDiscardArmLast()
    {
        // A discard arm must stay final, otherwise the added arms become unreachable.
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Payment p) => p switch
                {
                    Payment.PayPal(var email) => email,
                    _ => "other"
                };
            }
            """);

        var discardIndex = fixedSource.IndexOf("_ =>", StringComparison.Ordinal);
        var creditCardIndex = fixedSource.IndexOf("Payment.CreditCard", StringComparison.Ordinal);

        Assert.True(creditCardIndex >= 0, "the missing case should have been added");
        Assert.True(creditCardIndex < discardIndex, "added arms must come before the discard arm");
    }

    [Fact]
    public async Task Fix_WorksOnSwitchStatements()
    {
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Payment p)
                {
                    switch (p)
                    {
                        case Payment.PayPal(var email): return email;
                    }
                    return "?";
                }
            }
            """);

        Assert.Contains("case Payment.CreditCard(var number, var expiry):", fixedSource);
        Assert.Contains("case Payment.Crypto:", fixedSource);
    }

    [Fact]
    public async Task Fix_ProducesCodeThatResolvesTheDiagnostic()
    {
        // The real contract: after the fix, UNION004 is gone.
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Payment p) => p switch
                {
                    Payment.PayPal(var email) => email
                };
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(fixedSource);

        Assert.Empty(diagnostics.Where(d => d.Id == "UNION004"));
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && !d.IsSuppressed));
    }

    [Fact]
    public async Task Fix_EscapesKeywordMemberNames()
    {
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Shape s) => s switch
                {
                    Shape.Circle(var radius) => "circle"
                };
            }
            """,
            unionDeclaration: """
            using Corsinvest.Fx.Functional;

            [Union]
            public partial record Shape
            {
                public partial record Circle(double Radius);
                public partial record Triangle(double Base, double Height);
            }
            """);

        // 'Base' lower-cases to 'base', a keyword, so it has to be escaped.
        Assert.Contains("var @base", fixedSource);
    }

    [Fact]
    public async Task Fix_NamesGenericUnionVariable_AfterTheWrappedType()
    {
        // A generic-union wrapper (see UnionAttribute<T1..T8>) has exactly one positional
        // member, always called Value. The fix must name the local after the wrapped case
        // type instead - Pet.Cat(var cat), not the uninformative Pet.Cat(var value).
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Pet pet) => pet switch
                {
                    Pet.Cat(var cat) => cat.Name
                };
            }
            """,
            unionDeclaration: """
            using Corsinvest.Fx.Functional;

            public record Cat(string Name);
            public record Dog(string Name);

            [Union<Cat, Dog>]
            public abstract partial record Pet;
            """);

        Assert.Contains("Pet.Dog(var dog)", fixedSource);
    }

    [Fact]
    public async Task Fix_EscapesKeywordCaseTypeName_OnGenericUnion()
    {
        // 'Base' lower-cases to 'base', a keyword, so the wrapped-type-derived variable name
        // has to be escaped the same way member-name-derived ones are.
        var fixedSource = await ApplyFixAsync("""
            public static class T
            {
                public static string F(Shape shape) => shape switch
                {
                    Shape.Circle(var circle) => circle.Radius.ToString()
                };
            }
            """,
            unionDeclaration: """
            using Corsinvest.Fx.Functional;

            public record Circle(double Radius);
            public record Base(double Width);

            [Union<Circle, Base>]
            public abstract partial record Shape;
            """);

        Assert.Contains("Shape.Base(var @base)", fixedSource);
    }

    // ---- infrastructure ----------------------------------------------------

    private static async Task<string> ApplyFixAsync(string usageSource, string? unionDeclaration = null)
    {
        var (project, usageDocumentId) = CreateProject(usageSource, unionDeclaration ?? UnionDeclaration);

        var compilation = await project.GetCompilationAsync();
        var diagnostics = await compilation!
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new UnionExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        var union004 = diagnostics.Single(d => d.Id == "UNION004");

        var document = project.GetDocument(usageDocumentId)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            union004,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await new UnionExhaustivenessCodeFixProvider().RegisterCodeFixesAsync(context);

        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        var changedDocument = changedSolution.GetDocument(usageDocumentId)!;
        var formatted = await Microsoft.CodeAnalysis.Formatting.Formatter.FormatAsync(changedDocument);

        return (await formatted.GetTextAsync()).ToString();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string usageSource)
    {
        var (project, _) = CreateProject(usageSource, UnionDeclaration);

        var compilation = await project.GetCompilationAsync();

        return await compilation!
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new UnionExhaustivenessAnalyzer(),
                new UnionExhaustivenessSuppressor()))
            .GetAllDiagnosticsAsync();
    }

    private static (Project Project, DocumentId UsageDocumentId) CreateProject(string usageSource,
                                                                              string unionDeclaration)
    {
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .Append(MetadataReference.CreateFromFile(typeof(UnionAttribute).Assembly.Location))
            .ToList();

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var project = workspace.CurrentSolution
            .AddProject(projectId, "CodeFixTest", "CodeFixTest", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectMetadataReferences(projectId, references)
            .GetProject(projectId)!;

        // The generator runs ahead of time here: its output is added as a plain document so the
        // variant types exist for both the analyzer and the fix.
        var generated = RunGenerator(unionDeclaration, references);

        var usageDocumentId = DocumentId.CreateNewId(projectId);

        var solution = project.Solution
            .AddDocument(DocumentId.CreateNewId(projectId), "Union.cs", SourceText.From(unionDeclaration))
            .AddDocument(DocumentId.CreateNewId(projectId), "Union.g.cs", SourceText.From(generated))
            .AddDocument(usageDocumentId, "Usage.cs", SourceText.From(usageSource));

        return (solution.GetProject(projectId)!, usageDocumentId);
    }

    private static string RunGenerator(string unionDeclaration, List<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            "GenInput",
            [CSharpSyntaxTree.ParseText(unionDeclaration)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        // The nested [Union] shape emits "public partial record {Root}"; the generic
        // [Union<...>] shape emits "public abstract partial record {Root}" instead.
        return runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .First(s => s.Contains("public partial record") || s.Contains("public abstract partial record"));
    }
}
