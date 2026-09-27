/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;

namespace Corsinvest.Fx.Benchmarks;

/// <summary>
/// Reports allocations only, with every timing column hidden.
/// </summary>
/// <remarks>
/// <para>
/// These operations run in single-digit nanoseconds, where a benchmark's measurement error exceeds
/// the value being measured: a run of this suite produced <c>0.0528 ns ± 0.6494</c> for a direct
/// method call, and a ratio column reading "57x" for an operation that allocates nothing. Those are
/// artifacts of the sampling, not findings, and publishing them would invite conclusions the data
/// does not support.
/// </para>
/// <para>
/// Allocation counts have no such problem - they come from
/// <c>GC.GetAllocatedBytesForCurrentThread</c> and are exact, not statistical estimates, and they
/// are what the package documentation quotes. So the timing columns are removed rather than left to
/// be misread. If you do want timings, drop this attribute from the class and run with the default
/// job rather than <c>--job short</c>; expect it to take considerably longer to reach a usable
/// confidence interval on operations this small.
/// </para>
/// </remarks>
public sealed class AllocationOnlyConfig : ManualConfig
{
    public AllocationOnlyConfig()
    {
        // HideColumns rather than a restricted AddColumnProvider: the providers are additive, so
        // leaving Statistics out of the list does not remove it - BenchmarkDotNet still applies its
        // defaults on top, and the only visible effect is that Ratio loses its baseline and prints
        // "?". Naming the columns removes them.
        HideColumns(
            Column.Mean,
            Column.Error,
            Column.StdDev,
            Column.StdErr,
            Column.Median,
            Column.Min,
            Column.Max,
            Column.Q1,
            Column.Q3,
            Column.Ratio,
            Column.RatioSD,
            Column.AllocRatio);
    }
}
