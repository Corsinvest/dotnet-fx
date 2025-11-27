using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Functional.Tests;

public class OptionTests
{
    [Fact]
    public void Some_CreatesOptionWithValue()
    {
        var option = Option.Some(42);

        Assert.True(option.IsSome);
        Assert.False(option.IsNone);
        Assert.True(option.TryGetSome(out var some));
        Assert.Equal(42, some.Value);
    }

    [Fact]
    public void None_CreatesEmptyOption()
    {
        var option = Option.None<int>();

        Assert.False(option.IsSome);
        Assert.True(option.IsNone);
        Assert.True(option.TryGetNone(out _));
    }

    [Fact]
    public void FromNullable_WithNullClass_ReturnsNone()
    {
        string? nullStr = null;
        var option = Option.FromNullable(nullStr);

        Assert.True(option.IsNone);
    }

    [Fact]
    public void FromNullable_WithNonNullClass_ReturnsSome()
    {
        string? str = "hello";
        var option = Option.FromNullable(str);

        Assert.True(option.IsSome);
        Assert.Equal("hello", option.GetValueOrThrow());
    }

    [Fact]
    public void FromNullable_WithNullStruct_ReturnsNone()
    {
        int? nullInt = null;
        var option = Option.FromNullable(nullInt);

        Assert.True(option.IsNone);
    }

    [Fact]
    public void FromNullable_WithValueStruct_ReturnsSome()
    {
        int? value = 42;
        var option = Option.FromNullable(value);

        Assert.True(option.IsSome);
        Assert.Equal(42, option.GetValueOrThrow());
    }

    [Fact]
    public void GetValueOr_WithSome_ReturnsValue()
    {
        var option = Option.Some(42);
        var result = option.GetValueOr(0);

        Assert.Equal(42, result);
    }

    [Fact]
    public void GetValueOr_WithNone_ReturnsDefault()
    {
        var option = Option.None<int>();
        var result = option.GetValueOr(99);

        Assert.Equal(99, result);
    }

    [Fact]
    public void GetValueOr_WithFactory_WithSome_ReturnsValue()
    {
        var option = Option.Some(42);
        var result = option.GetValueOr(() => 99);

        Assert.Equal(42, result);
    }

    [Fact]
    public void GetValueOr_WithFactory_WithNone_CallsFactory()
    {
        var option = Option.None<int>();
        var factoryCalled = false;
        var result = option.GetValueOr(() =>
        {
            factoryCalled = true;
            return 99;
        });

        Assert.Equal(99, result);
        Assert.True(factoryCalled);
    }

    [Fact]
    public void GetValueOrThrow_WithSome_ReturnsValue()
    {
        var option = Option.Some(42);
        var result = option.GetValueOrThrow();

        Assert.Equal(42, result);
    }

    [Fact]
    public void GetValueOrThrow_WithNone_Throws()
    {
        var option = Option.None<int>();

        Assert.Throws<InvalidOperationException>(() => option.GetValueOrThrow());
    }

