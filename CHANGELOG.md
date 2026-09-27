# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the packages
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Each package versions
independently; the heading below states which ones a release covers.

## [1.0.0] - unreleased

First public release. Covers **Corsinvest.Fx.Functional** and **Corsinvest.Fx.Defer**, both
stable, and **Corsinvest.Fx.Unsafe**, which ships at the same version but is experimental - see its
README for what that means before depending on it. **Corsinvest.Fx.CompileTime** stays at
`0.1.0-alpha`: it has no test coverage yet, and the version says so.

### Corsinvest.Fx.Functional

**Union types** are declared with the `IUnion<T1..T8>` marker interface:

```csharp
public abstract partial record PaymentMethod : IUnion<CreditCard, PayPal, BankTransfer>;
```

The case types are ordinary standalone declarations, so one type can take part in several unions,
and a case can close over the root's own type parameter. That last point is why the marker is an
interface rather than an attribute: `[Union<Ok<T>, Fail<E>>]` is rejected by the compiler outright
(CS8968, an attribute argument may not use type parameters), while a base list has no such
restriction. `Option<T> : IUnion<Some<T>, None>` and `ResultOf<T, E> : IUnion<Ok<T>, Fail<E>>` are
built on exactly that, so both are unions in their own right and gain the generated
`Match`/`MatchAsync`/`Is*`/`TryGet*` surface.

A union root is written `abstract partial`; a root missing either keyword is reported as
**UNION014** rather than corrected silently. The generator emits one sealed nested wrapper per
case, which keeps the hierarchy closed and is what a `switch` matches on. A wrapper prints as its
value - `ToString()` returns the value's own string rather than exposing a wrapper name no other
generated member uses.

**Exhaustiveness checking for `switch` over a union.** `UNION004` names each case a `switch` fails
to handle and comes with a code fix that writes the missing arms. Three suppressors retire the
diagnostics that would otherwise push you toward a discard arm - `CS8509` (UNION005), `IDE0010`
(UNION006) and `IDE0072` (UNION007) - because on a closed hierarchy that arm is unreachable code
that also hides the next case you add. It works on `switch` statements too, which the compiler
never checks at all.

**Diagnostics for union shapes that cannot work**: `UNION008` (two cases resolve to the same
wrapper name), `UNION009` (two cases share one CLR type, so no implicit conversions are generated),
`UNION012` (an interface case type - C# forbids a user-defined conversion to or from an interface)
and `UNION013` (more than one `IUnion<...>` on a root). `[UnionCaseName<T>("...")]` pins a
wrapper's name when the generated one would collide.

**`ResultOf<T, E>` and `Option<T>`** for type-safe error handling without exceptions: railway-
oriented `Map`/`Bind`/`Recover` chains, LINQ query syntax, async throughout, and a dual naming
scheme (`IsOk`/`IsSuccess`, `Tap`/`OnSuccess`) so FluentResults-style code reads naturally. Factory
methods `Ok()` and `Fail()` are globally available by default via `EnableFunctionalGlobalUsings`.

**State-passing `Match` overloads** on every shape - `Match`, `MatchAsync`, and their void-returning
forms. A handler that reads from the enclosing scope captures, and a capturing lambda allocates a
display class plus one delegate per handler on every call; passing the value explicitly lets each
handler be `static`. Measured on a two-case union, 152 B and 818 ms per 20M calls became 0 B and
85 ms.

`TryGet{Case}` emits `[NotNullWhen(true)] out T?` for a reference-type case, so a caller who
ignores the `bool` gets `CS8602` and one who honours it stays warning-free. A value-type case keeps
`out T`, since `out int?` would mean `Nullable<int>` and change the parameter's type. The
`TryGetValue` extensions on `Option<T>` and `ResultOf<T, E>` carry `[MaybeNullWhen(false)]` for the
same reason, spelled with an attribute because their `T` is unconstrained.

**A case carrying no data allocates nothing.** The generator emits one shared instance per dataless
case, so `Option.None<T>()` is 0 B rather than 24 B. Record equality is by value, which keeps the
sharing invisible: the shared instance and a freshly constructed one stay indistinguishable through
`==`, `Equals`, `GetHashCode`, a `switch`, a pattern with deconstruction, and use as a dictionary
key. Sharing is applied only where it cannot be observed - the case must be a record (a plain class
compares by reference, so sharing would change `==` from `false` to `true`), with no instance state
to mutate and no hand-written constructor whose side effects would otherwise run once instead of
once per case.

**`Option.Some` rejects `null`.** A `Some` holding `null` reports `IsSome` while carrying nothing -
the state `Option<T>` exists to rule out - and the `NullReferenceException` it was meant to prevent
would surface later, inside a `Map` or `Bind`, far from where the null entered. `Option.FromNullable`
remains the way to turn a possible null into `None`. Value types are unaffected: `Option.Some(0)` is
`Some`, because `default(int)` is not null.

**`ResultOf.Try`** is the standalone entry point for turning exceptions into results, carrying both
the `Func` overloads and the `Action` overloads returning `ResultOf<Unit, E>`. The `.Try()`
extensions complement it by taking a value from the pipeline, which the standalone form cannot
express.

**`Option.TapSome`/`TapSomeAsync`** rather than `Tap`/`TapAsync`. The name matches `TapOk` on
`ResultOf`, which already says which case it runs on, and it avoids a collision:
`PipeExtensions.Tap` extends every type, so a `Tap` on `Option<T>` was ambiguous between the two
(CS0121) with nothing in the error pointing at the fix. They mean different things - `TapSome` runs
only on `Some` and hands the action the value, while `Pipe.Tap` always runs and hands it the
`Option<T>` itself - so both are kept.

**`Pipe` extensions** for data transformation chains - `Pipe`, `PipeIf`, `PipeEither`, `Tap`,
`TapAsync` and their async forms. `PipeIf` and `PipeEither` each take both a `bool` and a predicate
receiving the piped value.

### Corsinvest.Fx.Defer

Go-style `defer` for cleanup on scope exit, in LIFO order. The async overload returns
`IAsyncDisposable` rather than `IDisposable`, so `await using` is the only way to consume it and a
plain `using` is a compile error - which is what keeps an async cleanup from being blocked on by
accident. A deferred action that throws is swallowed so the remaining defers still run, the same
trade a `finally` makes.

### Documentation

- [Union Types](src/Functional/Corsinvest.Fx.Functional/docs/Union.md) - why an interface rather
  than an attribute, the switch and exhaustiveness story, a comparison with C# 15's `union`
  keyword, and the generated code in full.
- [ResultOf](src/Functional/Corsinvest.Fx.Functional/docs/ResultOf.md),
  [Option](src/Functional/Corsinvest.Fx.Functional/docs/Option.md),
  [Pipe](src/Functional/Corsinvest.Fx.Functional/docs/Pipe.md),
  [Try](src/Functional/Corsinvest.Fx.Functional/docs/Try.md) and
  [Unit](src/Functional/Corsinvest.Fx.Functional/docs/Unit.md).
- Ten runnable examples under [examples/](examples/).

[1.0.0]: https://github.com/Corsinvest/dotnet-fx/releases
