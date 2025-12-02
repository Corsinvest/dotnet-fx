using Corsinvest.Fx.CompileTime;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// <para>Example 10: CompileTime Basics</para>
/// <para>
/// Demonstrates compile-time execution of methods:
/// - Simple calculations (Fibonacci, Factorial, Primes)
/// - Async methods
/// - Cache strategies
/// - Performance monitoring
/// - Timeout handling
/// </para>
/// </summary>
public static class CompileTimeBasics
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 10: CompileTime Basics ═══\n");

        Console.WriteLine("1️⃣  Basic Calculations");
        Console.WriteLine($"  Fibonacci(10) = {CalculateFibonacci()}");
        Console.WriteLine($"  Factorial(15) = {CalculateFactorial()}");
        Console.WriteLine($"  Pi ≈ {CalculatePi()}");

        Console.WriteLine("\n2️⃣  String Operations");
        var message = BuildWelcomeMessage();
        Console.WriteLine($"  Welcome message:\n{message}");

        Console.WriteLine("\n3️⃣  Array Generation");
        var primes = GeneratePrimes();
        Console.WriteLine($"  Primes up to 100: [{string.Join(", ", primes)}]");

        Console.WriteLine("\n4️⃣  Cache Strategy");
        Console.WriteLine($"  Persistent cached value: {TestPersistentCache()}");

        Console.WriteLine("\n5️⃣  Build Information");
        LogBuildInfo();
    }

    public static async Task RunAsync()
    {
        Console.WriteLine("\n6️⃣  Async Methods");
        await InitializeAsync();
        var asyncInfo = await GetAsyncBuildInfo();
        Console.WriteLine($"  Async build info: {asyncInfo}");
    }

    [CompileTime(PerformanceWarningThresholdMs = 500)] // Fibonacci is intentionally slow
    public static int CalculateFibonacci() => Fibonacci(10);

    [CompileTime]
    public static double CalculatePi()
    {
        double pi = 0;
        for (int i = 0; i < 1000; i++)
        {
            pi += Math.Pow(-1, i) / ((2 * i) + 1);
        }
        return pi * 4;
    }

    [CompileTime]
    public static string BuildWelcomeMessage()
    {
        const string appName = "Corsinvest.Fx";
        const string version = "1.0.0";
        var buildTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return $"""
        Welcome to {appName}
                v{version}
                (built at {buildTime})
        """;
    }

    [CompileTime]
    public static int[] GeneratePrimes()
    {
        var primes = new List<int>();
        for (int i = 2; i <= 100; i++)
        {
            if (IsPrime(i))
            {
                primes.Add(i);
            }
        }
        return [.. primes];
    }

    [CompileTime]
    public static long CalculateFactorial()
    {
        long result = 1;
        for (int i = 2; i <= 15; i++)
        {
            result *= i;
        }
        return result;
    }

    [CompileTime(Cache = CacheStrategy.Persistent)]
    public static int TestPersistentCache() => 42 * 10;

    [CompileTime]
    public static void LogBuildInfo()
    {
        var timestamp = DateTime.Now;
        var buildId = Guid.NewGuid().ToString("N")[..8];
        Console.WriteLine($"  Build timestamp: {timestamp:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  Build ID: {buildId}");
    }

    [CompileTime]
    public static async Task InitializeAsync()
    {
        await Task.Delay(10);
        Console.WriteLine("  Async initialization completed");
    }

    [CompileTime]
    public static async Task<string> GetAsyncBuildInfo()
    {
        await Task.Delay(10);
        var buildTime = DateTime.Now;
        var version = "1.0.0";
        return $"v{version} - {buildTime:HH:mm:ss}";
    }

    private static int Fibonacci(int n) => n <= 1 ? n : Fibonacci(n - 1) + Fibonacci(n - 2);

    private static bool IsPrime(int n)
    {
        if (n < 2) { return false; }
        for (int i = 2; i * i <= n; i++)
        {
            if (n % i == 0) { return false; }
        }
        return true;
    }
}
