namespace Corsinvest.Fx.Functional.Tests;

public record Success<T>(T Value);
public record Failure(string Error);

[UnionCaseName<Success<int>>("Success")]
public abstract partial record SimpleResult<T> : IUnion<Success<T>, Failure>;

public class SimpleTests
{
    [Fact]
    public void Simple_Test()
    {
        var success = new SimpleResult<int>.Success(new Success<int>(42));
        Assert.True(success.IsSuccess);
        Assert.False(success.IsFailure);
    }
}