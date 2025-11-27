namespace Corsinvest.Fx.Functional.Tests;

// Test union types
[Union]
public partial record ResultTest<T, E>
{
    public partial record Ok(T Value);
    public partial record Error(E Value);
}

[Union]
public partial record OptionTest<T>
{
    public partial record Some(T Value);
    public partial record None();
}

[Union]
public partial record Shape
{
    public partial record Circle(double Radius);
    public partial record Rectangle(double Width, double Height);
    public partial record Triangle(double Base, double Height);
}

public class UnionTests
{
    [Fact]
    public void Result_Ok_CreatesCorrectInstance()
    {
        // Arrange & Act
        var result = new ResultTest<string, string>.Ok("success");

        // Assert
        Assert.True(result.IsOk);
        Assert.False(result.IsError);
    }

    [Fact]
    public void Result_Error_CreatesCorrectInstance()
    {
        // Arrange & Act
        var result = new ResultTest<string, string>.Error("failure");

        // Assert
        Assert.False(result.IsOk);
        Assert.True(result.IsError);
    }

    [Fact]
    public void Result_Match_ExecutesCorrectBranch()
    {
        // Arrange
        var okResult = new ResultTest<int, string>.Ok(42);
        var errorResult = new ResultTest<int, string>.Error("failure");

        // Act & Assert
        var okMessage = okResult.Match(
            ok => $"Success: {ok.Value}",
            error => $"Error: {error.Value}"
        );
        Assert.Equal("Success: 42", okMessage);

        var errorMessage = errorResult.Match(
            ok => $"Success: {ok.Value}",
            error => $"Error: {error.Value}"
        );
        Assert.Equal("Error: failure", errorMessage);
    }

    [Fact]
    public void Result_MatchVoid_ExecutesCorrectBranch()
    {
        // Arrange
        var result = new ResultTest<int, string>.Ok(42);
        string? executedBranch = null;

        // Act
        result.Match(
            ok => executedBranch = "ok",
            error => executedBranch = "error"
        );

        // Assert
        Assert.Equal("ok", executedBranch);
    }

    [Fact]
    public async Task Result_MatchAsync_ExecutesCorrectBranch()
    {
        // Arrange
        var result = new ResultTest<int, string>.Ok(42);

        // Act
        var message = await result.MatchAsync(
            async ok =>
            {
                await Task.Delay(1);
                return $"Async success: {ok.Value}";
            },
            async error =>
            {
                await Task.Delay(1);
                return $"Async error: {error.Value}";
            }
        );

        // Assert
        Assert.Equal("Async success: 42", message);
    }

    [Fact]
    public void Result_TryGet_ReturnsCorrectValue()
    {
        // Arrange
        var okResult = new ResultTest<int, string>.Ok(42);
        var errorResult = new ResultTest<int, string>.Error("failure");

        // Act & Assert
        Assert.True(okResult.TryGetOk(out var ok));
        Assert.Equal(42, ok.Value);

        Assert.False(okResult.TryGetError(out _));

        Assert.True(errorResult.TryGetError(out var error));
        Assert.Equal("failure", error.Value);

        Assert.False(errorResult.TryGetOk(out _));
    }

    [Fact]
    public void Option_Some_CreatesCorrectInstance()
    {
        // Arrange & Act
        var option = new OptionTest<string>.Some("value");

        // Assert
        Assert.True(option.IsSome);
        Assert.False(option.IsNone);
    }

    [Fact]
    public void Option_None_CreatesCorrectInstance()
    {
        // Arrange & Act
        var option = new OptionTest<string>.None();

        // Assert
        Assert.False(option.IsSome);
        Assert.True(option.IsNone);
    }

    [Fact]
    public void Shape_Circle_CalculatesAreaCorrectly()
    {
        // Arrange
        var shapes = new Shape[]
        {
            new Shape.Circle(5.0),
            new Shape.Rectangle(4.0, 6.0),
            new Shape.Triangle(3.0, 8.0)
        };

        // Act & Assert
        foreach (var shape in shapes)
        {
            var area = shape.Match(
                circle => Math.PI * circle.Radius * circle.Radius,
                rectangle => rectangle.Width * rectangle.Height,
                triangle => 0.5 * triangle.Base * triangle.Height
            );

            var expected = shape switch
            {
                Shape.Circle(var radius) => Math.PI * radius * radius,
                Shape.Rectangle(var width, var height) => width * height,
                Shape.Triangle(var @base, var height) => 0.5 * @base * height,
                _ => throw new InvalidOperationException()
            };

            Assert.Equal(expected, area, precision: 10);
        }
    }

    [Fact]
    public void Union_TypeChecking_Works()
    {
        // Act
        var okResult = new ResultTest<string, int>.Ok("success");
        var errorResult = new ResultTest<string, int>.Error(404);

        // Assert
        Assert.True(okResult.IsOk);
        Assert.True(errorResult.IsError);
    }

    [Fact]
    public void Union_ComplexScenario_ApiResponse()
    {
        // Arrange
        ResultTest<User, ApiError>[] responses = [
            new ResultTest<User, ApiError>.Ok(new User("John", 30)),
            new ResultTest<User, ApiError>.Error(new ApiError(404, "Not Found")),
            new ResultTest<User, ApiError>.Error(new ApiError(500, "Server Error"))
        ];

        // Act
        var messages = responses.Select(response => response.Match(
            ok => $"User: {ok.Value.Name}, Age: {ok.Value.Age}",
            error => $"Error {error.Value.Code}: {error.Value.Message}"
        )).ToList();

        // Assert
        Assert.Equal("User: John, Age: 30", messages[0]);
        Assert.Equal("Error 404: Not Found", messages[1]);
        Assert.Equal("Error 500: Server Error", messages[2]);
    }

    [Fact]
    public void Union_PatternMatching_WithCSharpSwitch()
    {
        // Arrange
        ResultTest<int, string> result = new ResultTest<int, string>.Ok(42);

        // Act
        var message = result switch
        {
            ResultTest<int, string>.Ok ok => $"Got value: {ok.Value}",
            ResultTest<int, string>.Error error => $"Got error: {error.Value}",
            _ => "Unknown"
        };

        // Assert
        Assert.Equal("Got value: 42", message);
    }
}

// Helper types for testing
public record User(string Name, int Age);
public record ApiError(int Code, string Message);
