namespace Corsinvest.Fx.Functional.Tests;

public class TryExtensionsTests
{
    // ============================================
    // Synchronous Try - Basic
    // ============================================

    [Fact]
    public void Try_WithSuccessfulOperation_ReturnsOk()
    {
        var result = "42".Try(int.Parse);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public void Try_WithFailingOperation_ReturnsError()
    {
        var result = "invalid".Try(int.Parse);

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("Should not be Ok"),
            error => Assert.IsType<FormatException>(error.ErrorValue)
        );
    }

    [Fact]
    public void Try_PreservesExceptionMessage()
    {
        var result = "abc".Try(int.Parse);

        result.Match(
            ok => Assert.Fail("Should not be Ok"),
            error => Assert.Contains("not in a correct format", error.ErrorValue.Message, StringComparison.OrdinalIgnoreCase)
        );
    }

    // ============================================
    // Try with Custom Error Mapper
    // ============================================

    [Fact]
    public void Try_WithErrorMapper_MapsExceptionToCustomType()
    {
        var result = "invalid".Try(
            int.Parse,
            ex => $"Parse failed: {ex.Message}"
        );

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("Should not be Ok"),
            error => Assert.Contains("Parse failed", error.ErrorValue)
        );
    }

    [Fact]
    public void Try_WithErrorMapper_EnumError_Works()
    {
        var result = "999999999999999999999999".Try(
            int.Parse,
            ex => ex is OverflowException ? ParseError.Overflow : ParseError.InvalidFormat
        );

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("Should not be Ok"),
            error => Assert.Equal(ParseError.Overflow, error.ErrorValue)
        );
    }

    [Fact]
    public void Try_WithErrorMapper_Success_DoesNotInvokeMapper()
    {
        bool mapperInvoked = false;

        var result = "42".Try(
            int.Parse,
            ex =>
            {
                mapperInvoked = true;
                return "Should not be called";
            }
        );

        Assert.True(result.IsSuccess);
        Assert.False(mapperInvoked);
        Assert.Equal(42, result.GetValueOrThrow());
    }

    // ============================================
    // Integration with Pipe
    // ============================================

    [Fact]
    public void Try_InPipelineWithPipe_Works()
    {
        var result = "  42  "
            .Pipe(s => s.Trim())
            .Try(int.Parse)
            .Map(x => x * 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public void Try_InPipelineWithPipe_Failure_StopsChain()
    {
        var result = "  invalid  "
            .Pipe(s => s.Trim())
            .Try(int.Parse)
            .Map(x => x * 2);  // This should not execute

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Try_AfterResultOf_WithBind_Works()
    {
        var result = "42"
            .Try(int.Parse)
            .Bind(x => x > 0
                ? ResultOf.Ok<int, Exception>(x * 2)
                : ResultOf.Fail<int, Exception>(new InvalidOperationException("Must be positive")));

        Assert.True(result.IsSuccess);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    // ============================================
    // Real-World Examples
    // ============================================

    [Fact]
    public void Try_ParseAge_CompleteExample()
    {
        var validAge = "25"
            .Try(int.Parse)
            .Ensure(x => x >= 0, new ArgumentException("Age cannot be negative"))
            .Ensure(x => x <= 150, new ArgumentException("Age too high"))
            .GetValueOr(0);

        Assert.Equal(25, validAge);
    }

    [Fact]
    public void Try_ParseAge_Invalid_ReturnsDefault()
    {
        var invalidAge = "abc"
            .Try(int.Parse)
            .Ensure(x => x >= 0, new ArgumentException("Age cannot be negative"))
            .GetValueOr(0);

        Assert.Equal(0, invalidAge);
    }

    [Fact]
    public void Try_FileOperation_WithErrorMapper()
    {
        var result = "nonexistent.txt".Try(
            path => File.ReadAllText(path),
            ex => $"File error: {ex.GetType().Name}"
        );

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("File should not exist"),
            error => Assert.Contains("File error", error.ErrorValue)
        );
    }

    [Fact]
    public void Try_UriParsing_Success()
    {
        var result = "https://example.com".Try(s => new Uri(s));

        Assert.True(result.IsSuccess);
        result.Match(
            ok => Assert.Equal("example.com", ok.Value.Host),
            error => Assert.Fail($"Should succeed: {error.ErrorValue.Message}")
        );
    }

    [Fact]
    public void Try_UriParsing_Invalid()
    {
        var result = "not a valid uri".Try(s => new Uri(s));

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("Should fail"),
            error => Assert.IsType<UriFormatException>(error.ErrorValue)
        );
    }

    // ============================================
    // Asynchronous Try
    // ============================================

    private static async Task<int> ParseAndDoubleAsync(string input)
    {
        await Task.Delay(1);
        return int.Parse(input) * 2;
    }

    private static async Task<string> ThrowAfterDelayAsync(string input)
    {
        await Task.Delay(1);
        throw new InvalidOperationException("Simulated error");
    }

    [Fact]
    public async Task TryAsync_WithSuccessfulOperation_ReturnsOk()
    {
        var result = await "42".TryAsync(ParseAndDoubleAsync);

        Assert.True(result.IsSuccess);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public async Task TryAsync_WithFailingOperation_ReturnsError()
    {
        var result = await "invalid".TryAsync(ParseAndDoubleAsync);

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("Should not be Ok"),
            error => Assert.IsType<FormatException>(error.ErrorValue)
        );
    }

    [Fact]
    public async Task TryAsync_WithErrorMapper_MapsException()
    {
        var result = await "test".TryAsync(
            ThrowAfterDelayAsync,
            ex => $"Async error: {ex.Message}"
        );

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("Should not be Ok"),
            error => Assert.Contains("Async error: Simulated error", error.ErrorValue)
        );
    }

    [Fact]
    public async Task TryAsync_OnTask_Works()
    {
        var result = await Task.FromResult("42")
            .TryAsync(ParseAndDoubleAsync);

        Assert.True(result.IsSuccess);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public async Task TryAsync_OnTask_WithSyncFunction_Works()
    {
        var result = await Task.FromResult("42")
            .Try(int.Parse);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public async Task TryAsync_InPipeline_Works()
    {
        var result = await "  42  "
            .Pipe(s => s.Trim())
            .TryAsync(ParseAndDoubleAsync)
            .Map(x => x + 10);

        Assert.True(result.IsSuccess);
        Assert.Equal(94, result.GetValueOrThrow());
    }

    // ============================================
    // Chaining with ResultOf methods
    // ============================================

    [Fact]
    public void Try_ThenBind_Works()
    {
        var result = "42".Try(int.Parse)
            .Bind(x => x > 40
                ? ResultOf.Ok<string, Exception>($"High: {x}")
                : ResultOf.Ok<string, Exception>($"Low: {x}"));

        Assert.True(result.IsSuccess);
        Assert.Equal("High: 42", result.GetValueOrThrow());
    }

    [Fact]
    public void Try_ThenMap_Works()
    {
        var result = "42".Try(int.Parse)
            .Map(x => x * 2)
            .Map(x => $"Result: {x}");

        Assert.True(result.IsSuccess);
        Assert.Equal("Result: 84", result.GetValueOrThrow());
    }

    [Fact]
    public void Try_ThenTap_Works()
    {
        var log = new List<int>();

        var result = "42".Try(int.Parse)
            .TapOk(x => log.Add(x))
            .Map(x => x * 2)
            .TapOk(x => log.Add(x));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 42, 84 }, log);
    }

    [Fact]
    public void Try_ThenEnsure_Works()
    {
        var positive = "42".Try(int.Parse)
            .Ensure(x => x > 0, new ArgumentException("Must be positive"));

        var negative = "-42".Try(int.Parse)
            .Ensure(x => x > 0, new ArgumentException("Must be positive"));

        Assert.True(positive.IsSuccess);
        Assert.True(negative.IsFailure);
    }

    // ============================================
    // Error Scenarios
    // ============================================

    [Fact]
    public void Try_WithNullInput_HandlesGracefully()
    {
        string? nullString = null;

        var result = nullString!.Try(s => s.Length);

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("Should fail with null"),
            error => Assert.IsType<NullReferenceException>(error.ErrorValue)
        );
    }

    [Fact]
    public void Try_WithCustomException_PreservesExceptionType()
    {
        var result = "test".Try<string, string, Exception>(
            s => throw new InvalidOperationException("Custom error"),
            ex => ex
        );

        Assert.True(result.IsFailure);
        result.Match(
            ok => Assert.Fail("Should fail"),
            error => Assert.IsType<InvalidOperationException>(error.ErrorValue)
        );
    }

    [Fact]
    public void Try_MultipleTrysInChain_FirstFailureStops()
    {
        var result = "42".Try(int.Parse)
            .Bind(x => "invalid".Try(int.Parse))
            .Map(x => x * 2);

        Assert.True(result.IsFailure);
    }

    // ============================================
    // Complex Scenarios
    // ============================================

    [Fact]
    public void Try_ComplexPipeline_Success()
    {
        var result = "  123  "
            .Pipe(s => s.Trim())
            .Try(int.Parse)
            .Ensure(x => x > 0, new ArgumentException("Must be positive"))
            .Map(x => x * 2)
            .Bind(x => x < 1000
                ? ResultOf.Ok<int, Exception>(x)
                : ResultOf.Fail<int, Exception>(new ArgumentException("Too large")))
            .GetValueOr(0);

        Assert.Equal(246, result);
    }

    [Fact]
    public void Try_ComplexPipeline_FailsAtParse()
    {
        var result = "  invalid  "
            .Pipe(s => s.Trim())
            .Try(int.Parse)
            .Ensure(x => x > 0, new ArgumentException("Must be positive"))
            .Map(x => x * 2)
            .GetValueOr(-1);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void Try_ComplexPipeline_FailsAtEnsure()
    {
        var result = "  -123  "
            .Pipe(s => s.Trim())
            .Try(int.Parse)
            .Ensure(x => x > 0, new ArgumentException("Must be positive"))
            .Map(x => x * 2)
            .GetValueOr(-1);

        Assert.Equal(-1, result);
    }

    [Fact]
    public async Task TryAsync_ComplexPipeline_Success()
    {
        var result = await "42"
            .TryAsync(ParseAndDoubleAsync)
            .Map(x => x + 10);

        var final = result.Match(
            ok => ok.Value < 100
                ? ResultOf.Ok<int, Exception>(ok.Value)
                : ResultOf.Fail<int, Exception>(new ArgumentException("Too large")),
            error => ResultOf.Fail<int, Exception>(error.ErrorValue)
        );

        Assert.True(final.IsSuccess);
        Assert.Equal(94, final.GetValueOrThrow());
    }

    // ============================================
    // Helper Types
    // ============================================

    private enum ParseError
    {
        InvalidFormat,
        Overflow
    }
}
