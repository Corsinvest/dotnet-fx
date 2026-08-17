using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Covers UNION004 (missing variant) and UNION005 (CS8509 suppression).
/// </summary>
public class UnionExhaustivenessTests
{
    private const string UnionDeclaration = """
        using Corsinvest.Fx.Functional;

        [Union]
        public partial record Payment
        {
            public partial record CreditCard(string Number, string Expiry);
            public partial record PayPal(string Email);
            public partial record Crypto(string Wallet);
        }
        """;

    // ---- UNION004: variants that are not handled ---------------------------

    [Fact]
    public async Task Reports_MissingVariant_WhenDiscardHidesIt()
    {
        // The compiler stays silent here: the discard makes the switch exhaustive as far as
        // it is concerned. This is exactly the case UNION004 exists for.
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p) => p switch
                {
                    Payment.CreditCard => 1m,
                    Payment.PayPal => 2m,
                    _ => 0m
                };
            }
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION004"));
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Crypto", diagnostic.GetMessage());
        Assert.DoesNotContain("PayPal", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_MissingVariant_InSwitchStatement()
    {
        // Switch statements get no compiler exhaustiveness diagnostic at all.
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p)
                {
                    switch (p)
                    {
                        case Payment.CreditCard: return 1m;
                        case Payment.PayPal: return 2m;
                    }
                    return 0m;
                }
            }
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION004"));
        Assert.Contains("Crypto", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_AllVariants_WhenOnlyDiscardPresent()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p) => p switch { _ => 0m };
            }
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION004"));
        Assert.Contains("CreditCard", diagnostic.GetMessage());
        Assert.Contains("PayPal", diagnostic.GetMessage());
        Assert.Contains("Crypto", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_WhenClauseDoesNotCoverVariant()
    {
        // A guarded arm can fail, so it does not prove the variant is handled.
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p) => p switch
                {
                    Payment.CreditCard c when c.Number.Length > 4 => 1m,
                    Payment.PayPal => 2m,
                    Payment.Crypto => 3m,
                    _ => 0m
                };
            }
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION004"));
        Assert.Contains("CreditCard", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_RefutableSubpatternDoesNotCoverVariant()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static string F(Payment p) => p switch
                {
                    Payment.CreditCard { Number: "x" } => "c",
                    Payment.PayPal => "p",
                    Payment.Crypto => "y",
                    _ => "?"
                };
            }
            """);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION004"));
        Assert.Contains("CreditCard", diagnostic.GetMessage());
    }

    // ---- UNION004: shapes that DO cover a variant --------------------------

    [Theory]
    [InlineData("Payment.CreditCard => 1m, Payment.PayPal => 2m, Payment.Crypto => 3m")]
    [InlineData("Payment.CreditCard c => 1m, Payment.PayPal p => 2m, Payment.Crypto y => 3m")]
    [InlineData("Payment.CreditCard { } => 1m, Payment.PayPal { } => 2m, Payment.Crypto { } => 3m")]
    [InlineData("Payment.CreditCard or Payment.PayPal => 1m, Payment.Crypto => 3m")]
    [InlineData("Payment.CreditCard(var n, var e) => 1m, Payment.PayPal(var m) => 2m, Payment.Crypto(var w) => 3m")]
    [InlineData("Payment.CreditCard(_, _) => 1m, Payment.PayPal(_) => 2m, Payment.Crypto(_) => 3m")]
    [InlineData("Payment.CreditCard { Number: var n } => 1m, Payment.PayPal { Email: var e } => 2m, Payment.Crypto { Wallet: var w } => 3m")]
    public async Task DoesNotReport_WhenEveryVariantIsCovered(string arms)
    {
        var diagnostics = await GetDiagnosticsAsync($$"""
            public static class T
            {
                public static decimal F(Payment p) => p switch { {{arms}} };
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Id == "UNION004"));
    }

    [Fact]
    public async Task DoesNotReport_ForNonUnionSwitch()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static string F(object o) => o switch { int => "i", string => "s", _ => "?" };
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Id == "UNION004"));
    }

    // ---- UNION005: CS8509 suppression --------------------------------------

    [Fact]
    public async Task Suppresses_CS8509_WhenAllVariantsHandled()
    {
        // No discard arm, every variant handled: the hierarchy is closed, so the
        // compiler's "not exhaustive" warning is wrong and gets suppressed.
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p) => p switch
                {
                    Payment.CreditCard => 1m,
                    Payment.PayPal => 2m,
                    Payment.Crypto => 3m,
                };
            }
            """);

        var cs8509 = diagnostics.Where(d => d.Id == "CS8509").ToList();
        Assert.All(cs8509, d => Assert.True(d.IsSuppressed, "CS8509 should be suppressed"));
    }

    [Fact]
    public async Task DoesNotSuppress_CS8509_WhenVariantMissing()
    {
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p) => p switch
                {
                    Payment.CreditCard => 1m,
                    Payment.PayPal => 2m,
                };
            }
            """);

        var cs8509 = Assert.Single(diagnostics.Where(d => d.Id == "CS8509"));
        Assert.False(cs8509.IsSuppressed);
    }

    // ---- UNION006 / UNION007: IDE "populate switch" suppression ------------

    [Fact]
    public void Suppressor_Covers_PopulateSwitchDiagnostics()
    {
        // IDE0010 and IDE0072 offer to add a default arm, which is exactly the pattern
        // UNION004 exists to prevent. Both must be in the suppressor's remit.
        var suppressed = new UnionExhaustivenessSuppressor()
            .SupportedSuppressions
            .Select(s => s.SuppressedDiagnosticId)
            .ToList();

        Assert.Contains("CS8509", suppressed);
        Assert.Contains("IDE0010", suppressed);
        Assert.Contains("IDE0072", suppressed);
    }

    [Fact]
    public void Suppressor_UsesDistinctIds_PerSuppressedDiagnostic()
    {
        // A SuppressionDescriptor targets one diagnostic id, so each needs its own descriptor.
        var descriptors = new UnionExhaustivenessSuppressor().SupportedSuppressions;

        Assert.Equal(descriptors.Length, descriptors.Select(d => d.Id).Distinct().Count());
        Assert.Equal(descriptors.Length, descriptors.Select(d => d.SuppressedDiagnosticId).Distinct().Count());
    }

    [Fact]
    public async Task Suppressor_HandlesSwitchStatements_NotJustExpressions()
    {
        // IDE0010 fires on statements, which the suppressor originally did not inspect at all.
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p)
                {
                    switch (p)
                    {
                        case Payment.CreditCard: return 1m;
                        case Payment.PayPal: return 2m;
                        case Payment.Crypto: return 3m;
                    }
                    return 0m;
                }
            }
            """);

        Assert.Empty(diagnostics.Where(d => d.Id == "UNION004"));
    }

    // ---- effect on the build, not just on the diagnostic list --------------

    [Fact]
    public async Task MissingVariant_BreaksBuild_WhenWarningsAreErrors()
    {
        // Directory.Build.props sets TreatWarningsAsErrors, so UNION004 must actually
        // stop a build - emitting the diagnostic is not the same as failing.
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p) => p switch
                {
                    Payment.CreditCard => 1m,
                    Payment.PayPal => 2m,
                    _ => 0m
                };
            }
            """,
            warningsAsErrors: true);

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "UNION004"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.False(diagnostic.IsSuppressed);
    }

    [Fact]
    public async Task ExhaustiveSwitchWithoutDiscard_BuildsClean_WhenWarningsAreErrors()
    {
        // The mirror case: with every variant handled and no discard arm, CS8509 would
        // otherwise be promoted to an error. The suppressor has to hold under promotion.
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p) => p switch
                {
                    Payment.CreditCard => 1m,
                    Payment.PayPal => 2m,
                    Payment.Crypto => 3m,
                };
            }
            """,
            warningsAsErrors: true);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && !d.IsSuppressed));
    }

    [Fact]
    public async Task Union004_CanBeSilencedByConfiguration()
    {
        // Consumers must be able to opt out, like with any other analyzer rule.
        var diagnostics = await GetDiagnosticsAsync("""
            public static class T
            {
                public static decimal F(Payment p) => p switch
                {
                    Payment.CreditCard => 1m,
                    Payment.PayPal => 2m,
                    _ => 0m
                };
            }
            """,
            severityOverride: ReportDiagnostic.Suppress);

        Assert.Empty(diagnostics.Where(d => d.Id == "UNION004" && !d.IsSuppressed));
    }

    // ---- infrastructure ----------------------------------------------------

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string usageSource,
        bool warningsAsErrors = false,
        ReportDiagnostic? severityOverride = null)
    {
        await Task.CompletedTask;

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .Append(MetadataReference.CreateFromFile(typeof(UnionAttribute).Assembly.Location))
            .ToList();

        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithGeneralDiagnosticOption(warningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default);

        if (severityOverride is { } severity)
        {
            options = options.WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic> { ["UNION004"] = severity });
        }

        var compilation = CSharpCompilation.Create(
            "ExhaustivenessTest",
            [
                CSharpSyntaxTree.ParseText(UnionDeclaration),
                CSharpSyntaxTree.ParseText(usageSource)
            ],
            references,
            options);

        // Run the union generator first so the variant types actually exist.
        var driver = CSharpGeneratorDriver.Create(new UnionGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var withGenerated, out _);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new UnionExhaustivenessAnalyzer(),
            new UnionExhaustivenessSuppressor());

        // GetAllDiagnosticsAsync includes compiler diagnostics with suppressions applied,
        // which is what makes the UNION005 assertions meaningful.
        return await withGenerated
            .WithAnalyzers(analyzers)
            .GetAllDiagnosticsAsync();
    }
}
