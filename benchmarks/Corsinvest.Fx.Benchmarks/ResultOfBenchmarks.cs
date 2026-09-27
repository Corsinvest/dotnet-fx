/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using BenchmarkDotNet.Attributes;
using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Benchmarks;

/// <summary>
/// What a <see cref="ResultOf{T, E}"/> costs, and what railway-oriented chaining costs on top.
/// </summary>
/// <remarks>
/// Unlike <c>Option</c>, neither case here is dataless - a <c>Fail</c> carries its error - so both
/// allocate their payload plus wrapper. The chain benchmarks show the cost is per step taken, not
/// per step written: once a chain has failed, the remaining steps pass the same instance along.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(AllocationOnlyConfig))]
public class ResultOfBenchmarks
{
    private static readonly ResultOf<int, string> Ok = ResultOf.Ok<int, string>(5);
    private static readonly ResultOf<int, string> Fail = ResultOf.Fail<int, string>("error");

    // ---- construction --------------------------------------------------

    [Benchmark(Baseline = true), BenchmarkCategory("Construction")]
    public ResultOf<int, string> Ok_Create() => ResultOf.Ok<int, string>(5);

    [Benchmark, BenchmarkCategory("Construction")]
    public ResultOf<int, string> Fail_Create() => ResultOf.Fail<int, string>("error");

    // ---- chaining ------------------------------------------------------

    [Benchmark, BenchmarkCategory("Chain")]
    public ResultOf<int, string> Ok_Map() => Ok.Map(static x => x * 2);

    [Benchmark, BenchmarkCategory("Chain")]
    public ResultOf<int, string> Fail_Map() => Fail.Map(static x => x * 2);

    [Benchmark, BenchmarkCategory("Chain")]
    public ResultOf<int, string> Ok_Bind() => Ok.Bind(static x => ResultOf.Ok<int, string>(x * 2));

    [Benchmark, BenchmarkCategory("Chain")]
    public ResultOf<int, string> Ok_Ensure_Passes() => Ok.Ensure(static x => x > 0, "negative");

    [Benchmark, BenchmarkCategory("Chain")]
    public ResultOf<int, string> Ok_Ensure_Fails() => Ok.Ensure(static x => x > 100, "too small");

    [Benchmark, BenchmarkCategory("Chain")]
    public ResultOf<int, string> Fail_MapError() => Fail.MapError(static e => e + "!");

    /// <summary>A four-step pipeline on the success path - the shape most code actually writes.</summary>
    [Benchmark, BenchmarkCategory("Chain")]
    public int Ok_FullPipeline()
        => Ok.Map(static x => x * 2)
             .Bind(static x => ResultOf.Ok<int, string>(x + 1))
             .Ensure(static x => x > 0, "negative")
             .GetValueOr(0);

    /// <summary>The same pipeline once it has already failed: the steps are traversed, not run.</summary>
    [Benchmark, BenchmarkCategory("Chain")]
    public int Fail_FullPipeline()
        => Fail.Map(static x => x * 2)
               .Bind(static x => ResultOf.Ok<int, string>(x + 1))
               .Ensure(static x => x > 0, "negative")
               .GetValueOr(0);

    // ---- unwrapping ----------------------------------------------------

    [Benchmark, BenchmarkCategory("Unwrap")]
    public int Ok_GetValueOr() => Ok.GetValueOr(0);

    [Benchmark, BenchmarkCategory("Unwrap")]
    public int Fail_GetValueOr() => Fail.GetValueOr(0);

    [Benchmark, BenchmarkCategory("Unwrap")]
    public bool Ok_TryGetValue() => Ok.TryGetValue(out _);

    [Benchmark, BenchmarkCategory("Unwrap")]
    public int Fail_Recover() => Fail.Recover(static _ => 0);

    [Benchmark, BenchmarkCategory("Unwrap")]
    public Option<int> Ok_ToOption() => Ok.ToOption();

    [Benchmark, BenchmarkCategory("Unwrap")]
    public Option<int> Fail_ToOption() => Fail.ToOption();

    // ---- try and combine -----------------------------------------------

    [Benchmark, BenchmarkCategory("Try")]
    public ResultOf<int, Exception> Try_Func() => ResultOf.Try(static () => 5);

    /// <summary>
    /// The <c>Action</c> overload is written out rather than wrapping the <c>Func</c> one through
    /// a lambda, which would capture the action and cost a display class plus a delegate per call.
    /// </summary>
    [Benchmark, BenchmarkCategory("Try")]
    public ResultOf<Unit, Exception> Try_Action() => ResultOf.Try(static () => { });

    /// <summary>The error list is only allocated once something has failed.</summary>
    [Benchmark, BenchmarkCategory("Combine")]
    public ResultOf<(int, int), List<string>> Combine_AllOk() => ResultOf.Combine(Ok, Ok);

    [Benchmark, BenchmarkCategory("Combine")]
    public ResultOf<(int, int), List<string>> Combine_WithFailure() => ResultOf.Combine(Fail, Ok);

    // ---- LINQ ----------------------------------------------------------

    [Benchmark, BenchmarkCategory("Linq")]
    public ResultOf<int, string> Linq_Select() => from x in Ok select x * 2;

    [Benchmark, BenchmarkCategory("Linq")]
    public ResultOf<int, string> Linq_SelectMany() => from x in Ok from y in Ok select x + y;
}
