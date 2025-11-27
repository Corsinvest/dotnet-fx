using System.Runtime.InteropServices;

namespace Corsinvest.Fx.Unsafe.Asm;

/// <summary>
/// Marks a partial method to be implemented with inline assembly bytecode.
/// The method must be declared as 'partial' and will be implemented by the source generator.
/// </summary>
/// <example>
/// <code>
/// [InlineAsm([0x48, 0x89, 0xC8, 0x48, 0x01, 0xD0, 0xC3], Architecture.X64, Platform.Any)]
/// public static partial long Add(long a, long b);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the InlineAsmAttribute with the specified bytecode.
/// </remarks>
/// <param name="bytecode">The machine code bytes to execute.</param>
/// <param name="architecture">The target CPU architecture (default: X64).</param>
/// <param name="platform">The target platform (default: Any).</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class InlineAsmAttribute(byte[] bytecode, Architecture architecture = Architecture.X64, Platform platform = Platform.Any) : Attribute
{
    /// <summary>
    /// Gets the machine code bytecode to execute.
    /// </summary>
    public byte[] Bytecode { get; } = bytecode ?? throw new ArgumentNullException(nameof(bytecode));

    /// <summary>
    /// Gets the target CPU architecture.
    /// </summary>
    public Architecture Architecture { get; } = architecture;

    /// <summary>
    /// Gets the target operating system platform.
    /// </summary>
    public Platform Platform { get; } = platform;
}
