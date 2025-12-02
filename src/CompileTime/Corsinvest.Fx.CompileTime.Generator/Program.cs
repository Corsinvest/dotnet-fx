using System.Text.Json;
using Corsinvest.Fx.CompileTime.Diagnostics;
using Corsinvest.Fx.CompileTime.Models;

namespace Corsinvest.Fx.CompileTime.Generator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = new JsonSerializerOptions() { WriteIndented = true };
        var responseFile = args[0].Replace("_request.json", "_response.json");

        try
        {
            var request = JsonSerializer.Deserialize<CompileTimeRequest>(await File.ReadAllTextAsync(args[0]))!;
            var engine = new ExecutionEngine();

            var response = new List<CompileTimeResponse>();
            foreach (var item in request.Methods)
            {
                response.Add(await engine.ExecuteAsync(request, item));
            }

            await File.WriteAllTextAsync(responseFile, JsonSerializer.Serialize(response, options));
            return 0;
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(responseFile, JsonSerializer.Serialize(new List<CompileTimeResponse>
            {
                new()
                {
                    Success = false,
                    ErrorMessage = ex.ToString(),
                    ErrorCode = DiagnosticDescriptors.SourceGenerationError.Id,
                    InvocationId = Guid.NewGuid().ToString()
                }
            }, options));
            return 1;
        }
    }
}
