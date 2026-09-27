/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using System.Globalization;
using System.Diagnostics;
using static Corsinvest.Fx.Defer.Defer;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 09: Defer - cleanup that is not a Dispose()
///
/// When a resource implements IDisposable, plain `using` is shorter, faster and safer - use that.
/// What `using` cannot express is an arbitrary action at scope exit, which is what defer is for:
/// - Restoring a value you changed
/// - Balancing a counter across every exit path
/// - Closing a Begin/End pair on an API that never implemented IDisposable
/// - Side effects at scope exit (temp files, metrics)
/// - Async cleanup, where `await using` is enforced by the compiler
/// </summary>
public static class DeferAsync
{
    public static async Task Run()
    {
        Console.WriteLine("\n═══ Example 09: Defer (Scope-Exit Cleanup) ═══\n");

        Example1_RestoringState();
        Example2_BalancingACounter();
        Example3_BeginEndPair();
        Example4_TempFileAndMetrics();
        Example5_LifoOrder();
        await Example6_AsyncCleanup();
        Example7_StatePassingForm();
    }

    // Example 1: restoring a value - the canonical case `using` cannot express, because there is
    // no object to dispose, only a value to put back.
    private static void Example1_RestoringState()
    {
        Console.WriteLine("1️⃣  Restoring state\n");

        Console.WriteLine($"   → Culture before: {Describe(CultureInfo.CurrentCulture)}");

        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            using var _ = defer(() => CultureInfo.CurrentCulture = previous);

            Console.WriteLine($"   → Inside scope:   {CultureInfo.CurrentCulture.Name}");
            Console.WriteLine($"   → 1234.5 formats as {1234.5:N2}");
        }

        Console.WriteLine($"   → Culture after:  {Describe(CultureInfo.CurrentCulture)}");
        Console.WriteLine();

        static string Describe(CultureInfo c) => string.IsNullOrEmpty(c.Name) ? "(invariant)" : c.Name;
    }

    private static int _depth;

    // Example 2: the decrement sits next to the increment, so an early return added later cannot
    // skip it. With try/finally this lives pages below; with defer it is one line.
    private static void Example2_BalancingACounter()
    {
        Console.WriteLine("2️⃣  Balancing a counter\n");

        Walk("root", 0);
        Console.WriteLine($"   → Depth back to {_depth} after the walk");
        Console.WriteLine();

        static void Walk(string node, int childCount)
        {
            _depth++;
            using var _ = defer(() => _depth--);

            Console.WriteLine($"   {new string(' ', _depth * 2)}→ {node} (depth {_depth})");

            if (_depth >= 3) { return; }   // early exit - still decremented

            for (var i = 0; i < 2; i++) { Walk($"{node}.{i}", childCount); }
        }
    }

    // Example 3: paired calls on an API that never implemented IDisposable - common in interop,
    // graphics and older libraries.
    private static void Example3_BeginEndPair()
    {
        Console.WriteLine("3️⃣  Begin/End pair\n");

        var api = new LegacyBatchApi();

        {
            api.BeginBatch();
            using var _ = defer(() => api.EndBatch());

            api.Add("first");
            api.Add("second");
        }

        Console.WriteLine();
    }

    // Example 4: side effects at scope exit - nothing is being released at all.
    private static void Example4_TempFileAndMetrics()
    {
        Console.WriteLine("4️⃣  Temp file and elapsed time\n");

        {
            var tempPath = Path.GetTempFileName();
            using var _1 = defer(() =>
            {
                File.Delete(tempPath);
                Console.WriteLine($"   → Temp file deleted: {Path.GetFileName(tempPath)}");
            });

            var timer = Stopwatch.StartNew();
            using var _2 = defer(() => Console.WriteLine($"   → Scope took {timer.ElapsedMilliseconds} ms"));

            File.WriteAllText(tempPath, "work in progress");
            Console.WriteLine($"   → Wrote {new FileInfo(tempPath).Length} bytes");
            Thread.Sleep(15);
        }   // both defers run here, in reverse order

        Console.WriteLine();
    }

    // Example 5: defers run last-registered-first, like nested using blocks.
    private static void Example5_LifoOrder()
    {
        Console.WriteLine("5️⃣  LIFO order\n");

        {
            using var _a = defer(() => Console.WriteLine("   → Released A (registered first, released last)"));
            using var _b = defer(() => Console.WriteLine("   → Released B"));
            using var _c = defer(() => Console.WriteLine("   → Released C (registered last, released first)"));

            Console.WriteLine("   → Body runs first");
        }

        Console.WriteLine();
    }

    // Example 6: the async overload returns IAsyncDisposable, so `await using` is the only way to
    // consume it - plain `using` is a compile error (CS8418). The cleanup is awaited, never
    // blocked on and never fired and forgotten.
    private static async Task Example6_AsyncCleanup()
    {
        Console.WriteLine("6️⃣  Async cleanup (await using enforced)\n");

        var api = new LegacyBatchApi();

        await using (defer(async () =>
        {
            await Task.Delay(10);
            Console.WriteLine("   → EndBatchAsync completed (awaited, not blocked on)");
        }))
        {
            api.BeginBatch();
            Console.WriteLine("   → Working inside the batch...");
            await Task.Delay(10);
        }

        // using var _ = defer(async () => ...);   // error CS8418 - by design

        Console.WriteLine();
    }

    private static int _sink;

    // Example 7: passing the state explicitly lets the lambda be static, so no display class and
    // no per-call delegate are allocated. Use it where a measurement says it matters.
    private static void Example7_StatePassingForm()
    {
        Console.WriteLine("7️⃣  State-passing form (fewer allocations)\n");

        _sink = 10;
        Console.WriteLine($"   → Before: {_sink}");

        {
            var previous = _sink;
            _sink = 99;

            // `static` here is the point: the lambda cannot capture, so the value has to be passed.
            using var _ = defer(previous, static value => _sink = value);

            Console.WriteLine($"   → Inside: {_sink}");
        }

        Console.WriteLine($"   → After:  {_sink}");
        Console.WriteLine("   → 112 B per scope capturing, 32 B state-passing (.NET 8)");
        Console.WriteLine();
    }

    // A stand-in for the kind of API this package exists for: paired calls, no IDisposable.
    private sealed class LegacyBatchApi
    {
        private bool _open;

        public void BeginBatch()
        {
            _open = true;
            Console.WriteLine("   → BeginBatch()");
        }

        public void Add(string item)
        {
            if (!_open) { throw new InvalidOperationException("Batch is not open"); }
            Console.WriteLine($"   → Add(\"{item}\")");
        }

        public void EndBatch()
        {
            _open = false;
            Console.WriteLine("   → EndBatch()");
        }
    }
}
