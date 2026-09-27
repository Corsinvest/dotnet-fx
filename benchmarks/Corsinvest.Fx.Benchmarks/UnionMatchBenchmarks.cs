/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using BenchmarkDotNet.Attributes;
using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Benchmarks;

public record CreditCard(string Number);
public record PayPal(string Email);
public record BankTransfer(string Iban);

public abstract partial record PaymentMethod : IUnion<CreditCard, PayPal, BankTransfer>;

public record Loading;
public record Ready(string Body);

public abstract partial record Response : IUnion<Loading, Ready>;

/// <summary>
/// What matching a union costs, and what the state-passing <c>Match</c> overloads are for.
/// </summary>
/// <remarks>
/// <para>
/// Matching allocates nothing by itself. The cost appears when a handler reads something from the
/// enclosing scope: that makes it a capturing lambda, which the compiler turns into a display
/// class plus one delegate per handler, allocated on every call. The state-passing overloads take
/// the value explicitly so each handler can be <c>static</c>, and a static lambda is cached.
/// </para>
/// <para>
/// The comparison to read is <see cref="Match_Capturing"/> against
/// <see cref="Match_StatePassing"/>. <see cref="Match_NonCapturing"/> is there to show the plain
/// form costs nothing when its handlers happen not to capture - which is why it stays the right
/// default, and the state-passing form is for a measured hot path rather than for everything.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(AllocationOnlyConfig))]
public class UnionMatchBenchmarks
{
    private static readonly PaymentMethod Payment = new PaymentMethod.CreditCard(new CreditCard("4111"));
    private static readonly Response Pending = Response.Loading.Shared;

    private decimal _rate = 2.5m;

    /// <summary>Handlers that read nothing from the scope: the compiler caches one delegate each.</summary>
    [Benchmark(Baseline = true)]
    public decimal Match_NonCapturing() => Payment.Match(
        static _ => 2.5m,
        static _ => 1.5m,
        static _ => 0.0m);

    /// <summary>Handlers reading <c>_rate</c> from the instance: a display class plus three delegates, per call.</summary>
    [Benchmark]
    public decimal Match_Capturing() => Payment.Match(
        _ => _rate * 2.5m,
        _ => _rate * 1.5m,
        _ => _rate * 0.0m);

    /// <summary>The same work with the value passed in, so every handler can be <c>static</c>.</summary>
    [Benchmark]
    public decimal Match_StatePassing() => Payment.Match(
        _rate,
        static (decimal r, CreditCard _) => r * 2.5m,
        static (decimal r, PayPal _) => r * 1.5m,
        static (decimal r, BankTransfer _) => r * 0.0m);

    /// <summary>A native switch over the closed hierarchy - no delegates involved at all.</summary>
    [Benchmark]
    public decimal Switch() => Payment switch
    {
        PaymentMethod.CreditCard => 2.5m,
        PaymentMethod.PayPal => 1.5m,
        PaymentMethod.BankTransfer => 0.0m
    };

    [Benchmark]
    public bool Is_Property() => Payment.IsCreditCard;

    [Benchmark]
    public bool TryGet() => Payment.TryGetCreditCard(out _);

    // ---- construction --------------------------------------------------

    /// <summary>
    /// The wrapper alone, over a case value that already exists: this is the cost the docs quote
    /// for "constructing a case wrapper".
    /// </summary>
    [Benchmark, BenchmarkCategory("Construction")]
    public PaymentMethod Wrap_ExistingValue() => new PaymentMethod.CreditCard(Card);

    /// <summary>
    /// Wrapper plus the case value, which is what calling code usually writes. Two objects, so
    /// twice the wrapper's own cost - worth keeping separate from
    /// <see cref="Wrap_ExistingValue"/> so neither figure is mistaken for the other.
    /// </summary>
    [Benchmark, BenchmarkCategory("Construction")]
    public PaymentMethod Wrap_NewValue() => new PaymentMethod.CreditCard(new CreditCard("4111"));

    /// <summary>A dataless case is handed out from a shared instance rather than allocated.</summary>
    [Benchmark, BenchmarkCategory("Construction")]
    public Response Dataless_Case() => Response.Loading.Shared;

    [Benchmark, BenchmarkCategory("Construction")]
    public Response Case_WithData() => new Response.Ready(new Ready("body"));

    private static readonly CreditCard Card = new("4111");
}
