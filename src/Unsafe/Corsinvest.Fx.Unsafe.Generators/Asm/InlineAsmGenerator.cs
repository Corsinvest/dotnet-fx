using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Corsinvest.Fx.Unsafe.Generators.Asm;

[Generator]
public class InlineAsmGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Debug support
#if DEBUG
        // Uncomment to attach debugger
        // if (!System.Diagnostics.Debugger.IsAttached)
        //     System.Diagnostics.Debugger.Launch();
#endif

        // Find partial methods with [InlineAsm] attribute
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsInlineAsmCandidate,
                transform: GetInlineAsmInfo)
            .Where(info => info is not null);

        context.RegisterSourceOutput(provider, (spc, info) => GenerateInlineAsm(spc, info!));
    }

    private static bool IsInlineAsmCandidate(SyntaxNode node, System.Threading.CancellationToken ct)
    {
        // Must be a partial method with attributes
        return node is MethodDeclarationSyntax
        {
            Modifiers: var mods,
            AttributeLists.Count: > 0
        } && mods.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    }

    private static InlineAsmInfo? GetInlineAsmInfo(
        GeneratorSyntaxContext context,
        System.Threading.CancellationToken ct)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(method, ct);

        if (symbol is null) { return null; }

        // Find [InlineAsm] attributes
        var asmAttributes = symbol.GetAttributes()
            .Where(a => a.AttributeClass?.Name == "InlineAsmAttribute")
            .ToList();

        if (asmAttributes.Count == 0) { return null; }

        // Get containing class info
        var containingType = symbol.ContainingType;
        var ns = symbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : symbol.ContainingNamespace.ToDisplayString();

        return new()
        {
            Namespace = ns,
            ClassName = containingType.Name,
            MethodName = symbol.Name,
            ReturnType = symbol.ReturnType.ToDisplayString(),
            Parameters = [.. symbol.Parameters.Select(p => new ParamInfo(p.Type.ToDisplayString(), p.Name))],
            IsStatic = symbol.IsStatic,
            AsmVariants = [.. asmAttributes.Select(ParseAsmAttribute).Where(v => v.Bytecode.Length > 0)]
        };
    }

    private static AsmVariant ParseAsmAttribute(AttributeData attr)
    {
        byte[]? bytecode = null;

        if (attr.ConstructorArguments.Length > 0)
        {
            var arg = attr.ConstructorArguments[0];
            if (!arg.IsNull && arg.Values.Length > 0)
            {
                bytecode = [.. arg.Values.Select(v => (byte)(v.Value ?? 0))];
            }
        }

        var architecture = attr.ConstructorArguments.Length > 1
            ? (Architecture)(int)(attr.ConstructorArguments[1].Value ?? 1)
            : Architecture.X64;

        var platform = attr.ConstructorArguments.Length > 2
            ? (int)(attr.ConstructorArguments[2].Value ?? 0) switch
            {
                0 => "Any",
                1 => "Windows",
                2 => "Linux",
                3 => "OSX",
                _ => "Any"
            }
            : "Any";

        // Check named arguments
        foreach (var named in attr.NamedArguments)
        {
            switch (named.Key)
            {
                case "Architecture":
                    architecture = (Architecture)(int)(named.Value.Value ?? 1);
                    break;

                case "Platform":
                    platform = (int)(named.Value.Value ?? 0) switch
                    {
                        0 => "Any",
                        1 => "Windows",
                        2 => "Linux",
                        3 => "OSX",
                        _ => platform
                    };
                    break;

                case "Bytecode":
                    if (!named.Value.IsNull && named.Value.Values.Length > 0)
                    {
                        bytecode = [.. named.Value.Values.Select(v => (byte)(v.Value ?? 0))];
                    }
                    break;
            }
        }

        return new()
        {
            Bytecode = bytecode ?? [],
            Architecture = architecture,
            Platform = platform
        };
    }

    private static void GenerateInlineAsm(SourceProductionContext context, InlineAsmInfo info)
    {
        if (info.AsmVariants.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "IASM001",
                    "No valid bytecode",
                    "Method '{0}' has InlineAsm attribute but no valid bytecode was found",
                    "InlineAsm",
                    DiagnosticSeverity.Warning,
                    true),
                Location.None,
                info.MethodName));
            return;
        }

        context.AddSource($"{info.ClassName}.{info.MethodName}.g.cs", GenerateMethod(info));
    }

    private static string GenerateMethod(InlineAsmInfo info)
    {
        var sb = new StringBuilder();

        // File header
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by InlineAsmGenerator");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine($"namespace {info.Namespace};");
            sb.AppendLine();
        }

        // Class declaration
        sb.AppendLine($"partial class {info.ClassName}");
        sb.AppendLine("{");

        // Generate lazy function pointer
        GenerateLazyFunctionPointer(sb, info);

        // Generate method implementation
        GenerateMethodImplementation(sb, info);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static (string comment, string codeBytes) GenerateBytecode(AsmVariant variant)
    {
        var comment = new StringBuilder();
        comment.AppendLine($"                // Bytecode ({variant.Architecture}/{variant.Platform}):");
        if (variant.Bytecode.Length == 0) { comment.AppendLine($"    // WARNING: Empty bytecode"); }

        var codeBytes = variant.Bytecode.Length > 0
                        ? $"new byte[] {{ {(string.Join(", ", variant.Bytecode.Select(b => $"0x{b:X2}")))} }}"
                        : "System.ReadOnlySpan<byte>.Empty";

        return (comment.ToString(), codeBytes);
    }

    private static void GenerateLazyFunctionPointer(StringBuilder sb, InlineAsmInfo info)
    {
        var funcPtrType = BuildFunctionPointerType(info);

        sb.AppendLine($"    private static readonly System.Lazy<nint> {info.MethodName}_LazyPtr = new System.Lazy<nint>(() =>");
        sb.AppendLine("    {");
        sb.AppendLine("        unsafe");
        sb.AppendLine("        {");

        // Single variant: just emit directly
        if (info.AsmVariants.Count == 1)
        {
            var (comment, codeBytes) = GenerateBytecode(info.AsmVariants[0]);
            sb.AppendLine(comment);
            sb.AppendLine($"            var codeBytes = {codeBytes};");
        }
        else
        {
            sb.AppendLine("            System.ReadOnlySpan<byte> codeBytes;");

            bool firstArch = true;
            foreach (var archGroup in info.AsmVariants.GroupBy(v => v.Architecture))
            {
                var archCondition =
                    $"System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.{archGroup.Key}";

                sb.AppendLine($"            {(firstArch ? "if" : "else if")} ({archCondition})");
                sb.AppendLine("            {");

                var byPlatform = archGroup.ToList();
                var anyPlatform = byPlatform.FirstOrDefault(v => v.Platform == "Any");
                var specificPlatforms = byPlatform.Where(v => v.Platform != "Any").ToList();

                if (specificPlatforms.Count > 0)
                {
                    bool firstPlatform = true;
                    foreach (var variant in specificPlatforms)
                    {
                        var platformCondition = variant.Platform switch
                        {
                            "Windows" => "System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)",
                            "Linux" => "System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)",
                            "OSX" => "System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)",
                            _ => null
                        };

                        if (platformCondition == null)
                        {
                            continue;
                        }

                        sb.AppendLine($"                {(firstPlatform ? "if" : "else if")} ({platformCondition})");

                        var (comment, codeBytes) = GenerateBytecode(variant);
                        sb.AppendLine(comment);
                        sb.AppendLine($"                    codeBytes = {codeBytes};");

                        firstPlatform = false;
                    }

                    if (anyPlatform != null)
                    {
                        sb.AppendLine("                else");
                        var (comment, codeBytes) = GenerateBytecode(anyPlatform);
                        sb.AppendLine(comment);
                        sb.AppendLine($"                    codeBytes = {codeBytes};");
                    }
                    else
                    {
                        sb.AppendLine("                else");
                        sb.AppendLine("                    codeBytes = System.ReadOnlySpan<byte>.Empty;");
                    }
                }
                else if (anyPlatform != null)
                {
                    var (comment, codeBytes) = GenerateBytecode(anyPlatform);
                    sb.AppendLine(comment);
                    sb.AppendLine($"                codeBytes = {codeBytes};");
                }
                else
                {
                    sb.AppendLine("                codeBytes = System.ReadOnlySpan<byte>.Empty;");
                }

                sb.AppendLine("            }");
                firstArch = false;
            }

            sb.AppendLine("            else");
            sb.AppendLine("                codeBytes = System.ReadOnlySpan<byte>.Empty;");
        }

        sb.AppendLine();
        sb.AppendLine("            if (codeBytes.Length == 0)");
        sb.AppendLine($"                throw new System.NotSupportedException($\"No compatible bytecode for '{info.MethodName}' on {System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture})\");");
        sb.AppendLine();
        sb.AppendLine("            // Allocate executable memory (cross-platform: VirtualAlloc on Windows, mmap on Unix)");
        sb.AppendLine("            var mem = Corsinvest.Fx.Unsafe.Asm.NativeMemoryHelper.AllocateExecutable((nuint)codeBytes.Length);");
        sb.AppendLine("            codeBytes.CopyTo(new System.Span<byte>((void*)mem, codeBytes.Length));");
        sb.AppendLine("            Corsinvest.Fx.Unsafe.Asm.NativeMemoryHelper.MakeReadExecute(mem, (nuint)codeBytes.Length);");
        sb.AppendLine();
        sb.AppendLine("            return (nint)mem;");
        sb.AppendLine("        }");
        sb.AppendLine("    });");
        sb.AppendLine();
    }

    private static void GenerateCleanupMethod(StringBuilder sb, InlineAsmInfo info)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Frees the allocated executable memory for {info.MethodName}.");
        sb.AppendLine($"    /// Call this during application shutdown if needed.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static unsafe void {info.MethodName}_Cleanup()");
        sb.AppendLine("    {");
        sb.AppendLine($"        if ({info.MethodName}_LazyPtr.IsValueCreated)");
        sb.AppendLine("        {");
        sb.AppendLine($"            Corsinvest.Fx.Unsafe.Asm.NativeMemoryHelper.Free((void*){info.MethodName}_LazyPtr.Value);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateMethodImplementation(StringBuilder sb, InlineAsmInfo info)
    {
        var staticModifier = info.IsStatic 
                                ? "static " 
                                : string.Empty;

        var parameters = string.Join(", ", info.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var paramNames = string.Join(", ", info.Parameters.Select(p => p.Name));
        var funcPtrType = BuildFunctionPointerType(info);

        sb.AppendLine($"    [System.Runtime.CompilerServices.MethodImpl(");
        sb.AppendLine($"        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"    public {staticModifier}unsafe partial {info.ReturnType} {info.MethodName}({parameters})");
        sb.AppendLine("    {");
        sb.AppendLine($"        var ptr = ({funcPtrType}){info.MethodName}_LazyPtr.Value;");
        sb.AppendLine();

        if (info.ReturnType == "void")
        {
            sb.AppendLine($"        ptr({paramNames});");
        }
        else
        {
            sb.AppendLine($"        return ptr({paramNames});");
        }

        sb.AppendLine("    }");
    }

    private static string BuildFunctionPointerType(InlineAsmInfo info)
    {
        var paramTypes = string.Join(", ", info.Parameters.Select(p => MapToUnmanagedType(p.Type)));

        if (info.ReturnType == "void")
        {
            return paramTypes.Length > 0
                ? $"delegate* unmanaged<{paramTypes}, void>"
                : "delegate* unmanaged<void>";
        }
        else
        {
            var returnType = MapToUnmanagedType(info.ReturnType);
            return paramTypes.Length > 0
                ? $"delegate* unmanaged<{paramTypes}, {returnType}>"
                : $"delegate* unmanaged<{returnType}>";
        }
    }

    private static string MapToUnmanagedType(string managedType)
        => managedType switch
        {
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.IntPtr" => "nint",
            "System.UIntPtr" => "nuint",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Boolean" => "bool",
            _ => managedType
        };
}

// Data structures
internal record InlineAsmInfo
{
    public string Namespace { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public string ReturnType { get; init; } = string.Empty;
    public bool IsStatic { get; init; }
    public List<ParamInfo> Parameters { get; init; } = [];
    public List<AsmVariant> AsmVariants { get; init; } = [];
}

internal record AsmVariant
{
    public byte[] Bytecode { get; init; } = [];
    public Architecture Architecture { get; init; } = Architecture.X64;
    public string Platform { get; init; } = "Any";
}

internal record ParamInfo(string Type, string Name);
