using static Corsinvest.Fx.Defer.Defer;

namespace Corsinvest.Fx.Defer.Tests;

public class DeferTests
{
    [Fact]
    public void Defer_SyncAction_ExecutesOnDispose()
    {
        // Arrange
        bool executed = false;

        // Act
        {
            using var _ = defer(() => executed = true);
            Assert.False(executed); // Not executed yet
        }

        // Assert
        Assert.True(executed); // Executed after dispose
    }

    [Fact]
    public async Task Defer_AsyncAction_ExecutesOnDisposeAsync()
    {
        // Arrange
        bool executed = false;

        // Act
        {
            await using var _ = defer(async () =>
            {
                await Task.Delay(10);
                executed = true;
            });
            Assert.False(executed); // Not executed yet
        }

        // Assert
        Assert.True(executed); // Executed after dispose
    }

    [Fact]
    public void Defer_MultipleDefers_ExecuteInLIFOOrder()
    {
        // Arrange
        var executionOrder = new List<int>();

        // Act
        {
            using var _1 = defer(() => executionOrder.Add(1));
            using var _2 = defer(() => executionOrder.Add(2));
            using var _3 = defer(() => executionOrder.Add(3));
        }

        // Assert - LIFO order (Last In, First Out)
        Assert.Equal(new[] { 3, 2, 1 }, executionOrder);
    }

    [Fact]
    public void Defer_WithException_StillExecutes()
    {
        // Arrange
        bool cleanupExecuted = false;

        // Act & Assert
        try
        {
            using var _ = defer(() => cleanupExecuted = true);
            throw new InvalidOperationException("Test exception");
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }

        Assert.True(cleanupExecuted); // Cleanup executed even with exception
    }

    [Fact]
    public void Defer_ExceptionInAction_Suppressed()
    {
        // Arrange
        bool secondCleanupExecuted = false;

        // Act - No exception should escape
        {
            Action throwAction = () => throw new InvalidOperationException("First cleanup throws");
            Action okAction = () => secondCleanupExecuted = true;

            using var _1 = defer(throwAction);
            using var _2 = defer(okAction);
        } // Dispose happens here (LIFO: _2 then _1)

        // Assert
        Assert.True(secondCleanupExecuted); // Second cleanup still executed
    }

    [Fact]
    public async Task Defer_AsyncWithAwaitUsing_DoesNotBlock()
    {
        // Arrange
        bool executed = false;

        // Act - Using await using ensures non-blocking async execution
        await Task.Run(async () =>
        {
            await using var _ = defer(async () =>
            {
                await Task.Delay(10);
                executed = true;
            });
        });

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public void Defer_NullAction_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        {
            Action? nullAction = null;
            using var _ = defer(nullAction!);
        }
    }

    [Fact]
    public async Task Defer_NullAsyncAction_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        {
            Func<Task>? nullAsyncAction = null;
            await using var _ = defer(nullAsyncAction!);
        }
    }

    [Fact]
    public void Defer_ComplexScenario_DatabaseTransaction()
    {
        // Arrange
        var operations = new List<string>();
        var mockConnection = new MockDbConnection(operations);
        var mockTransaction = new MockDbTransaction(operations);

        // Act
        {
            mockConnection.Open();
            using var _Connection = defer(mockConnection.Close);

            mockTransaction.Begin();
            using var _Transaction = defer(mockTransaction.Rollback);

            operations.Add("BusinessLogic");

            // Simulate successful completion
            mockTransaction.Commit();
        }

        // Assert
        var expected = new[]
        {
            "ConnectionOpened",
            "TransactionBegan",
            "BusinessLogic",
            "TransactionCommitted",
            "TransactionRolledback", // Still called, but no-op after commit
            "ConnectionClosed"
        };
        Assert.Equal(expected, operations);
    }
}

// Mock classes for testing
internal class MockDbConnection(List<string> operations)
{
    public void Open() => operations.Add("ConnectionOpened");
    public void Close() => operations.Add("ConnectionClosed");
}

internal class MockDbTransaction(List<string> operations)
{
    public void Begin() => operations.Add("TransactionBegan");
    public void Commit() => operations.Add("TransactionCommitted");
    public void Rollback() => operations.Add("TransactionRolledback");// In real scenario, rollback after commit would be no-op
}
