using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 08: ResultOf Recovery Strategies
///
/// Demonstrates error recovery patterns:
/// - Recover from errors with fallback values
/// - RecoverWith for alternative operations
/// - Retry logic with ResultOf
/// - Graceful degradation strategies
/// </summary>
public static class ResultOfRecover
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 08: ResultOf Recovery Strategies ═══\n");

        // Example 1: Simple Recover - fallback value
        Console.WriteLine("1️⃣  Simple Recover - Fallback Value\n");

        var successResult = ResultOf.Ok<int, string>(42);
        var errorResult = ResultOf.Fail<int, string>("Network error");

        var recovered1 = successResult.GetValueOr(0);
        var recovered2 = errorResult.GetValueOr(0);

        Console.WriteLine($"   Success result recovered: {recovered1}");
        Console.WriteLine($"   Error result recovered: {recovered2} (fallback value)");

        // Example 2: Recover with function - compute fallback from error
        Console.WriteLine("\n2️⃣  Recover with Function - Error-based Fallback\n");

        var apiErrors = new[]
        {
            ResultOf.Ok<UserData, ApiError>(new UserData(1, "Alice")),
            ResultOf.Fail<UserData, ApiError>(ApiError.NotFound),
            ResultOf.Fail<UserData, ApiError>(ApiError.Unauthorized)
        };

        foreach (var result in apiErrors)
        {
            var userData = result.GetValueOr(error => error switch
            {
                ApiError.NotFound => new UserData(0, "Guest"),
                ApiError.Unauthorized => new UserData(-1, "Anonymous"),
                _ => new UserData(0, "Unknown")
            });

            Console.WriteLine($"   Result: {FormatResult(result)} → Recovered: {userData.Name}");
        }

        // Example 3: RecoverWith - alternative operation
        Console.WriteLine("\n3️⃣  RecoverWith - Alternative Operation\n");

        var primaryApiResult = CallPrimaryApi();
        var finalResult = RecoverWithBackupApi(primaryApiResult);

        Console.WriteLine($"   Primary API: {FormatResult(primaryApiResult)}");
        Console.WriteLine($"   Final result: {FormatResult(finalResult)}");

        // Example 4: Retry strategy
        Console.WriteLine("\n4️⃣  Retry Strategy\n");

        var maxRetries = 3;
        var retryResult = RetryOperation(() => UnreliableOperation(), maxRetries);

        Console.WriteLine($"   Final result after {maxRetries} retries: {FormatResult(retryResult)}");

        // Example 5: Graceful degradation - cache fallback
        Console.WriteLine("\n5️⃣  Graceful Degradation - Cache Fallback\n");

        var freshData = FetchFreshData(userId: 1);
        var dataWithFallback = freshData.Match(
            ok => ResultOf.Ok<UserData, DataError>(ok.Value),
            fail =>
            {
                Console.WriteLine($"   → Fresh data failed: {fail.ErrorValue}, trying cache...");
                return GetFromCache(userId: 1);
            }
        );

        Console.WriteLine($"   Final data: {FormatResult(dataWithFallback)}");

        // Example 6: Error transformation and recovery
        Console.WriteLine("\n6️⃣  Error Transformation and Recovery\n");

        var dbResult = DatabaseOperation();
        var transformedResult = dbResult
            .MapError(MapDatabaseErrorToUserError)
            .GetValueOr(error => $"Operation failed: {error}");

        Console.WriteLine($"   Database operation: {FormatResult(dbResult)}");
        Console.WriteLine($"   Transformed and recovered: {transformedResult}");

        // Example 7: Complex recovery chain
        Console.WriteLine("\n7️⃣  Complex Recovery Chain\n");

        var complexResult = ComplexOperationWithRecovery();
        Console.WriteLine($"   Complex operation result: {FormatResult(complexResult)}");
    }

    // Example: Primary API call (fails)
    private static ResultOf<string, ApiError> CallPrimaryApi()
    {
        Console.WriteLine("   → Calling primary API...");
        return ResultOf.Fail<string, ApiError>(ApiError.Timeout);
    }

    // Example: Backup API call (succeeds)
    private static ResultOf<string, ApiError> CallBackupApi()
    {
        Console.WriteLine("   → Primary failed, calling backup API...");
        return ResultOf.Ok<string, ApiError>("Data from backup API");
    }

    // Recovery strategy: fallback to backup API
    private static ResultOf<string, ApiError> RecoverWithBackupApi(ResultOf<string, ApiError> primaryResult)
        => primaryResult.Match(
            ok => primaryResult,
            fail => CallBackupApi()
        );

    // Retry strategy
    private static ResultOf<string, string> RetryOperation(
        Func<ResultOf<string, string>> operation,
        int maxRetries)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            Console.WriteLine($"   → Attempt {attempt}/{maxRetries}...");
            var result = operation();

            if (result.IsOk)
            {
                Console.WriteLine($"   → Success on attempt {attempt}!");
                return result;
            }

            Console.WriteLine($"   → Failed: {result.GetValueOr(_ => "unknown error")}");

            if (attempt < maxRetries)
            {
                Console.WriteLine("   → Retrying...");
            }
        }

        return ResultOf.Fail<string, string>("Max retries exceeded");
    }

    // Unreliable operation (succeeds on 3rd attempt)
    private static int _attemptCount = 0;
    private static ResultOf<string, string> UnreliableOperation()
    {
        _attemptCount++;
        return _attemptCount >= 3
            ? ResultOf.Ok<string, string>("Success!")
            : ResultOf.Fail<string, string>("Temporary failure");
    }

    // Fetch fresh data (simulated failure)
    private static ResultOf<UserData, DataError> FetchFreshData(int userId)
    {
        Console.WriteLine("   → Fetching fresh data...");
        return ResultOf.Fail<UserData, DataError>(DataError.NetworkError);
    }

    // Get from cache (fallback)
    private static ResultOf<UserData, DataError> GetFromCache(int userId)
    {
        Console.WriteLine("   → Loading from cache...");
        return ResultOf.Ok<UserData, DataError>(new UserData(userId, "Alice (cached)"));
    }

    // Database operation (simulated error)
    private static ResultOf<string, DatabaseError> DatabaseOperation()
        => ResultOf.Fail<string, DatabaseError>(DatabaseError.ConnectionLost);

    // Map database error to user-friendly error
    private static UserError MapDatabaseErrorToUserError(DatabaseError dbError)
        => dbError switch
        {
            DatabaseError.ConnectionLost => UserError.ServiceUnavailable,
            DatabaseError.Timeout => UserError.ServiceUnavailable,
            DatabaseError.NotFound => UserError.NotFound,
            _ => UserError.InternalError
        };

    // Complex operation with multiple recovery strategies
    private static ResultOf<string, string> ComplexOperationWithRecovery()
    {
        Console.WriteLine("   → Step 1: Trying primary service...");
        var step1 = ResultOf.Fail<string, string>("Primary service down");

        if (step1.IsFail)
        {
            Console.WriteLine("   → Step 2: Trying secondary service...");
            var step2 = ResultOf.Fail<string, string>("Secondary service down");

            if (step2.IsFail)
            {
                Console.WriteLine("   → Step 3: Using cached data...");
                return ResultOf.Ok<string, string>("Cached data (stale)");
            }

            return step2;
        }

        return step1;
    }

    // Helper: Format result for display
    private static string FormatResult<T, E>(ResultOf<T, E> result)
        => result.Match(
            ok => $"Ok({ok.Value})",
            fail => $"Fail({fail.ErrorValue})"
        );

    // Domain models and errors
    private record UserData(int Id, string Name);

    private enum ApiError { NotFound, Unauthorized, Timeout, ServerError }
    private enum DataError { NetworkError, CacheExpired }
    private enum DatabaseError { ConnectionLost, Timeout, NotFound }
    private enum UserError { NotFound, ServiceUnavailable, InternalError }
}
