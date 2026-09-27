/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using BenchmarkDotNet.Attributes;
using static Corsinvest.Fx.Defer.Defer;

namespace Corsinvest.Fx.Benchmarks;

/// <summary>
/// What <c>defer</c> costs against the <c>try/finally</c> it replaces.
/// </summary>
/// <remarks>
/// The honest comparison is the first two: <c>defer</c> is not free, and in a genuinely hot loop
/// <c>finally</c> is the right answer. What the 24 bytes buy is the cleanup sitting next to the
/// thing it undoes rather than pages below it. The state-passing overload removes the capture, not
/// the object itself, so it lands between the two.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(AllocationOnlyConfig))]
public class DeferBenchmarks
{
    // Static, so the state-passing benchmark can hand over just the value being restored. Capturing
    // `this` as well would put a second field in the state object and add 8 bytes to the figure,
    // measuring the tuple rather than the overload.
    private static int _sink;

    [Benchmark(Baseline = true)]
    public void TryFinally()
    {
        var previous = _sink;
        try { _sink = 1; }
        finally { _sink = previous; }
    }

    /// <summary>The lambda captures <c>previous</c>: display class plus delegate, plus the object.</summary>
    [Benchmark]
    public void Defer_Capturing()
    {
        var previous = _sink;
        _sink = 1;
        using var _ = defer(() => _sink = previous);
    }

    /// <summary>The state is passed in, so the lambda is <c>static</c> and only the object remains.</summary>
    [Benchmark]
    public void Defer_StatePassing()
    {
        var previous = _sink;
        _sink = 1;
        using var _ = defer(previous, static value => _sink = value);
    }

    /// <summary>A method group allocates a delegate but no display class.</summary>
    [Benchmark]
    public void Defer_MethodGroup() => defer(Reset).Dispose();

    private static void Reset() => _sink = 0;
}
