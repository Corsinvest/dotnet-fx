namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Tests for Recover and RecoverAsync extension methods
/// </summary>
public class ResultOfRecoverTests
{
    // ============================================
    // Recover - Sync Tests
    // ============================================

    [Fact]
    public void Recover_OnOk_ReturnsOriginalValue()
    {
        // Arrange
        var result = ResultOf.Ok<int, string>(42);

        // Act
        var recovered = result.Recover(error => -1);

        // Assert
        Assert.Equal(42, recovered);
    }

    [Fact]
    public void Recover_OnFail_ReturnsRecoveredValue()
    {
        // Arrange
        var result = ResultOf.Fail<int, string>("error");

        // Act
        var recovered = result.Recover(error => -1);

        // Assert
        Assert.Equal(-1, recovered);
    }

    [Fact]
    public void Recover_OnFail_PassesErrorToRecoveryFunction()
    {
        // Arrange
        var result = ResultOf.Fail<int, string>("not found");
        string? capturedError = null;

        // Act
        result.Recover(error =>
        {
            capturedError = error;
            return 0;
        });

        // Assert
        Assert.Equal("not found", capturedError);
    }

    [Fact]
    public void Recover_WithErrorBasedLogic_AppliesCorrectRecovery()
    {
        // Arrange
        ResultOf<int, DbError> notFoundResult = ResultOf.Fail<int, DbError>(DbError.NotFound);
        ResultOf<int, DbError> connectionLostResult = ResultOf.Fail<int, DbError>(DbError.ConnectionLost);

        // Act
        var notFoundRecovered = notFoundResult.Recover(error => error switch
        {
            DbError.NotFound => -1,
            DbError.ConnectionLost => -2,
            _ => -999
        });

        var connectionLostRecovered = connectionLostResult.Recover(error => error switch
        {
            DbError.NotFound => -1,
            DbError.ConnectionLost => -2,
            _ => -999
        });

        // Assert
        Assert.Equal(-1, notFoundRecovered);
        Assert.Equal(-2, connectionLostRecovered);
    }

    [Fact]
    public void Recover_CanThrowInRecoveryFunction()
    {
        // Arrange
        var result = ResultOf.Fail<int, string>("fatal error");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            result.Recover(error => throw new InvalidOperationException($"Cannot recover from: {error}"))
        );

