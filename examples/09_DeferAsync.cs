using static Corsinvest.Fx.Defer.Defer;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 09: Defer Async - Go-style Resource Cleanup
///
/// Demonstrates defer pattern for automatic resource cleanup:
/// - Async cleanup with await using
/// - Database connections
/// - File streams
/// - HTTP clients
/// - Transaction rollback
/// </summary>
public static class DeferAsync
{
    public static async Task Run()
    {
        Console.WriteLine("\n═══ Example 09: Defer Async (Resource Cleanup) ═══\n");

        // Example 1: Database connection cleanup
        await Example1_DatabaseConnection();

        // Example 2: File operations
        await Example2_FileOperations();

        // Example 3: HTTP client cleanup
        await Example3_HttpClient();

        // Example 4: Transaction with rollback
        await Example4_TransactionRollback();

        // Example 5: Multiple resource cleanup (LIFO order)
        await Example5_MultipleResources();
    }

    // Example 1: Database connection with automatic close
    private static async Task Example1_DatabaseConnection()
    {
        Console.WriteLine("1️⃣  Database Connection Cleanup\n");

        await using var _ = defer(async () =>
        {
            Console.WriteLine("   → Connection closed");
            await Task.Delay(5);
        });

        Console.WriteLine("   → Opening database connection...");
        await Task.Delay(10);
        Console.WriteLine("   → Executing query...");
        await Task.Delay(10);
        Console.WriteLine("   → Query complete");
        // Connection will be closed automatically when scope exits

        Console.WriteLine();
    }

    // Example 2: File operations with cleanup
    private static async Task Example2_FileOperations()
    {
        Console.WriteLine("2️⃣  File Operations Cleanup\n");

        await using var _ = defer(async () =>
        {
            Console.WriteLine("   → File handle released");
            await Task.Delay(5);
        });

        Console.WriteLine("   → Opening file...");
        await Task.Delay(10);
        Console.WriteLine("   → Writing data...");
        await Task.Delay(10);
        Console.WriteLine("   → Flushing buffer...");
        await Task.Delay(10);
        // File will be closed automatically

        Console.WriteLine();
    }

    // Example 3: HTTP client cleanup
    private static async Task Example3_HttpClient()
    {
        Console.WriteLine("3️⃣  HTTP Client Cleanup\n");

        await using var _ = defer(async () =>
        {
            Console.WriteLine("   → HTTP client disposed");
            await Task.Delay(5);
        });

        Console.WriteLine("   → Creating HTTP client...");
        await Task.Delay(10);
        Console.WriteLine("   → Sending request...");
        await Task.Delay(10);
        Console.WriteLine("   → Response received");
        // HTTP client will be disposed automatically

        Console.WriteLine();
    }

    // Example 4: Transaction with automatic rollback on error
    private static async Task Example4_TransactionRollback()
    {
        Console.WriteLine("4️⃣  Transaction with Rollback\n");

        var committed = false;

        try
        {
            await using var _ = defer(async () =>
            {
                if (!committed)
                {
                    Console.WriteLine("   → Transaction rolled back (cleanup)");
                    await Task.Delay(5);
                }
                else
                {
                    Console.WriteLine("   → Transaction committed successfully");
                }
            });

            Console.WriteLine("   → Starting transaction...");
            await Task.Delay(10);

            Console.WriteLine("   → Insert operation 1...");
            await Task.Delay(10);

            Console.WriteLine("   → Insert operation 2...");
            await Task.Delay(10);

            // Simulate error (comment out to see successful commit)
            // throw new InvalidOperationException("Database error!");

            Console.WriteLine("   → Committing transaction...");
            committed = true;
            await Task.Delay(10);

            // Defer will run after this point
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Error: {ex.Message}");
            // Defer will still run and rollback
        }

        Console.WriteLine();
    }

    // Example 5: Multiple resources (LIFO cleanup order)
    private static async Task Example5_MultipleResources()
    {
        Console.WriteLine("5️⃣  Multiple Resources (LIFO Order)\n");

        Console.WriteLine("   → Acquiring Resource A...");
        await using var _resourceA = defer(async () =>
        {
            Console.WriteLine("   → Released Resource A (last acquired, first released)");
            await Task.Delay(5);
        });

        await Task.Delay(10);

        Console.WriteLine("   → Acquiring Resource B...");
        await using var _resourceB = defer(async () =>
        {
            Console.WriteLine("   → Released Resource B");
            await Task.Delay(5);
        });

        await Task.Delay(10);

        Console.WriteLine("   → Acquiring Resource C...");
        await using var _resourceC = defer(async () =>
        {
            Console.WriteLine("   → Released Resource C (first to be released)");
            await Task.Delay(5);
        });

        Console.WriteLine("   → Using all resources...");
        await Task.Delay(10);

        Console.WriteLine("   → Done with resources");
        // Resources will be released in reverse order: C, B, A

        Console.WriteLine();
    }

    // Note: defer() is globally available via Defer.defer() static method
}
