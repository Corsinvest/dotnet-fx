using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 05: Pipe Workflow
///
/// Demonstrates universal pipeline composition:
/// - Data transformation pipelines with Pipe
/// - Side effects with Tap
/// - Conditional logic with PipeWhen
/// - Async pipelines with PipeAsync
/// </summary>
public static class PipeWorkflow
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 05: Pipe Workflow ═══\n");

        // Example 1: Simple data transformation pipeline
        Console.WriteLine("1️⃣  Data Transformation Pipeline\n");

        var result1 = 10
            .Pipe(x => x * 2)           // 20
            .Pipe(x => x + 5)           // 25
            .Pipe(x => x.ToString())    // "25"
            .Pipe(s => $"Result: {s}"); // "Result: 25"

        Console.WriteLine($"   {result1}");

        // Example 2: Using Tap for side effects (logging, debugging)
        Console.WriteLine("\n2️⃣  Pipeline with Side Effects (Tap)\n");

        var result2 = 5
            .Tap(x => Console.WriteLine($"   → Start: {x}"))
            .Pipe(x => x * x)
            .Tap(x => Console.WriteLine($"   → After square: {x}"))
            .Pipe(x => x + 10)
            .Tap(x => Console.WriteLine($"   → After add: {x}"))
            .Pipe(x => Math.Sqrt(x))
            .Tap(x => Console.WriteLine($"   → Final: {x:F2}"));

        // Example 3: Processing text data
        Console.WriteLine("\n3️⃣  Text Processing Pipeline\n");

        var text = "  hello world  ";
        var processed = text
            .Pipe(s => s.Trim())
            .Tap(s => Console.WriteLine($"   After trim: '{s}'"))
            .Pipe(s => s.ToUpper())
            .Tap(s => Console.WriteLine($"   After upper: '{s}'"))
            .Pipe(s => s.Replace(" ", "_"))
            .Tap(s => Console.WriteLine($"   After replace: '{s}'"))
            .Pipe(s => $"[{s}]");

        Console.WriteLine($"   Final result: {processed}");

        // Example 4: Complex object transformation
        Console.WriteLine("\n4️⃣  Object Transformation Pipeline\n");

        var rawData = new RawUserData("  alice@EXAMPLE.com  ", "  Alice Smith  ", "25");

        var user = rawData
            .Pipe(data => new
            {
                Email = data.Email.Trim().ToLower(),
                Name = data.Name.Trim(),
                Age = int.Parse(data.Age)
            })
            .Tap(data => Console.WriteLine($"   → Normalized: {data.Email}, {data.Name}, {data.Age}"))
            .Pipe(data => new User(
                Id: Random.Shared.Next(1000, 9999),
                Email: data.Email,
                Name: data.Name,
                Age: data.Age,
                IsActive: true
            ))
            .Tap(u => Console.WriteLine($"   → Created user #{u.Id}"));

        Console.WriteLine($"   Final user: {user}");

        // Example 5: Calculation pipeline with functions
        Console.WriteLine("\n5️⃣  Calculation Pipeline with Named Functions\n");

        var number = 16.0
            .Tap(x => Console.WriteLine($"   Start: {x}"))
            .Pipe(SquareRoot)
            .Tap(x => Console.WriteLine($"   After √: {x}"))
            .Pipe(Cube)
            .Tap(x => Console.WriteLine($"   After x³: {x}"))
            .Pipe(x => Clamp(x, 0, 100))
            .Tap(x => Console.WriteLine($"   After clamp: {x}"));

        Console.WriteLine($"   Final: {number}");

        // Example 6: Pipeline with conditional logic (PipeWhen)
        Console.WriteLine("\n6️⃣  Conditional Pipeline (PipeWhen)\n");

        var testValues = new[] { 5, 15, 25 };

        foreach (var value in testValues)
        {
            var processed1 = value
                .Tap(x => Console.Write($"   {x} → "))
                .Pipe(x => x * 2)
                .Tap(x => Console.Write($"{x} → "))
                .PipeIf(x => x > 20, x => x / 2) // Divide by 2 if > 20
                .Tap(x => Console.Write($"{x} "));

            Console.WriteLine($"(final: {processed1})");
        }
    }

    // Helper functions
    private static double SquareRoot(double x) => Math.Sqrt(x);
    private static double Cube(double x) => x * x * x;
    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));

    // Data models
    private record RawUserData(string Email, string Name, string Age);
    private record User(int Id, string Email, string Name, int Age, bool IsActive);
}
