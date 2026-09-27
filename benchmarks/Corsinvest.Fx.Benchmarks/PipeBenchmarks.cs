/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using BenchmarkDotNet.Attributes;
using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Benchmarks;

/// <summary>
/// What the <c>Pipe</c> extensions cost against writing the same calls directly.
/// </summary>
/// <remarks>
/// These are thin wrappers around a delegate call, so the baseline is the direct call itself.
/// Nothing here allocates as long as the handlers do not capture: what the benchmarks protect
/// against is an implementation that grows a closure or an intermediate object.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(AllocationOnlyConfig))]
public class PipeBenchmarks
{
    private const int Value = 5;

    [Benchmark(Baseline = true)]
    public int Direct_Call() => Double(Value);

    [Benchmark]
    public int Pipe() => Value.Pipe(static x => Double(x));

    [Benchmark]
    public int Pipe_WithArgument() => Value.Pipe(static (x, factor) => x * factor, 2);

    [Benchmark]
    public int Tap() => Value.Tap(static _ => { });

    [Benchmark]
    public int TapIf_True() => Value.TapIf(true, static _ => { });

    [Benchmark]
    public int TapIf_False() => Value.TapIf(false, static _ => { });

    [Benchmark]
    public int PipeIf_Condition() => Value.PipeIf(true, static x => Double(x));

    [Benchmark]
    public int PipeIf_Predicate() => Value.PipeIf(static x => x > 0, static x => Double(x));

    [Benchmark]
    public int PipeEither_Condition() => Value.PipeEither(true, static x => Double(x), static x => x);

    [Benchmark]
    public int PipeEither_Predicate()
        => Value.PipeEither(static x => x > 0, static x => Double(x), static x => x);

    /// <summary>A short chain, the shape the extensions exist for.</summary>
    [Benchmark]
    public int Chain()
        => Value.Pipe(static x => Double(x))
                .Tap(static _ => { })
                .PipeIf(static x => x > 5, static x => x + 1);

    private static int Double(int x) => x * 2;
}