    [Fact]
    public void GetValueOrThrow_WithNone_ThrowsWithMessage()
    {
        var option = Option.None<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            option.GetValueOrThrow("Custom error"));
        Assert.Equal("Custom error", ex.Message);
    }

    [Fact]
    public void TryGetValue_WithSome_ReturnsTrue()
    {
        var option = Option.Some(42);
        var success = option.TryGetValue(out var value);

        Assert.True(success);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_WithNone_ReturnsFalse()
    {
        var option = Option.None<int>();
        var success = option.TryGetValue(out var value);

        Assert.False(success);
        Assert.Equal(default, value);
    }

    [Fact]
    public void ToNullable_WithSome_ReturnsValue()
    {
        var option = Option.Some("hello");
        var result = option.ToNullable();

        Assert.NotNull(result);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ToNullable_WithNone_ReturnsNull()
    {
        var option = Option.None<string>();
        var result = option.ToNullable();

        Assert.Null(result);
    }

    [Fact]
    public void ToNullableStruct_WithSome_ReturnsValue()
    {
        var option = Option.Some(42);
        var result = option.ToNullableStruct();

        Assert.True(result.HasValue);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToNullableStruct_WithNone_ReturnsNull()
    {
        var option = Option.None<int>();
        var result = option.ToNullableStruct();

        Assert.False(result.HasValue);
    }

    [Fact]
    public void Map_WithSome_TransformsValue()
    {
        var option = Option.Some(42);
        var result = option.Map(x => x * 2);

        Assert.True(result.IsSome);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public void Map_WithNone_ReturnsNone()
    {
        var option = Option.None<int>();
        var result = option.Map(x => x * 2);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Bind_WithSome_ChainsOperation()
    {
        var option = Option.Some(42);
        var result = option.Bind(x => x > 0 ? Option.Some(x * 2) : Option.None<int>());

        Assert.True(result.IsSome);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public void Bind_WithNone_ReturnsNone()
    {
        var option = Option.None<int>();
        var result = option.Bind(x => Option.Some(x * 2));

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Bind_WithSomeReturningNone_ReturnsNone()
    {
        var option = Option.Some(-5);
        var result = option.Bind(x => x > 0 ? Option.Some(x * 2) : Option.None<int>());

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Filter_WithMatchingPredicate_ReturnsSome()
    {
        var option = Option.Some(42);
        var result = option.Filter(x => x > 0);

        Assert.True(result.IsSome);
        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public void Filter_WithFailingPredicate_ReturnsNone()
    {
        var option = Option.Some(42);
        var result = option.Filter(x => x < 0);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Filter_WithNone_ReturnsNone()
    {
        var option = Option.None<int>();
        var result = option.Filter(x => true);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Tap_WithSome_ExecutesAction()
    {
        var option = Option.Some(42);
        var sideEffect = 0;
        var result = option.Tap(x => sideEffect = x);

        Assert.Equal(42, sideEffect);
        Assert.Same(option, result); // Should return same instance
    }

    [Fact]
    public void Tap_WithNone_DoesNotExecuteAction()
    {
        var option = Option.None<int>();
        var sideEffect = 0;
        var result = option.Tap(x => sideEffect = x);

        Assert.Equal(0, sideEffect);
        Assert.Same(option, result);
    }

    [Fact]
    public void Select_LinqSupport_Works()
    {
        var option = Option.Some(42);
        var result = from x in option
                     select x * 2;

        Assert.True(result.IsSome);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public void SelectMany_LinqSupport_Works()
    {
        var option1 = Option.Some(10);
        var option2 = Option.Some(5);

        var result = from x in option1
                     from y in option2
                     select x + y;

        Assert.True(result.IsSome);
        Assert.Equal(15, result.GetValueOrThrow());
    }

    [Fact]
    public void Where_LinqSupport_Works()
    {
        var option = Option.Some(42);
        var result = from x in option
                     where x > 0
                     select x;

        Assert.True(result.IsSome);
        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public void Where_LinqSupport_WithFailingPredicate_ReturnsNone()
    {
        var option = Option.Some(42);
        var result = from x in option
                     where x < 0
                     select x;

        Assert.True(result.IsNone);
    }

    [Fact]
    public void ToResult_WithSome_ReturnsOk()
    {
        var option = Option.Some(42);
        var result = option.ToResult("error");

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Match(ok => ok.Value, error => 0));
    }

    [Fact]
    public void ToResult_WithNone_ReturnsFail()
    {
        var option = Option.None<int>();
        var result = option.ToResult("error");

        Assert.True(result.IsFailure);
        Assert.Equal("error", result.Match(ok => "", error => error.ErrorValue));
    }

    [Fact]
    public void ToResult_WithFactory_WithSome_ReturnsOk()
    {
        var option = Option.Some(42);
        var factoryCalled = false;
        var result = option.ToResult(() =>
        {
            factoryCalled = true;
            return "error";
        });

        Assert.True(result.IsOk);
        Assert.False(factoryCalled);
    }

    [Fact]
    public void ToResult_WithFactory_WithNone_CallsFactory()
    {
        var option = Option.None<int>();
        var factoryCalled = false;
        var result = option.ToResult(() =>
        {
            factoryCalled = true;
            return "error";
        });

        Assert.True(result.IsFailure);
        Assert.True(factoryCalled);
    }

    [Fact]
    public async Task MapAsync_WithSome_TransformsValueAsync()
    {
        var option = Option.Some(42);
        var result = await option.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.True(result.IsSome);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public async Task MapAsync_WithNone_ReturnsNone()
    {
        var option = Option.None<int>();
        var result = await option.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task BindAsync_WithSome_ChainsOperationAsync()
    {
        var option = Option.Some(42);
        var result = await option.BindAsync(async x =>
        {
            await Task.Delay(1);
            return Option.Some(x * 2);
        });

        Assert.True(result.IsSome);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public async Task BindAsync_WithNone_ReturnsNone()
    {
        var option = Option.None<int>();
        var result = await option.BindAsync(async x =>
        {
            await Task.Delay(1);
            return Option.Some(x * 2);
        });

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task TapAsync_WithSome_ExecutesActionAsync()
    {
        var option = Option.Some(42);
        var sideEffect = 0;
        var result = await option.TapAsync(async x =>
        {
            await Task.Delay(1);
            sideEffect = x;
        });

        Assert.Equal(42, sideEffect);
        Assert.Same(option, result);
    }

    [Fact]
    public async Task TapAsync_WithNone_DoesNotExecuteActionAsync()
    {
        var option = Option.None<int>();
        var sideEffect = 0;
        var result = await option.TapAsync(async x =>
        {
            await Task.Delay(1);
            sideEffect = x;
        });

        Assert.Equal(0, sideEffect);
        Assert.Same(option, result);
    }

    [Fact]
    public async Task MapAsync_OnTask_Works()
    {
        var optionTask = Task.FromResult(Option.Some(42));
        var result = await optionTask.MapAsync(x => x * 2);

        Assert.True(result.IsSome);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public async Task BindAsync_OnTask_Works()
    {
        var optionTask = Task.FromResult(Option.Some(42));
        var result = await optionTask.BindAsync(x => Option.Some(x * 2));

        Assert.True(result.IsSome);
        Assert.Equal(84, result.GetValueOrThrow());
    }

    [Fact]
    public void Match_WithSome_ExecutesSomeCase()
    {
        var option = Option.Some(42);
        var result = option.Match(
            some => $"Value: {some.Value}",
            none => "No value"
        );

        Assert.Equal("Value: 42", result);
    }

    [Fact]
    public void Match_WithNone_ExecutesNoneCase()
    {
        var option = Option.None<int>();
        var result = option.Match(
            some => $"Value: {some.Value}",
            none => "No value"
        );

        Assert.Equal("No value", result);
    }

    // ============================================
    // Flatten Tests
    // ============================================

    [Fact]
    public void Flatten_WithSomeSome_ReturnsInnerSome()
    {
        var nested = Option.Some(Option.Some(42));
        var result = nested.Flatten();

        Assert.True(result.IsSome);
        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public void Flatten_WithSomeNone_ReturnsNone()
    {
        var nested = Option.Some(Option.None<int>());
        var result = nested.Flatten();

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Flatten_WithNone_ReturnsNone()
    {
        var nested = Option.None<Option<int>>();
        var result = nested.Flatten();

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Flatten_RealWorld_ParseAndValidateChain()
    {
        Option<int> TryParseInt(string input)
            => int.TryParse(input, out var value)
                ? Option.Some(value)
                : Option.None<int>();

        Option<int> ValidatePositive(int value)
            => value > 0
                ? Option.Some(value)
                : Option.None<int>();

        // Using Map creates nested Option
        var nested = TryParseInt("42").Map(ValidatePositive);
        var result = nested.Flatten();

        Assert.True(result.IsSome);
        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public void Flatten_RealWorld_FailedValidation()
    {
        Option<int> TryParseInt(string input)
            => int.TryParse(input, out var value)
                ? Option.Some(value)
                : Option.None<int>();

        Option<int> ValidatePositive(int value)
            => value > 0
                ? Option.Some(value)
                : Option.None<int>();

        var nested = TryParseInt("-5").Map(ValidatePositive);
        var result = nested.Flatten();

        Assert.True(result.IsNone);
    }

    // ============================================
    // OrElse Tests
    // ============================================

    [Fact]
    public void OrElse_WithSome_ReturnsOriginal()
    {
        var primary = Option.Some(42);
        var alternative = Option.Some(99);

        var result = primary.OrElse(alternative);

        Assert.True(result.IsSome);
        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public void OrElse_WithNone_ReturnsAlternative()
    {
        var primary = Option.None<int>();
        var alternative = Option.Some(99);

        var result = primary.OrElse(alternative);

        Assert.True(result.IsSome);
        Assert.Equal(99, result.GetValueOrThrow());
    }

    [Fact]
    public void OrElse_WithBothNone_ReturnsNone()
    {
        var primary = Option.None<int>();
        var alternative = Option.None<int>();

        var result = primary.OrElse(alternative);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void OrElse_ChainMultiple_ReturnsFirstSome()
    {
        var opt1 = Option.None<int>();
        var opt2 = Option.None<int>();
        var opt3 = Option.Some(99);
        var opt4 = Option.Some(42);

        var result = opt1.OrElse(opt2).OrElse(opt3).OrElse(opt4);

        Assert.True(result.IsSome);
        Assert.Equal(99, result.GetValueOrThrow());
    }

    [Fact]
    public void OrElse_WithFactory_WithSome_DoesNotCallFactory()
    {
        var primary = Option.Some(42);
        var factoryCalled = false;

        var result = primary.OrElse(() =>
        {
            factoryCalled = true;
            return Option.Some(99);
        });

        Assert.True(result.IsSome);
        Assert.Equal(42, result.GetValueOrThrow());
        Assert.False(factoryCalled);
    }

    [Fact]
    public void OrElse_WithFactory_WithNone_CallsFactory()
    {
        var primary = Option.None<int>();
        var factoryCalled = false;

        var result = primary.OrElse(() =>
        {
            factoryCalled = true;
            return Option.Some(99);
        });

        Assert.True(result.IsSome);
        Assert.Equal(99, result.GetValueOrThrow());
        Assert.True(factoryCalled);
    }

    [Fact]
    public void OrElse_RealWorld_CacheFallbackChain()
    {
        Option<string> GetFromCache(int id) => Option.None<string>();
        Option<string> GetFromDatabase(int id) => Option.Some($"User-{id}");
        Option<string> GetDefault() => Option.Some("Anonymous");

        var result = GetFromCache(42)
            .OrElse(GetFromDatabase(42))
            .OrElse(GetDefault());

        Assert.True(result.IsSome);
        Assert.Equal("User-42", result.GetValueOrThrow());
    }

    [Fact]
    public void OrElse_RealWorld_ConfigurationFallback()
    {
        Option<string> GetFromEnvironment(string key) => Option.None<string>();
        Option<string> GetFromConfigFile(string key) => Option.None<string>();
        Option<string> GetDefault(string key) => Option.Some("default-value");

        var config = GetFromEnvironment("API_KEY")
            .OrElse(() => GetFromConfigFile("API_KEY"))
            .OrElse(() => GetDefault("API_KEY"));

        Assert.True(config.IsSome);
        Assert.Equal("default-value", config.GetValueOrThrow());
    }

    [Fact]
    public void OrElse_VsGetValueOr_StaysInOptionContext()
    {
        var option = Option.None<int>();

        // OrElse stays in Option context - can continue chaining
        var result1 = option
            .OrElse(Option.Some(42))
            .Map(x => x * 2);

        Assert.True(result1.IsSome);
        Assert.Equal(84, result1.GetValueOrThrow());

        // GetValueOr unwraps to plain value
        var result2 = option.GetValueOr(42) * 2;

        Assert.Equal(84, result2);
    }
}
