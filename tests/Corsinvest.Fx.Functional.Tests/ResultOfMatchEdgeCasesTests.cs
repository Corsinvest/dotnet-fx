namespace Corsinvest.Fx.Functional.Tests;

public class ResultOfMatchEdgeCasesTests
{
    // ============================================
    // Match default case (should never happen, but needs coverage)
    // ============================================

    [Fact]
    public void Match_WithReturnValue_CoversAllBranches()
    {
        var okResult = ResultOf.Ok<int, string>(42);
        var errorResult = ResultOf.Fail<int, string>("error");

        // Test Ok branch
        var okValue = okResult.Match(
            ok => ok.Value * 2,
            error => 0
        );
        Assert.Equal(84, okValue);

        // Test Error branch
        var errorValue = errorResult.Match(
            ok => ok.Value,
            error => -1
        );
        Assert.Equal(-1, errorValue);
    }

    [Fact]
    public void Match_VoidVersion_CoversAllBranches()
    {
        var okResult = ResultOf.Ok<int, string>(42);
        var errorResult = ResultOf.Fail<int, string>("error");

        var okExecuted = false;
        var errorExecuted = false;

        // Test Ok branch
        okResult.Match(
            ok => okExecuted = true,
            error => { }
        );
        Assert.True(okExecuted);

        // Test Error branch
        errorResult.Match(
            ok => { },
            error => errorExecuted = true
        );
        Assert.True(errorExecuted);
    }

    // ============================================
    // MatchAsync (generated) - cover both branches
    // ============================================

    [Fact]
    public async Task MatchAsync_Generated_CoversOkBranch()
    {
        var result = ResultOf.Ok<int, string>(42);

        var value = await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                return ok.Value * 2;
            },
            async error =>
            {
                await Task.Delay(1);
                return 0;
            }
        );

        Assert.Equal(84, value);
    }

    [Fact]
    public async Task MatchAsync_Generated_CoversErrorBranch()
    {
        var result = ResultOf.Fail<int, string>("error");

        var value = await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                return ok.Value;
            },
            async error =>
            {
                await Task.Delay(1);
                return -1;
            }
        );

        Assert.Equal(-1, value);
    }

    [Fact]
    public async Task MatchAsync_Generated_VoidVersion_CoversOkBranch()
    {
        var result = ResultOf.Ok<int, string>(42);
        var executed = false;

        await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                executed = true;
            },
            async error =>
            {
                await Task.Delay(1);
            }
        );

        Assert.True(executed);
    }

    [Fact]
    public async Task MatchAsync_Generated_VoidVersion_CoversErrorBranch()
    {
        var result = ResultOf.Fail<int, string>("error");
        var executed = false;

        await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
            },
            async error =>
            {
                await Task.Delay(1);
                executed = true;
            }
        );

        Assert.True(executed);
    }

    // ============================================
    // Match variations with different types
    // ============================================

    [Fact]
    public void Match_WithReferenceTypes_Works()
    {
        var result = ResultOf.Ok<string, Exception>("hello");

        var value = result.Match(
            ok => ok.Value.ToUpper(),
            error => ""
        );

        Assert.Equal("HELLO", value);
    }

    [Fact]
    public void Match_WithNullableTypes_Works()
    {
        var result = ResultOf.Ok<int?, string>(null);

        var value = result.Match(
            ok => ok.Value ?? 0,
            error => -1
        );

        Assert.Equal(0, value);
    }

    [Fact]
    public void Match_WithValueTypes_Works()
    {
        var result = ResultOf.Ok<bool, int>(true);

        var value = result.Match(
            ok => ok.Value,
            error => false
        );

        Assert.True(value);
    }

    [Fact]
    public void Match_WithComplexTypes_Works()
    {
        var user = new User { Id = 1, Name = "Alice" };
        var result = ResultOf.Ok<User, string>(user);

        var name = result.Match(
            ok => ok.Value.Name,
            error => "Unknown"
        );

        Assert.Equal("Alice", name);
    }

    [Fact]
    public async Task MatchAsync_WithComplexTypes_Works()
    {
        var user = new User { Id = 1, Name = "Bob" };
        var result = ResultOf.Ok<User, string>(user);

        var name = await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                return ok.Value.Name;
            },
            async error =>
            {
                await Task.Delay(1);
                return "Unknown";
            }
        );

        Assert.Equal("Bob", name);
    }

    private record User
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
    }
}
