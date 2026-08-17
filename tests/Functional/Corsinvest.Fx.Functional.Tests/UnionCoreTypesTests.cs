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
}
