namespace Corsinvest.Fx.CompileTime;

/// <summary>
/// Attribute used to annotate generated code with diagnostic information.
/// This allows the analyzer to read diagnostic data directly from the generated code.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
public class CompileTimeDiagnosticAttribute : System.Attribute
{
    /// <summary>
    /// Creates a new instance of the attribute.
    /// </summary>
    /// <param name="id">The diagnostic ID (e.g., "COMPTIME101")</param>
    /// <param name="filePath">The file path where the diagnostic should be reported</param>
    /// <param name="startPosition">The starting position of the text span</param>
    /// <param name="length">The length of the text span</param>
    /// <param name="messageArgs">The arguments for the message format string</param>
    public CompileTimeDiagnosticAttribute(string id, string filePath, int startPosition, int length, object[] messageArgs)
    {
        Id = id;
        FilePath = filePath;
        StartPosition = startPosition;
        Length = length;
        MessageArgs = messageArgs;
    }

    /// <summary>
    /// The diagnostic ID (e.g., "COMPTIME101")
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The file path where the diagnostic should be reported
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// The starting position of the text span
    /// </summary>
    public int StartPosition { get; }

    /// <summary>
    /// The length of the text span
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// The arguments for the message format string
    /// </summary>
    public object[] MessageArgs { get; }
}