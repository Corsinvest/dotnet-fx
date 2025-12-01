namespace Corsinvest.Fx.CompileTime.Models;

public class CompileTimeResponse
{
    public bool Success { get; set; }
    public string SerializedValue { get; set; } = default!;
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public long ExecutionTimeMs { get; set; }
    public long MemoryFootprintBytes { get; set; }
    public required string InvocationId { get; set; }
}
