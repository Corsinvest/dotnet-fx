namespace Corsinvest.Fx.Unsafe.Asm;

/// <summary>
/// Specifies the operating system platform for inline assembly code.
/// </summary>
public enum Platform
{
    /// <summary>
    /// Code works on any platform with the specified architecture.
    /// </summary>
    Any,

    /// <summary>
    /// Code is specific to Windows.
    /// </summary>
    Windows,

    /// <summary>
    /// Code is specific to Linux.
    /// </summary>
    Linux,

    /// <summary>
    /// Code is specific to macOS (OSX).
    /// </summary>
    OSX
}
