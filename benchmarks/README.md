# Benchmarks

Allocation measurements for the figures quoted in the package documentation, so a claim in a README
can be checked rather than taken on faith.

## Running them

```bash
# everything (about 20 minutes - each benchmark is compiled and run in its own process)
dotnet run -c Release --project benchmarks/Corsinvest.Fx.Benchmarks -- --filter '*' --job short

# one suite (several can be listed, space-separated - a '|' between patterns does not work)
dotnet run -c Release --project benchmarks/Corsinvest.Fx.Benchmarks -- --filter '*OptionBenchmarks*' --job short

# a smoke test that every benchmark still runs (one iteration each, ~30s, figures unusable)
dotnet run -c Release --project benchmarks/Corsinvest.Fx.Benchmarks -- --filter '*' --job dry

# no filter opens an interactive picker
dotnet run -c Release --project benchmarks/Corsinvest.Fx.Benchmarks
```

**Release only.** BenchmarkDotNet refuses a Debug build, and rightly so: with the JIT's
optimizations off the allocations themselves change. `Option.Some(42)` reports 72 B under Debug
against 48 B under Release, which would make a regression test on the figure meaningless.

## What each suite covers

| Suite | Question it answers |
| --- | --- |
| `OptionBenchmarks` | What a `Some` and a `None` cost to build, and what a chain costs on each path |
| `ResultOfBenchmarks` | The same for `ResultOf`, plus `Try`, `Combine` and LINQ query syntax |
| `UnionMatchBenchmarks` | What matching costs, and what the state-passing `Match` overloads are for |
| `DeferBenchmarks` | `defer` against the `try/finally` it replaces |
| `PipeBenchmarks` | Whether the `Pipe` extensions cost anything over calling the delegate directly |

## Why there are no timings

These suites report **allocations only** - `AllocationOnlyConfig` hides Mean, Error, StdDev, Median
and Ratio.

The operations measured here run in single-digit nanoseconds, and at that scale a short benchmark
job's measurement error exceeds the value being measured. A run of this suite produced
`0.0528 ns ± 0.6494` for a plain method call, and a Ratio column reading `57x` for an operation that
allocates nothing at all. Those figures are sampling artifacts, not findings, and publishing them
would invite conclusions the data cannot support.

Allocation counts have no such problem: they come from `GC.GetAllocatedBytesForCurrentThread` and
are exact. They are also the figures the package documentation quotes, which is what these
benchmarks exist to back.

If you do want timings for a specific question, drop `[Config(typeof(AllocationOnlyConfig))]` from
the class and run with the default job rather than `--job short`. Expect it to take considerably
longer to reach a usable confidence interval on operations this small.

## Reading the results

The **Allocated** column is the one that matters. Two figures recur:

- **48 B** is one `Some`/`Ok`/`Fail`: the payload record plus the wrapper that closes the
  hierarchy. It is the floor for any case that carries data.
- **0 B** appears wherever a dataless case is involved - `Option.None<T>()` and any chain step that
  passes one along - because the generator hands out a shared instance instead of allocating.

A handler that captures is the other source of allocation: the compiler turns it into a display
class plus one delegate per handler, on every call. `UnionMatchBenchmarks` measures exactly that,
and `Match_Capturing` against `Match_StatePassing` is the comparison the state-passing overloads
exist for.

## Adding a benchmark

Put it next to the claim it backs. If a README or an XML doc comment quotes a figure, there should
be a benchmark here that produces it - that is the point of the project. Mark the method
`[Benchmark]`, keep the handlers `static` unless the benchmark is *about* capturing, and read a
value out so the JIT cannot eliminate the work.
