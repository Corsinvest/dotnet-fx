namespace Corsinvest.Fx.CompileTime.Models;

public class CompileTimeRequest
{
    public List<string> GlobalReferencePaths { get; set; } = [];
    public List<Item> Methods { get; set; } = [];
    public ProjectType ProjectType { get; set; }

    public class Item
    {
        public required string ClassCode { get; set; }
        public required string Namespace { get; set; }
        public required string TypeName { get; set; }
        public required string MethodName { get; set; }
        public required List<string> MethodParameterTypeNames { get; set; } = [];
        public required object[] Parameters { get; set; }
        public int TimeoutMs { get; set; }
        public required string ReturnTypeFullName { get; set; }
        public required string InvocationId { get; set; }
    }
}
