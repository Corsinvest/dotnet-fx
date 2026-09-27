/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

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

public enum Severity { Info, Warning, Error }
public record Detail(string Text);

/// <summary>A union with an enum case, which the generator caches, and a record case, which it does not.</summary>
public abstract partial record Problem : IUnion<Severity, Detail>;

/// <summary>
/// The enum cache is an allocation optimisation, so what these assert is that it changes nothing
/// else: a cached wrapper has to behave exactly as a freshly constructed one.
/// </summary>
public class EnumCaseCacheTests
{
    [Fact]
    public void DeclaredMember_ReusesOneInstance()
    {
        Problem a = Severity.Warning;
        Problem b = Severity.Warning;

        Assert.Same(a, b);
    }

    [Fact]
    public void DifferentMembers_AreDifferentInstances()
    {
        Problem warning = Severity.Warning;
        Problem error = Severity.Error;

        Assert.NotSame(warning, error);
        Assert.NotEqual(warning, error);
    }

    [Fact]
    public void CachingIsNotObservable()
    {
        Problem cached = Severity.Warning;
        var fresh = new Problem.Severity(Severity.Warning);

        Assert.Equal(cached, fresh);
        Assert.True(cached == fresh);
        Assert.Equal(cached.GetHashCode(), fresh.GetHashCode());
        Assert.Single(new HashSet<Problem> { cached, fresh });
        Assert.True(cached.IsSeverity);
        Assert.True(cached.TryGetSeverity(out var value));
        Assert.Equal(Severity.Warning, value);
        Assert.Equal("Warning", cached.Match(s => s.ToString(), d => d.Text));
    }

    [Fact]
    public void UndeclaredValue_StillWorks()
    {
        // An enum is not restricted to its declared members: (Severity)99 is legal C#, and the
        // lookup has to fall back to allocating rather than indexing past its table.
        Problem odd = (Severity)99;

        Assert.True(odd.IsSeverity);
        Assert.True(odd.TryGetSeverity(out var value));
        Assert.Equal((Severity)99, value);
        Assert.NotSame(odd, (Problem)(Severity)99);
    }

    [Fact]
    public void NegativeUndeclaredValue_StillWorks()
    {
        Problem negative = (Severity)(-5);

        Assert.True(negative.TryGetSeverity(out var value));
        Assert.Equal((Severity)(-5), value);
    }

    [Fact]
    public void RecordCase_IsNotCached()
    {
        // No finite set of values to enumerate, so every one is its own object.
        Problem a = new Detail("x");
        Problem b = new Detail("x");

        Assert.NotSame(a, b);
        Assert.Equal(a, b);      // still equal by value
    }

    [Fact]
    public void ResultOfWithAnEnumError_IsNotCached()
    {
        // The cache keys off the case type being an enum. ResultOf<T, E>'s cases are Ok<T> and
        // Fail<E> - records whatever E is - so an enum error type does not reach it. Asserted so
        // the distinction does not get documented the wrong way round again.
        var a = ResultOf.Fail<int, Severity>(Severity.Warning);
        var b = ResultOf.Fail<int, Severity>(Severity.Warning);

        Assert.NotSame(a, b);
        Assert.Equal(a, b);      // still equal by value
    }

    [Fact]
    public void SwitchOverACachedCase_Matches()
    {
        Problem problem = Severity.Error;

        var described = problem switch
        {
            Problem.Severity(var s) => $"severity {s}",
            Problem.Detail(var d) => d.Text,
        };

        Assert.Equal("severity Error", described);
    }
}
