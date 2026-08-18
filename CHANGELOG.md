# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the packages
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Each package versions
independently; the heading below states which ones a release covers.

## [2.0.0] - unreleased

Everything below is in **Corsinvest.Fx.Functional**. `Corsinvest.Fx.Defer` ships 2.0.0 as well but
carries no user-visible change - it shares the repository's version number. `Corsinvest.Fx.Unsafe`
and `Corsinvest.Fx.CompileTime` stay at `0.1.0-alpha`; the packaging fix below is the only thing
that reaches them.

### Changed - breaking

- **Union types are declared with the `IUnion<T1..T8>` marker interface; the `[Union]` and
  `[Union<T1..T8>]` attributes are removed.** There is no compatibility shim, and the compile
  errors a 1.x project gets do not name the replacement - see the
  [migration guide](src/Functional/Corsinvest.Fx.Functional/docs/Union.md#migrating-from-1x) for
  the mechanical translation.

  The move exists because an attribute cannot express a case that closes over the root's own type
  parameter. `[Union<Ok<T>, Fail<E>>]` is rejected by the compiler outright (CS8968: an attribute
  argument may not use type parameters), which is why `Option<T>` and `ResultOf<T, E>` could never
  be written as unions in 1.x. A base list has no such restriction, so
  `ResultOf<T, E> : IUnion<Ok<T>, Fail<E>>` compiles, and Roslyn hands the generator the case
  types already substituted.

- **Case types are now ordinary standalone declarations**, not nested `partial record`s inside the
  root. One type can therefore take part in several unions.

- **A union root must be written `abstract partial`.** 1.x added `abstract` silently; a root
  missing either keyword is now reported as **UNION014** rather than corrected behind your back.

- **A union case prints as its value.** `ToString()` on a wrapper returns the value's own string
  rather than `NetworkError { Value = Timeout }`, which exposed a wrapper name no other generated
  member uses. Equality, `GetHashCode`, `with` and positional patterns are unchanged.

`Option<T>` and `ResultOf<T, E>` are themselves `IUnion<...>` roots now, but **neither one's public
API moved**: code that only calls `Match`, `Some`/`None`, `Ok`/`Fail`, `Map`, `Bind`, or switches
on the wrapper types compiles against 2.0.0 unmodified. The break reaches only code that declared
its *own* `[Union]` types.

### Added

- **Exhaustiveness checking for `switch` over a union.** `UNION004` names each case a `switch`
  fails to handle, and three suppressors retire the diagnostics that used to push you toward a
  discard arm - `CS8509` (UNION005), `IDE0010` (UNION006) and `IDE0072` (UNION007). On a closed
  hierarchy that arm is unreachable code that also hides the next case you add.

- **A code fix that fills in the missing cases**, offered on any `UNION004`.

- **State-passing `Match` overloads** on every shape - `Match`, `MatchAsync`, and their
  void-returning forms. A handler that reads from the enclosing scope captures, and a capturing
  lambda allocates a display class plus one delegate per handler on every call. Passing the value
  explicitly lets each handler be `static`: measured on a two-case union, 152 B and 818 ms per 20M
  calls became 0 B and 85 ms.

- **`Option<T>` and `ResultOf<T, E>` expressed through `IUnion<...>`**, so both gain the generated
  `Match`/`MatchAsync`/`Is*`/`TryGet*` surface and the exhaustiveness checking above.

- **Diagnostics for union shapes that cannot work**: `UNION008` (two cases resolve to the same
  wrapper name), `UNION009` (two cases share one CLR type, so no implicit conversions are
  generated for that union), `UNION012` (an interface case type - C# forbids a user-defined
  conversion to or from an interface), `UNION013` (more than one `IUnion<...>` on a root).

- **`[UnionCaseName<T>("...")]`** to pin a wrapper's name when the generated one would collide or
  when a 1.x nested name has to stay stable.

- **`PipeEither` overloads taking a predicate**, sync and async. Only the `bool` form existed for a
  value, so a branch could not read the value it was piped - which is exactly what a mid-chain
  branch needs. `PipeIf` already had both forms.

### Fixed

- `TryGet{Case}` assigned `default!` to a non-nullable `out`, telling the compiler "never null" on
  both paths including the false one. A reference-type case now emits
  `[NotNullWhen(true)] out T?`, so a caller who ignores the `bool` gets `CS8602` and one who
  honours it stays warning-free. A value-type case keeps its old signature: `out int?` would mean
  `Nullable<int>` and change the parameter's type.

- A union whose own type parameter was named `TResult` did not compile - the generated
  `Match<TResult>` shadowed it (`CS0693`), leaving handler and return type spelled the same while
  denoting different symbols (`CS1503`). Generated method type parameters now dodge whatever the
  root declares.

- Generic case types keep their bare name: `Option<T>.Some`, not `Option<T>.SomeOfT`. The
  argument-qualified form is used only when a union really does carry two constructions of the same
  generic definition.

- Nested union roots, hint-name collisions between roots of the same name in different namespaces,
  and shadowed type parameters threaded into the generated `Task<TRoot>` extension class.

- **`dotnet pack` produced no package at all.** Three independent faults: `Functional` and `Unsafe`
  located their analyzer DLL through `$(OutputPath)\..`, which lands in the project's own
  `bin\$(Configuration)\` rather than in the sibling generator project; `DocumentationFile` was set
  from `$(AssemblyName)` before the SDK defines it, so every project wrote a file literally named
  `.xml` and NuGet refused the package (`NU5119`); and `CompileTime` declared a
  `PackageReadmeFile` it never packed (`NU5039`). All four packages now build.

- Documentation examples that did not compile: the README piped through `PipeTapAsync`, which does
  not exist (the method is `TapAsync`), and five snippets wrote `.Pipe(Power, 2)`, where the extra
  argument's type is inferred from the literal and fails to match a `double` parameter (`CS0123`).

### Documentation

- [Union Types](src/Functional/Corsinvest.Fx.Functional/docs/Union.md) rewritten around
  `IUnion<...>`: why an interface rather than an attribute, the switch and exhaustiveness story,
  the migration guide, a comparison with C# 15's `union` keyword, and the generated code in full.

- [Pipe](src/Functional/Corsinvest.Fx.Functional/docs/Pipe.md) rewritten - it documented one method
  out of twenty-five and closed with a link to itself.

## Earlier

Releases before 2.0.0 predate this file.

[2.0.0]: https://github.com/Corsinvest/dotnet-fx/releases