        Assert.Contains("Cannot recover from: fatal error", exception.Message);
    }

    [Fact]
    public void Recover_WithComplexObject_ReturnsDefaultObject()
    {
        // Arrange
        var result = ResultOf.Fail<User, string>("User not found");
        var defaultUser = new User(0, "Anonymous");

        // Act
        var recovered = result.Recover(error => defaultUser);

        // Assert
        Assert.Equal(0, recovered.Id);
        Assert.Equal("Anonymous", recovered.Name);
    }

    [Fact]
    public void Recover_ChainedRecovery_FallsBackThroughLevels()
    {
        // Arrange - simulate trying cache, then DB, then default
        ResultOf<string, string> cacheResult = ResultOf.Fail<string>("Cache miss");
        ResultOf<string, string> dbResult = ResultOf.Fail<string>("DB connection lost");

        // Act
        var value = cacheResult.Recover(cacheError =>
            dbResult.Recover(dbError => "Default value")
        );

        // Assert
        Assert.Equal("Default value", value);
    }

    // ============================================
    // RecoverAsync - Async Tests
    // ============================================

    [Fact]
    public async Task RecoverAsync_OnOk_ReturnsOriginalValue()
    {
        // Arrange
        var result = ResultOf.Ok<int, string>(42);

        // Act
        var recovered = await result.RecoverAsync(async error =>
        {
            await Task.Delay(1);
            return -1;
        });

        // Assert
        Assert.Equal(42, recovered);
    }

    [Fact]
    public async Task RecoverAsync_OnFail_ReturnsRecoveredValue()
    {
        // Arrange
        var result = ResultOf.Fail<int, string>("error");

        // Act
        var recovered = await result.RecoverAsync(async error =>
        {
            await Task.Delay(1);
            return -1;
        });

        // Assert
        Assert.Equal(-1, recovered);
    }

    [Fact]
    public async Task RecoverAsync_OnFail_PassesErrorToRecoveryFunction()
    {
        // Arrange
        var result = ResultOf.Fail<int, string>("database error");
        string? capturedError = null;

        // Act
        await result.RecoverAsync(async error =>
        {
            capturedError = error;
            await Task.Delay(1);
            return 0;
        });

        // Assert
        Assert.Equal("database error", capturedError);
    }

    [Fact]
    public async Task RecoverAsync_WithAsyncDataAccess_FetchesFallbackData()
    {
        // Arrange
        var result = ResultOf.Fail<User, string>("Primary DB down");

        // Act
        var recovered = await result.RecoverAsync(async error =>
        {
            await Task.Delay(1); // Simulate async DB call
            return new User(999, "Fallback User");
        });

        // Assert
        Assert.Equal(999, recovered.Id);
        Assert.Equal("Fallback User", recovered.Name);
    }

    // ============================================
    // RecoverAsync - Task<ResultOf> Overloads
    // ============================================

    [Fact]
    public async Task RecoverAsync_TaskResultOf_WithAsyncRecovery_Works()
    {
        // Arrange
        Task<ResultOf<int, string>> resultTask = Task.FromResult(
            ResultOf.Fail<int, string>("error")
        );

        // Act
        var recovered = await resultTask.RecoverAsync(async error =>
        {
            await Task.Delay(1);
            return -1;
        });

        // Assert
        Assert.Equal(-1, recovered);
    }

    [Fact]
    public async Task Recover_TaskResultOf_WithSyncRecovery_Works()
    {
        // Arrange
        Task<ResultOf<int, string>> resultTask = Task.FromResult(
            ResultOf.Fail<int, string>("error")
        );

        // Act
        var recovered = await resultTask.Recover(error => -1);

        // Assert
        Assert.Equal(-1, recovered);
    }

    [Fact]
    public async Task RecoverAsync_ChainedWithOtherOperations_Works()
    {
        // Arrange
        async Task<ResultOf<int, string>> FetchDataAsync()
        {
            await Task.Delay(1);
            return ResultOf.Fail<int, string>("Network error");
        }

        // Act
        var value = await FetchDataAsync()
            .RecoverAsync(async error =>
            {
                await Task.Delay(1); // Fallback async operation
                return 999;
            });

        // Assert
        Assert.Equal(999, value);
    }

    // ============================================
    // Real-World Scenarios
    // ============================================

    [Fact]
    public void Recover_CacheFallback_FetchesFromDatabaseOnCacheMiss()
    {
        // Arrange
        ResultOf<User, string> cacheResult = ResultOf.Fail<User>("Cache miss");
        var dbUser = new User(1, "John Doe");

        // Act
        var user = cacheResult.Recover(cacheError => dbUser);

        // Assert
        Assert.Equal("John Doe", user.Name);
    }

    [Fact]
    public void Recover_WithLogging_LogsErrorAndReturnsDefault()
    {
        // Arrange
        var result = ResultOf.Fail<int, string>("Computation failed");
        var loggedError = string.Empty;

        // Act
        var value = result.Recover(error =>
        {
            loggedError = $"Error logged: {error}";
            return 0;
        });

        // Assert
        Assert.Equal(0, value);
        Assert.Equal("Error logged: Computation failed", loggedError);
    }

    [Fact]
    public async Task RecoverAsync_MultiLayerFallback_TriesMultipleSources()
    {
        // Arrange
        async Task<ResultOf<string, string>> GetFromCacheAsync()
        {
            await Task.Delay(1);
            return ResultOf.Fail<string>("Cache miss");
        }

        async Task<ResultOf<string, string>> GetFromDatabaseAsync()
        {
            await Task.Delay(1);
            return ResultOf.Fail<string>("DB connection lost");
        }

        async Task<string> GetFromBackupAsync()
        {
            await Task.Delay(1);
            return "Backup data";
        }

        // Act
        var data = await GetFromCacheAsync()
            .RecoverAsync(async cacheError =>
                await GetFromDatabaseAsync()
                    .RecoverAsync(async dbError =>
                        await GetFromBackupAsync()
                    )
            );

        // Assert
        Assert.Equal("Backup data", data);
    }

    [Fact]
    public void Recover_ErrorBasedStrategy_ChoosesDifferentDefaults()
    {
        // Arrange
        var notFoundError = ResultOf.Fail<User, DbError>(DbError.NotFound);
        var permissionDeniedError = ResultOf.Fail<User, DbError>(DbError.PermissionDenied);

        // Act
        var anonymousUser = notFoundError.Recover(error => error switch
        {
            DbError.NotFound => new User(0, "Anonymous"),
            DbError.PermissionDenied => new User(-1, "Guest"),
            _ => new User(-999, "Error")
        });

        var guestUser = permissionDeniedError.Recover(error => error switch
        {
            DbError.NotFound => new User(0, "Anonymous"),
            DbError.PermissionDenied => new User(-1, "Guest"),
            _ => new User(-999, "Error")
        });

        // Assert
        Assert.Equal("Anonymous", anonymousUser.Name);
        Assert.Equal("Guest", guestUser.Name);
    }

    // ============================================
    // Helper Types
    // ============================================

    private record User(int Id, string Name);

    private enum DbError
    {
        NotFound,
        ConnectionLost,
        PermissionDenied
    }
}
