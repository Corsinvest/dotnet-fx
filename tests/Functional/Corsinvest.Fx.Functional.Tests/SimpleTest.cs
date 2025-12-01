namespace Corsinvest.Fx.Functional.Tests;

[Union]
public partial record SimpleResult<T>
{
    public partial record Success(T Value);
    public partial record Failure(string Error);
}

public class SimpleTests
{
    [Fact]
    public void Simple_Test()
    {
        var success = new SimpleResult<int>.Success(42);
        Assert.True(success.IsSuccess);
        Assert.False(success.IsFailure);
    }
}