namespace Corsinvest.Fx.Functional.Tests;

public class PipeExtensionsTests
{
    // ============================================
    // Synchronous Pipe - Basic
    // ============================================

    [Fact]
    public void Pipe_WithSingleFunction_TransformsValue()
    {
        var result = 5.Pipe(x => x * 2);

        Assert.Equal(10, result);
    }

    [Fact]
    public void Pipe_ChainedTransformations_AppliesInOrder()
    {
        var result = 5
            .Pipe(x => x * 2)      // 10
            .Pipe(x => x + 3)      // 13
            .Pipe(x => x.ToString());

        Assert.Equal("13", result);
    }

    [Fact]
    public void Pipe_WithStringTransformation_Works()
    {
        var result = "hello"
            .Pipe(s => s.ToUpper())
            .Pipe(s => s + " WORLD");

        Assert.Equal("HELLO WORLD", result);
    }

    // ============================================
    // Pipe with Multiple Arguments
    // ============================================

    private static double Power(double x, int exponent) => Math.Pow(x, exponent);
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    private static string Substring(string text, int start, int length) => text.Substring(start, length);

    [Fact]
    public void Pipe_WithOneExtraArgument_PassesArgumentCorrectly()
    {
        var result = 5.0.Pipe(Power, 2);

        Assert.Equal(25.0, result);
    }

    [Fact]
    public void Pipe_WithTwoExtraArguments_PassesArgumentsCorrectly()
    {
        var result = 35.0.Pipe(x => Clamp(x, 0, 30));

        Assert.Equal(30.0, result);
    }

