using Microsoft.CodeAnalysis;

namespace Corsinvest.Fx.CompileTime.Helpers;

internal static class CompileTimeHelper
{
    public static AttributeData GetAttributes(IMethodSymbol methodSymbol)
        => methodSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == nameof(CompileTimeAttribute))!;

    public static T GetNamedArgumentValue<T>(AttributeData attr, string argumentName, T defaultValue = default!)
    {
        // Check named arguments first
        var namedArg = attr.NamedArguments.FirstOrDefault(x => x.Key == argumentName);
        if (!namedArg.Equals(default(KeyValuePair<string, TypedConstant>)))
        {
            var value = namedArg.Value.Value;

            // Handle enum conversion for CacheStrategy
            if (typeof(T).IsEnum && value != null)
            {
                try
                {
                    return (T)Enum.ToObject(typeof(T), value);
                }
                catch
                {
                    return defaultValue;
                }
            }

            if (value is T namedValue) { return namedValue; }
        }

        return defaultValue;
    }

    public static bool IsValid(IMethodSymbol methodSymbol)
    {
        var attr = GetAttributes(methodSymbol);
        return attr != null
                && GetNamedArgumentValue(attr, nameof(CompileTimeAttribute.Enabled), new CompileTimeAttribute().Enabled);
    }

    public static string GenerateSafeClassName(string namespaceName, string className)
        => $"CompileTime_{GenerateShortHash(namespaceName + "." + className)}_{className}";

    public static string GenerateShortHash(string input)
    {
        if (string.IsNullOrEmpty(input)) { return "00000000"; }

        // Simple hash algorithm for consistent short identifiers
        uint hash = 2166136261u; // FNV offset basis
        foreach (char c in input)
        {
            hash ^= c;
            hash *= 16777619u; // FNV prime
        }

        return hash.ToString("X8");
    }

    public static string GetMethodContentHash(IMethodSymbol methodSymbol)
    {
        // Generate a hash based on method signature and content
        var methodSignature = $"{methodSymbol.ContainingType.ToDisplayString()}.{methodSymbol.Name}({string.Join(",", methodSymbol.Parameters.Select(p => p.Type.ToDisplayString()))})";
        return GenerateShortHash(methodSignature);
    }
}
