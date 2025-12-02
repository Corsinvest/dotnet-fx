using System.Reflection;
using Corsinvest.Fx.CompileTime.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Corsinvest.Fx.CompileTime.Models;

/// <summary>
/// Represents information about an invocation to a CompileTime method.
/// </summary>
internal class InvocationInfo
{
    private InvocationInfo(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol)
    {
        _invocation = invocation;
        MethodSymbol = methodSymbol;
        Parameters = ExtractParameterValues(invocation);
    }

    private readonly InvocationExpressionSyntax _invocation;

    public IMethodSymbol MethodSymbol { get; }
    public string FilePath => _invocation.SyntaxTree.FilePath;
    public object[] Parameters { get; }

    // Optional properties for modern interceptor format (populated via object initializer when available)
    public int InterceptableVersion { get; init; }
    public string InterceptableData { get; init; } = string.Empty;
    public string InvocationId { get; } = Guid.NewGuid().ToString();
    public int Line => GetMethodNamePosition().Line;
    public int Character => GetMethodNamePosition().Character;

    private (int Line, int Character) GetMethodNamePosition()
    {
        // For member access like "RealExecutionDemo.CalculateFibonacci()"
        if (_invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodNameSpan = memberAccess.Name.GetLocation().GetLineSpan();
            return (methodNameSpan.StartLinePosition.Line + 1, methodNameSpan.StartLinePosition.Character + 1);
        }
        else
        {
            // For direct method calls like "CalculateFibonacci()"
            var invocationSpan = _invocation.GetLocation().GetLineSpan();
            return (invocationSpan.StartLinePosition.Line + 1, invocationSpan.StartLinePosition.Character + 1);
        }
    }

    private static object[] ExtractParameterValues(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList?.Arguments.Count == 0) { return []; }

        var parameters = new List<object>();

        foreach (var argument in invocation.ArgumentList!.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax literal)
            {
                var value = ExtractLiteralValue(literal);
                if (value != null)
                {
                    parameters.Add(value);
                }
                else
                {
                    // If we can't extract a constant value, return empty array to indicate non-constant parameters
                    return [];
                }
            }
            else
            {
                // Non-literal parameter - not supported for compile-time execution
                return [];
            }
        }

        return [.. parameters];
    }

    private static object? ExtractLiteralValue(LiteralExpressionSyntax literal)
        => literal.Token.ValueText switch
        {
            var text when int.TryParse(text, out var intValue) => intValue,
            var text when long.TryParse(text, out var longValue) && text.EndsWith("L") => longValue,
            var text when double.TryParse(text, out var doubleValue) => doubleValue,
            var text when float.TryParse(text, out var floatValue) && text.EndsWith("f") => floatValue,
            var text when decimal.TryParse(text, out var decimalValue) && text.EndsWith("m") => decimalValue,
            var text when bool.TryParse(text, out var boolValue) => boolValue,
            var text when text.Length == 1 => text[0], // char
            var text => text // string
        };

    public static string CreateParameterSuffix(object[] parameters)
        => parameters == null || parameters.Length == 0
            ? string.Empty
            : $"_{CompileTimeHelper.GenerateShortHash(string.Join("|", parameters.Select(p => p?.ToString() ?? "null")))}"; // Create a deterministic hash from all parameter values

    private static (int version, string data)? TryGetInterceptableLocation(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        try
        {
            // Try to find GetInterceptableLocation API (available in Roslyn 4.9+)
            // Signature: InterceptableLocation? GetInterceptableLocation(SyntaxNode node, CancellationToken cancellationToken)
            var getInterceptableLocationMethod = typeof(SemanticModel)
                .GetMethod("GetInterceptableLocation", BindingFlags.Public | BindingFlags.Instance);

            if (getInterceptableLocationMethod == null)
            {
                // API not available - will use legacy format as fallback
                return null;
            }

            // Call: model.GetInterceptableLocation(invocation, CancellationToken.None)
            var interceptableLocation = getInterceptableLocationMethod.Invoke(model, [invocation, CancellationToken.None]);

            if (interceptableLocation == null)
            {
                // Location not interceptable (e.g., method doesn't have [InterceptsLocation] support)
                return null;
            }

            // Extract Version and Data properties from InterceptableLocation struct
            var locationType = interceptableLocation.GetType();
            var versionProperty = locationType.GetProperty("Version");
            var dataProperty = locationType.GetProperty("Data");

            if (versionProperty == null || dataProperty == null)
            {
                // Unexpected type structure
                return null;
            }

            var version = (int)versionProperty.GetValue(interceptableLocation)!;
            var data = (string)dataProperty.GetValue(interceptableLocation)!;

            return (version, data);
        }
        catch
        {
            // API not available or failed - fall back to legacy .NET 8 syntax
            return null;
        }
    }

    public static InvocationInfo? Get(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol) { return null; }
        if (!CompileTimeHelper.IsValid(methodSymbol)) { return null; }

        var interceptableLocation = TryGetInterceptableLocation(invocation, model);
        return interceptableLocation.HasValue
                ? new(invocation, methodSymbol)
                {
                    InterceptableVersion = interceptableLocation.Value.version,
                    InterceptableData = interceptableLocation.Value.data
                }
                : new(invocation, methodSymbol);
    }
}