    [Fact]
    public void Pipe_WithTwoExtraArguments_String_PassesArgumentsCorrectly()
    {
        var result = "Hello World".Pipe(Substring, 0, 5);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Pipe_ComplexChain_MatchesExample()
    {
        // The example from documentation: 5^2 = 25, +10 = 35, Clamp(35, 0, 30) = 30
        var result = 5.0
            .Pipe(Power, 2)                  // 25
            .Pipe(x => x + 10)               // 35
            .Pipe(x => Clamp(x, 0, 30));     // 30

        Assert.Equal(30.0, result);
    }

    // ============================================
    // Asynchronous Pipe
    // ============================================

    private static async Task<int> DoubleAsync(int x)
    {
        await Task.Delay(1);
        return x * 2;
    }

    private static async Task<int> AddAsync(int x, int y)
    {
        await Task.Delay(1);
        return x + y;
    }

    [Fact]
    public async Task PipeAsync_WithAsyncFunction_TransformsValue()
    {
        var result = await 5.PipeAsync(DoubleAsync);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task PipeAsync_ChainedTransformations_AppliesInOrder()
    {
        var result = await 5
            .PipeAsync(DoubleAsync)         // 10
            .PipeAsync(DoubleAsync);        // 20

        Assert.Equal(20, result);
    }

    [Fact]
    public async Task PipeAsync_WithTask_Works()
    {
        var result = await Task.FromResult(5)
            .PipeAsync(DoubleAsync);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task Pipe_OnTask_WithSyncFunction_Works()
    {
        var result = await Task.FromResult(5)
            .Pipe(x => x * 2);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task PipeAsync_WithExtraArgument_Works()
    {
        var result = await 5.PipeAsync(AddAsync, 10);

        Assert.Equal(15, result);
    }

    [Fact]
    public async Task PipeAsync_MixingSyncAndAsync_Works()
    {
        var result = await 5
            .Pipe(x => x * 2)              // Sync: 10
            .PipeAsync(DoubleAsync)        // Async: 20
            .Pipe(x => x + 5);             // Sync: 25

        Assert.Equal(25, result);
    }

    // ============================================
    // Tap - Side Effects
    // ============================================

    [Fact]
    public void Tap_ExecutesSideEffect_ReturnsOriginalValue()
    {
        int sideEffectValue = 0;

        var result = 42.Tap(x => sideEffectValue = x * 2);

        Assert.Equal(42, result);          // Original value returned
        Assert.Equal(84, sideEffectValue);  // Side effect executed
    }

    [Fact]
    public void Tap_InChain_DoesNotModifyValue()
    {
        var log = new List<int>();

        var result = 5
            .Pipe(x => x * 2)                      // 10
            .Tap(x => log.Add(x))              // Log 10, return 10
            .Pipe(x => x + 5)                      // 15
            .Tap(x => log.Add(x))              // Log 15, return 15
            .Pipe(x => x * 2);                     // 30

        Assert.Equal(30, result);
        Assert.Equal(new[] { 10, 15 }, log);
    }

    [Fact]
    public async Task TapAsync_ExecutesSideEffect_ReturnsOriginalValue()
    {
        int sideEffectValue = 0;

        var result = await 42.TapAsync(async x =>
        {
            await Task.Delay(1);
            sideEffectValue = x * 2;
        });

        Assert.Equal(42, result);
        Assert.Equal(84, sideEffectValue);
    }

    [Fact]
    public async Task TapAsync_OnTask_ExecutesSideEffect()
    {
        var log = new List<int>();

        var task = Task.FromResult(5);
        var result = await task
            .Tap(x => log.Add(x))              // Sync tap
            .TapAsync(async x =>               // Async tap
            {
                await Task.Delay(1);
                log.Add(x * 2);
            });

        Assert.Equal(5, result);
        Assert.Equal(new[] { 5, 10 }, log);
    }

    // ============================================
    // TapIf - Conditional Side Effects
    // ============================================

    [Fact]
    public void TapIf_WhenTrue_ExecutesSideEffect()
    {
        int sideEffectValue = 0;

        var result = 42.TapIf(true, x => sideEffectValue = x * 2);

        Assert.Equal(42, result);          // Original value returned
        Assert.Equal(84, sideEffectValue);  // Side effect executed
    }

    [Fact]
    public void TapIf_WhenFalse_DoesNotExecuteSideEffect()
    {
        int sideEffectValue = 0;

        var result = 42.TapIf(false, x => sideEffectValue = x * 2);

        Assert.Equal(42, result);         // Original value returned
        Assert.Equal(0, sideEffectValue);  // Side effect NOT executed
    }

    [Fact]
    public void TapIf_InChain_OnlyExecutesWhenConditionMet()
    {
        var log = new List<string>();

        var result = 5
            .Pipe(x => x * 2)                                    // 10
            .TapIf(true, x => log.Add($"First: {x}"))           // Executes
            .Pipe(x => x + 5)                                    // 15
            .TapIf(false, x => log.Add($"Second: {x}"))         // Does NOT execute
            .Pipe(x => x * 2);                                   // 30

        Assert.Equal(30, result);
        Assert.Single(log);
        Assert.Equal("First: 10", log[0]);
    }

    [Fact]
    public async Task TapIfAsync_WhenTrue_ExecutesSideEffect()
    {
        int sideEffectValue = 0;

        var result = await 42.TapIfAsync(true, async x =>
        {
            await Task.Delay(1);
            sideEffectValue = x * 2;
        });

        Assert.Equal(42, result);
        Assert.Equal(84, sideEffectValue);
    }

    [Fact]
    public async Task TapIfAsync_WhenFalse_DoesNotExecuteSideEffect()
    {
        int sideEffectValue = 0;

        var result = await 42.TapIfAsync(false, async x =>
        {
            await Task.Delay(1);
            sideEffectValue = x * 2;
        });

        Assert.Equal(42, result);
        Assert.Equal(0, sideEffectValue);
    }

    [Fact]
    public async Task TapIfAsync_OnTask_WhenTrue_ExecutesSideEffect()
    {
        var log = new List<int>();
        var task = Task.FromResult(5);

        var result = await task.TapIfAsync(true, async x =>
        {
            await Task.Delay(1);
            log.Add(x * 2);
        });

        Assert.Equal(5, result);
        Assert.Single(log);
        Assert.Equal(10, log[0]);
    }

    [Fact]
    public async Task TapIfAsync_OnTask_WhenFalse_DoesNotExecuteSideEffect()
    {
        var log = new List<int>();
        var task = Task.FromResult(5);

        var result = await task.TapIfAsync(false, async x =>
        {
            await Task.Delay(1);
            log.Add(x * 2);
        });

        Assert.Equal(5, result);
        Assert.Empty(log);
    }

    // ============================================
    // Conditional Pipes - PipeIf
    // ============================================

    [Fact]
    public void PipeIf_WhenTrue_AppliesTransformation()
    {
        var result = 10.PipeIf(true, x => x * 2);

        Assert.Equal(20, result);
    }

    [Fact]
    public void PipeIf_WhenFalse_ReturnsOriginalValue()
    {
        var result = 10.PipeIf(false, x => x * 2);

        Assert.Equal(10, result);
    }

    [Fact]
    public void PipeIf_WithPredicate_WhenTrue_AppliesTransformation()
    {
        var result = 10.PipeIf(x => x > 5, x => x * 2);

        Assert.Equal(20, result);
    }

    [Fact]
    public void PipeIf_WithPredicate_WhenFalse_ReturnsOriginalValue()
    {
        var result = 10.PipeIf(x => x > 20, x => x * 2);

        Assert.Equal(10, result);
    }

    [Fact]
    public void PipeIf_InChain_OnlyAppliesWhenConditionMet()
    {
        var result = 5
            .Pipe(x => x * 2)                      // 10
            .PipeIf(x => x > 8, x => x + 10)      // 20 (condition met)
            .PipeIf(x => x > 50, x => x + 100);   // 20 (condition not met)

        Assert.Equal(20, result);
    }

    [Fact]
    public void PipeIf_StringExample_Truncates()
    {
        var longText = "Hello World";
        var shortText = "Hi";

        var truncated = longText.PipeIf(s => s.Length > 10, s => s[..10]);
        var notTruncated = shortText.PipeIf(s => s.Length > 10, s => s[..10]);

        Assert.Equal("Hello Worl", truncated);
        Assert.Equal("Hi", notTruncated);
    }

    // ============================================
    // Conditional Pipes - PipeEither
    // ============================================

    [Fact]
    public void PipeEither_WhenTrue_AppliesFirstTransformation()
    {
        var result = 10.PipeEither(true, x => x * 2, x => x * 3);

        Assert.Equal(20, result);
    }

    [Fact]
    public void PipeEither_WhenFalse_AppliesSecondTransformation()
    {
        var result = 10.PipeEither(false, x => x * 2, x => x * 3);

        Assert.Equal(30, result);
    }

    [Fact]
    public void PipeEither_WithDifferentTypes_Works()
    {
        var positiveResult = 10.PipeEither(true,
            x => $"Positive: {x}",
            x => $"Negative: {x}");

        var negativeResult = 10.PipeEither(false,
            x => $"Positive: {x}",
            x => $"Negative: {x}");

        Assert.Equal("Positive: 10", positiveResult);
        Assert.Equal("Negative: 10", negativeResult);
    }

    // ============================================
    // Async Conditional Pipes - PipeIfAsync
    // ============================================

    [Fact]
    public async Task PipeIfAsync_WhenTrue_AppliesAsyncTransformation()
    {
        var result = await 10.PipeIfAsync(true, async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(20, result);
    }

    [Fact]
    public async Task PipeIfAsync_WhenFalse_ReturnsOriginalValue()
    {
        var result = await 10.PipeIfAsync(false, async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task PipeIfAsync_WithPredicate_WhenTrue_AppliesTransformation()
    {
        var result = await 10.PipeIfAsync(x => x > 5, async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(20, result);
    }

    [Fact]
    public async Task PipeIfAsync_WithPredicate_WhenFalse_ReturnsOriginalValue()
    {
        var result = await 10.PipeIfAsync(x => x > 20, async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task PipeIfAsync_OnTask_WhenTrue_AppliesTransformation()
    {
        var task = Task.FromResult(10);

        var result = await task.PipeIfAsync(true, async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(20, result);
    }

    [Fact]
    public async Task PipeIfAsync_OnTask_WhenFalse_ReturnsOriginalValue()
    {
        var task = Task.FromResult(10);

        var result = await task.PipeIfAsync(false, async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task PipeIfAsync_InChain_OnlyAppliesWhenConditionMet()
    {
        var result = await 5
            .PipeAsync(async x =>
            {
                await Task.Delay(1);
                return x * 2;
            })
            .PipeIfAsync(x => x > 8, async x =>
            {
                await Task.Delay(1);
                return x + 10;
            })
            .PipeIfAsync(x => x > 50, async x =>
            {
                await Task.Delay(1);
                return x + 100;
            });

        Assert.Equal(20, result);
    }

    // ============================================
    // Async Conditional Pipes - PipeEitherAsync
    // ============================================

    [Fact]
    public async Task PipeEitherAsync_WhenTrue_AppliesFirstAsyncTransformation()
    {
        var result = await 10.PipeEitherAsync(true,
            async x =>
            {
                await Task.Delay(1);
                return x * 2;
            },
            async x =>
            {
                await Task.Delay(1);
                return x * 3;
            });

        Assert.Equal(20, result);
    }

    [Fact]
    public async Task PipeEitherAsync_WhenFalse_AppliesSecondAsyncTransformation()
    {
        var result = await 10.PipeEitherAsync(false,
            async x =>
            {
                await Task.Delay(1);
                return x * 2;
            },
            async x =>
            {
                await Task.Delay(1);
                return x * 3;
            });

        Assert.Equal(30, result);
    }

    [Fact]
    public async Task PipeEitherAsync_OnTask_WhenTrue_AppliesFirstTransformation()
    {
        var task = Task.FromResult(10);

        var result = await task.PipeEitherAsync(true,
            async x =>
            {
                await Task.Delay(1);
                return x * 2;
            },
            async x =>
            {
                await Task.Delay(1);
                return x * 3;
            });

        Assert.Equal(20, result);
    }

    [Fact]
    public async Task PipeEitherAsync_OnTask_WhenFalse_AppliesSecondTransformation()
    {
        var task = Task.FromResult(10);

        var result = await task.PipeEitherAsync(false,
            async x =>
            {
                await Task.Delay(1);
                return x * 2;
            },
            async x =>
            {
                await Task.Delay(1);
                return x * 3;
            });

        Assert.Equal(30, result);
    }

    [Fact]
    public async Task PipeEitherAsync_OnTask_WithPredicate_WhenTrue_AppliesFirstTransformation()
    {
        var task = Task.FromResult(10);

        var result = await task.PipeEitherAsync(
            x => x > 5,
            async x =>
            {
                await Task.Delay(1);
                return $"Greater: {x}";
            },
            async x =>
            {
                await Task.Delay(1);
                return $"Less or equal: {x}";
            });

        Assert.Equal("Greater: 10", result);
    }

    [Fact]
    public async Task PipeEitherAsync_OnTask_WithPredicate_WhenFalse_AppliesSecondTransformation()
    {
        var task = Task.FromResult(3);

        var result = await task.PipeEitherAsync(
            x => x > 5,
            async x =>
            {
                await Task.Delay(1);
                return $"Greater: {x}";
            },
            async x =>
            {
                await Task.Delay(1);
                return $"Less or equal: {x}";
            });

        Assert.Equal("Less or equal: 3", result);
    }

    [Fact]
    public async Task PipeEitherAsync_WithDifferentTypes_Works()
    {
        var positiveResult = await 10.PipeEitherAsync(true,
            async x =>
            {
                await Task.Delay(1);
                return $"Positive: {x}";
            },
            async x =>
            {
                await Task.Delay(1);
                return $"Negative: {x}";
            });

        var negativeResult = await 10.PipeEitherAsync(false,
            async x =>
            {
                await Task.Delay(1);
                return $"Positive: {x}";
            },
            async x =>
            {
                await Task.Delay(1);
                return $"Negative: {x}";
            });

        Assert.Equal("Positive: 10", positiveResult);
        Assert.Equal("Negative: 10", negativeResult);
    }

    [Fact]
    public async Task PipeEitherAsync_InChain_Works()
    {
        var result = await 5
            .PipeAsync(async x =>
            {
                await Task.Delay(1);
                return x * 2;
            })
            .PipeEitherAsync(
                x => x > 8,
                async x =>
                {
                    await Task.Delay(1);
                    return x + 10;
                },
                async x =>
                {
                    await Task.Delay(1);
                    return x - 10;
                });

        Assert.Equal(20, result);
    }

    // ============================================
    // Real-World Examples
    // ============================================

    [Fact]
    public void Pipe_StringProcessing_CompleteExample()
    {
        var result = "  hello world  "
            .Pipe(s => s.Trim())
            .Pipe(s => s.ToUpper())
            .Pipe(s => s.Replace(" ", "-"))
            .Pipe(s => s[..10]);

        Assert.Equal("HELLO-WORL", result);
    }

    [Fact]
    public void Pipe_MathCalculations_CompleteExample()
    {
        var result = 3.0
            .Pipe(Power, 2)                  // 9
            .Pipe(x => x + 5)                // 14
            .Pipe(x => x * 2)                // 28
            .Pipe(x => Clamp(x, 0, 25));     // 25

        Assert.Equal(25.0, result);
    }

    [Fact]
    public async Task PipeAsync_CompleteExample_Works()
    {
        var log = new List<string>();

        var result = await 5.PipeAsync(async x =>
        {
            var doubled = await DoubleAsync(x);
            await Task.Delay(1);
            log.Add($"After first double: {doubled}");
            var quadrupled = await DoubleAsync(doubled);
            log.Add($"Final: {quadrupled}");
            return quadrupled;
        });

        Assert.Equal(20, result);
        Assert.Equal(2, log.Count);
        Assert.Contains("After first double: 10", log);
        Assert.Contains("Final: 20", log);
    }

    [Fact]
    public void Pipe_WithConditionals_ConfigurationExample()
    {
        bool shouldNormalize = true;
        bool shouldEnrich = false;

        var data = "test-data"
            .Pipe(s => s.ToUpper())
            .PipeIf(shouldNormalize, s => s.Replace("-", "_"))
            .PipeIf(shouldEnrich, s => s + "_ENRICHED");

        Assert.Equal("TEST_DATA", data);
    }

    // ============================================
    // Edge Cases
    // ============================================

    [Fact]
    public void Pipe_WithNullableTypes_Works()
    {
        int? nullableValue = 42;

        var result = nullableValue.Pipe(x => x * 2);

        Assert.Equal(84, result);
    }

    [Fact]
    public void Pipe_WithComplexTypes_Works()
    {
        var person = new Person("John", 30);

        var result = person
            .Pipe(p => new Person(p.Name, p.Age + 1))
            .Pipe(p => new Person(p.Name.ToUpper(), p.Age));

        Assert.Equal("JOHN", result.Name);
        Assert.Equal(31, result.Age);
    }

    [Fact]
    public void Tap_DoesNotThrowOnException_ButPropagates()
    {
        Assert.Throws<InvalidOperationException>(() =>
            5.Tap(x => throw new InvalidOperationException("Test"))
        );
    }

    [Fact]
    public void Pipe_WithZeroValue_Works()
    {
        var result = 0
            .Pipe(x => x + 10)
            .Pipe(x => x * 2);

        Assert.Equal(20, result);
    }

    [Fact]
    public void Pipe_WithNegativeNumbers_Works()
    {
        var step1 = -5.Pipe(x => x * 2);      // -10
        var step2 = step1.Pipe(Math.Abs);     // 10

        Assert.Equal(-10, step1);
        Assert.Equal(10, step2);
    }

    // ============================================
    // Helper Types
    // ============================================

    private record Person(string Name, int Age);
}
