namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Locks the public shape of Option and ResultOf after the move to the IUnion marker.
/// </summary>
public class UnionCoreTypesTests
{
    [Fact]
    public void Option_Some_CarriesTheValue()
    {
        var option = Option.Some(42);

        Assert.True(option.IsSome);
        Assert.Equal(42, option.Match(some => some.Value, none => 0));
    }

    [Fact]
    public void Option_None_IsRecognised()
    {
        var option = Option.None<int>();

        Assert.True(option.IsNone);
        Assert.Equal(0, option.Match(some => some.Value, none => 0));
    }

    [Fact]
    public void Option_SupportsNativeSwitch()
    {
        Option<int> option = Option.Some(7);

        var result = option switch
        {
            Option<int>.Some(var some) => some.Value,
            Option<int>.None => 0
        };

        Assert.Equal(7, result);
    }

    [Fact]
    public void Option_Map_StillChains()
    {
        Assert.Equal(10, Option.Some(5).Map(x => x * 2).GetValueOr(0));
    }

    [Fact]
    public void ResultOf_Ok_CarriesTheValue()
    {
        var result = ResultOf.Ok<int, string>(42);

        Assert.True(result.IsOk);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Match(ok => ok.Value, fail => 0));
    }

    [Fact]
    public void ResultOf_Fail_CarriesTheError()
    {
        var result = ResultOf.Fail<int, string>("boom");

        Assert.True(result.IsFail);
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Match(ok => "", fail => fail.ErrorValue));
    }

    [Fact]
    public void ResultOf_SupportsNativeSwitch()
    {
        ResultOf<int, string> result = ResultOf.Ok<int, string>(7);

        var value = result switch
        {
            ResultOf<int, string>.Ok(var ok) => ok.Value,
            ResultOf<int, string>.Fail => -1
        };

        Assert.Equal(7, value);
    }

    [Fact]
    public void ResultOf_BindStillShortCircuits()
    {
        var result = ResultOf.Ok<int, string>(1)
                             .Bind(x => ResultOf.Fail<int, string>("stop"))
                             .Bind(x => ResultOf.Ok<int, string>(x + 1));

        Assert.Equal("stop", result.Match(ok => "", fail => fail.ErrorValue));
    }
}
