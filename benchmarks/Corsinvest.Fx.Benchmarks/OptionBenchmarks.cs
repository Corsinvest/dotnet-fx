/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using BenchmarkDotNet.Attributes;
using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Benchmarks;

/// <summary>
/// What an <see cref="Option{T}"/> costs to build and to move through a chain.
/// </summary>
/// <remarks>
/// The numbers that matter here are the allocations. A <c>Some</c> is two objects - the
/// <c>Some&lt;T&gt;</c> payload and the wrapper that closes the hierarchy - while a <c>None</c>
/// carries no data and is handed out from a shared instance, so it allocates nothing at all. That
/// makes the failing path of a chain free, however long the chain is.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(AllocationOnlyConfig))]
public class OptionBenchmarks
{
    private static readonly Option<int> Some = Option.Some(5);
    private static readonly Option<int> None = Option.None<int>();

    // ---- construction --------------------------------------------------

    [Benchmark(Baseline = true), BenchmarkCategory("Construction")]
    public Option<int> Some_Create() => Option.Some(5);

    /// <summary>The shared instance: no allocation, whatever <c>T</c> is.</summary>
    [Benchmark, BenchmarkCategory("Construction")]
    public Option<int> None_Create() => Option.None<int>();

    [Benchmark, BenchmarkCategory("Construction")]
    public Option<string> FromNullable_Null() => Option.FromNullable<string>(null);

    [Benchmark, BenchmarkCategory("Construction")]
    public Option<string> FromNullable_Value() => Option.FromNullable("value");

    // ---- chaining ------------------------------------------------------

    [Benchmark, BenchmarkCategory("Chain")]
    public Option<int> Some_Map() => Some.Map(static x => x * 2);

    /// <summary>Mapping a None allocates nothing: the shared instance is returned as is.</summary>
    [Benchmark, BenchmarkCategory("Chain")]
    public Option<int> None_Map() => None.Map(static x => x * 2);

    [Benchmark, BenchmarkCategory("Chain")]
    public Option<int> None_Map_Map_Map()
        => None.Map(static x => x * 2).Map(static x => x + 1).Map(static x => x - 1);

    [Benchmark, BenchmarkCategory("Chain")]
    public Option<int> Some_Filter_Fails() => Some.Filter(static x => x > 10);

    [Benchmark, BenchmarkCategory("Chain")]
    public Option<int> Some_Filter_Passes() => Some.Filter(static x => x > 1);

    [Benchmark, BenchmarkCategory("Chain")]
    public Option<int> Some_Bind() => Some.Bind(static x => Option.Some(x * 2));

    // ---- unwrapping ----------------------------------------------------

    [Benchmark, BenchmarkCategory("Unwrap")]
    public int Some_GetValueOr() => Some.GetValueOr(0);

    [Benchmark, BenchmarkCategory("Unwrap")]
    public int None_GetValueOr() => None.GetValueOr(0);

    [Benchmark, BenchmarkCategory("Unwrap")]
    public bool Some_TryGetValue() => Some.TryGetValue(out _);

    /// <summary>
    /// Matching does not allocate by itself - the cost, when there is one, comes from a handler
    /// that captures. Both handlers here are <c>static</c>.
    /// </summary>
    [Benchmark, BenchmarkCategory("Unwrap")]
    public int Some_Match() => Some.Match(static s => s.Value, static _ => 0);

    [Benchmark, BenchmarkCategory("Unwrap")]
    public int None_Match() => None.Match(static s => s.Value, static _ => 0);

    [Benchmark, BenchmarkCategory("Unwrap")]
    public string Some_Switch() => Some switch
    {
        Option<int>.Some => "some",
        Option<int>.None => "none"
    };

    // ---- a realistic lookup --------------------------------------------

    private static readonly Dictionary<int, string> Lookup = new() { [1] = "one" };

    [Benchmark, BenchmarkCategory("Lookup")]
    public Option<string> Lookup_Hit() => Find(1);

    /// <summary>The miss is the common case in real code, and it is free.</summary>
    [Benchmark, BenchmarkCategory("Lookup")]
    public Option<string> Lookup_Miss() => Find(999);

    private static Option<string> Find(int key)
        => Lookup.TryGetValue(key, out var value) ? Option.Some(value) : Option.None<string>();
}
