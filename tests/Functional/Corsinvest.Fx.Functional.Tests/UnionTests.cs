namespace Corsinvest.Fx.Functional.Tests;

// Test union types
public record OkCase<T>(T Value);
public record ErrorCase<E>(E Value);

[UnionCaseName<OkCase<int>>("Ok")]
[UnionCaseName<ErrorCase<int>>("Error")]
public abstract partial record ResultTest<T, E> : IUnion<OkCase<T>, ErrorCase<E>>;

public record SomeCase<T>(T Value);
public record NoneCase;

[UnionCaseName<SomeCase<int>>("Some")]
[UnionCaseName<NoneCase>("None")]
public abstract partial record OptionTest<T> : IUnion<SomeCase<T>, NoneCase>;

public record Circle(double Radius);
public record Rectangle(double Width, double Height);
public record Triangle(double Base, double Height);

public abstract partial record Shape : IUnion<Circle, Rectangle, Triangle>;

public class UnionTests
{
    [Fact]
    public void Result_Ok_CreatesCorrectInstance()
    {
        // Arrange & Act
        var result = new ResultTest<string, string>.Ok(new OkCase<string>("success"));

        // Assert
        Assert.True(result.IsOk);
        Assert.False(result.IsError);
    }

    [Fact]
    public void Result_Error_CreatesCorrectInstance()
    {
        // Arrange & Act
        var result = new ResultTest<string, string>.Error(new ErrorCase<string>("failure"));

        // Assert
        Assert.False(result.IsOk);
        Assert.True(result.IsError);
    }

    [Fact]
    public void Result_Match_ExecutesCorrectBranch()
    {
        // Arrange
        var okResult = new ResultTest<int, string>.Ok(new OkCase<int>(42));
        var errorResult = new ResultTest<int, string>.Error(new ErrorCase<string>("failure"));

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
        var result = new ResultTest<int, string>.Ok(new OkCase<int>(42));
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
        var result = new ResultTest<int, string>.Ok(new OkCase<int>(42));

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
        var okResult = new ResultTest<int, string>.Ok(new OkCase<int>(42));
        var errorResult = new ResultTest<int, string>.Error(new ErrorCase<string>("failure"));

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
        var option = new OptionTest<string>.Some(new SomeCase<string>("value"));

        // Assert
        Assert.True(option.IsSome);
        Assert.False(option.IsNone);
    }

    [Fact]
    public void Option_None_CreatesCorrectInstance()
    {
        // Arrange & Act
        var option = new OptionTest<string>.None(new NoneCase());

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
            new Shape.Circle(new Circle(5.0)),
            new Shape.Rectangle(new Rectangle(4.0, 6.0)),
            new Shape.Triangle(new Triangle(3.0, 8.0))
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
                Shape.Circle(var circle) => Math.PI * circle.Radius * circle.Radius,
                Shape.Rectangle(var rectangle) => rectangle.Width * rectangle.Height,
                Shape.Triangle(var triangle) => 0.5 * triangle.Base * triangle.Height,
                _ => throw new InvalidOperationException()
            };

            Assert.Equal(expected, area, precision: 10);
        }
    }

    [Fact]
    public void Union_TypeChecking_Works()
    {
        // Act
        var okResult = new ResultTest<string, int>.Ok(new OkCase<string>("success"));
        var errorResult = new ResultTest<string, int>.Error(new ErrorCase<int>(404));

        // Assert
        Assert.True(okResult.IsOk);
        Assert.True(errorResult.IsError);
    }

    [Fact]
    public void Union_ComplexScenario_ApiResponse()
    {
        // Arrange
        ResultTest<User, ApiError>[] responses = [
            new ResultTest<User, ApiError>.Ok(new OkCase<User>(new User("John", 30))),
            new ResultTest<User, ApiError>.Error(new ErrorCase<ApiError>(new ApiError(404, "Not Found"))),
            new ResultTest<User, ApiError>.Error(new ErrorCase<ApiError>(new ApiError(500, "Server Error")))
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
        ResultTest<int, string> result = new ResultTest<int, string>.Ok(new OkCase<int>(42));

        // Act
        var message = result switch
        {
            ResultTest<int, string>.Ok(var ok) => $"Got value: {ok.Value}",
            ResultTest<int, string>.Error(var error) => $"Got error: {error.Value}",
            _ => "Unknown"
        };

        // Assert
        Assert.Equal("Got value: 42", message);
    }
}

// Helper types for testing
public record User(string Name, int Age);
public record ApiError(int Code, string Message);
