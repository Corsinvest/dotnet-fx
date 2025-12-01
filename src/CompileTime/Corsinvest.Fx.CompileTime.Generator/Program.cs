using System.Text.Json;
using Corsinvest.Fx.CompileTime.Models;

namespace Corsinvest.Fx.CompileTime.Generator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Read request JSON from stdin
            using var reader = new StreamReader(Console.OpenStandardInput());
            var requestJson = await reader.ReadToEndAsync();

            var request = JsonSerializer.Deserialize<CompileTimeRequest>(requestJson)!;
            var engine = new ExecutionEngine();

            var response = new List<CompileTimeResponse>();
            foreach (var item in request.Methods)
            {
                response.Add(await engine.ExecuteAsync(request, item));
            }

            // Write response JSON to stdout
            var responseJson = JsonSerializer.Serialize(response);
            Console.Write(responseJson);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Generator failed: {ex}");

            // Write error response to stdout
            try
            {
                var errorResponse = JsonSerializer.Serialize(new List<CompileTimeResponse>
                {
                    new()
                    {
                        Success = false,
                        ErrorMessage = ex.ToString(),
                        ErrorCode = "GENFAIL",
                        InvocationId = Guid.NewGuid().ToString()
                    }
                });
                Console.Write(errorResponse);
            }
            catch
            {
                // silent fail
            }

            return 1;
        }
    }
}
