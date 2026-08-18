# Union Types - Discriminated Unions

**Create type-safe union types with source generators**

## Overview

The `IUnion<T1..T8>` marker interface enables you to create discriminated unions (also known as sum types or tagged unions) in C#. Discriminated unions represent a value that can be one of several named cases, each potentially with different data.

The case types are ordinary, independently declared types - records, classes, structs, enums,
even `string` and other primitives - and the interface just names the closed set. The generator
sees the declaration and emits, per case, a `sealed partial record` wrapper nested inside the root:

```csharp
using Corsinvest.Fx.Functional;

public record Cat(string Name);
public record Dog(string Name);

public abstract partial record Pet : IUnion<Cat, Dog>;
```

```csharp
// Generated (roughly):
public sealed partial record Pet.Cat(Cat Value) : Pet;
public sealed partial record Pet.Dog(Dog Value) : Pet;
```

The source generator also creates:

- An implicit conversion from each case type to the root
- Exhaustive pattern matching via `Match()`, plus a void-returning `Match()` overload
- `MatchAsync` overloads, both on the instance and as `Task<TRoot>` extensions (see
  [Async Pattern Matching](#async-pattern-matching))
- Native `switch` support with compile-time exhaustiveness checking (see
  [Switch Expressions](#switch-expressions))
- `Is{Case}` properties and `TryGet{Case}` methods

## Basic Usage

### Defining a Union Type

```csharp
using Corsinvest.Fx.Functional;

public record Circle(double Radius);
public record Rectangle(double Width, double Height);
public record Triangle(double Base, double Height);

public abstract partial record Shape : IUnion<Circle, Rectangle, Triangle>;
```

**Requirements:**

- The union root must be `partial`
- The case types are ordinary types, declared independently - not nested, not `partial`, no
  attribute on them at all

### Pattern Matching

```csharp
double CalculateArea(Shape shape) => shape.Match(
    circle => Math.PI * circle.Radius * circle.Radius,
    rectangle => rectangle.Width * rectangle.Height,
    triangle => 0.5 * triangle.Base * triangle.Height
);

// Usage
var circle = new Circle(5.0);
Shape shapeValue = circle;              // implicit conversion from the case type
var area = CalculateArea(shapeValue);   // 78.54
```

The `Match()` method is **exhaustive** - you must handle all cases, or it won't compile. Each
handler receives the **case type itself** (`Circle`, not the generated `Shape.Circle` wrapper),
since the wrapper's only job is to make the hierarchy closed.

That holds for printing too: a wrapper's `ToString` prints its **value**, so a union reads the way
the case does.

```csharp
Shape shape = new Circle(5.0);
Console.WriteLine(shape);       // Circle { Radius = 5 }, not Circle { Value = Circle { Radius = 5 } }
```

The case name is used only when the value is null, so a case never prints as an empty string.

### Matching without capturing

A handler that reads anything from the surrounding scope is a *capturing* lambda, and the compiler
turns it into a heap object per call - one display class holding the captured values, plus one
delegate per handler:

```csharp
decimal Fee(PaymentMethod method, decimal rate) => method.Match(
    creditCard => rate * 2.5m,      // captures `rate`
    payPal     => rate * 1.5m       // captures it again
);
```

Every `Match`, `MatchAsync` and their void-returning forms therefore come in a second shape that
takes the value explicitly, so each handler can be `static` and capture nothing:

```csharp
decimal Fee(PaymentMethod method, decimal rate) => method.Match(
    rate,
    static (r, creditCard) => r * 2.5m,
    static (r, payPal)     => r * 1.5m
);
```

Measured on a two-case union, per call: **152 B and 818 ms** for the capturing form over 20M
iterations, against **0 B and 85 ms** for the state-passing one. Pass a tuple or a small record when
a handler needs more than one value.

The plain form stays the right default - it reads better, and a handler that captures nothing
(`x => x.Radius`) allocates nothing either, because the compiler caches a single delegate for it.
Reach for the state-passing form on a hot path, where the union is matched in a loop or per request.

## Why an interface, and not an attribute?

Earlier versions of this package spelled a union with `[Union]` and nested `partial record` cases.
That attribute is gone; `IUnion<T1..T8>` replaced it entirely. The reason is not stylistic - it is
what makes `Option<T>` and `ResultOf<T, E>` expressible at all.

An attribute's type arguments are **metadata**. They are resolved before the decorated type is
even bound, which means they can never reference that type's own type parameters. Try to spell a
generic union this way and every variant fails, for a different reason:

```csharp
// error CS8968: an attribute argument cannot reference the decorated type's own type parameter.
// [Union<Ok<T>, Fail<E>>]
// public abstract partial record ResultOf<T, E>;

// error CS7003: an unbound generic type is not a legal type argument.
// [Union<Ok<>, Fail<>>]
// public abstract partial record ResultOf<T, E>;

// error CS0416: 'Ok<T>': an attribute argument cannot use type parameters.
// [UnionOf(typeof(Ok<T>))]
// public abstract partial record ResultOf<T, E>;
```

None of these are generator limitations - the compiler rejects all three before any generator
code runs. There is no clever workaround inside an attribute-based design; the constraint is
structural.

An interface is different: it is part of the type's own declaration, resolved in the same scope
as the type parameters it declares. So this compiles, unremarkably:

```csharp
public abstract partial record ResultOf<T, E> : IUnion<Ok<T>, Fail<E>>;
```

and Roslyn hands the generator `Ok<T>` and `Fail<E>` **already bound** to `ResultOf<T, E>`'s own
`T` and `E`. The generator does not substitute, infer, or reconstruct anything - it reads the
interface's type arguments off the symbol exactly as the compiler resolved them. There is no
substitution logic in the generator at all. That absence is the design's payoff, not an
implementation detail: it is *why* a case can close over the root's own type parameter, something
no attribute-based design could ever add, no matter how it was extended.

This is why `Option<T>` (`IUnion<Some<T>, None>`) and `ResultOf<T, E>` (`IUnion<Ok<T>, Fail<E>>`)
exist as they do - see [Option\<T\>](Option.md) and [ResultOf\<T, E\>](ResultOf.md).

**Case types can be almost anything**: classes, sealed classes, records, structs, record structs,
enums, `string`, `int` and other primitives, arrays, closed generics (`List<T>`,
`Dictionary<K,V>`), and tuples - including **named** tuples such as `(int X, int Y)`, which could
never appear as an attribute type argument (`CS8970`) but are an ordinary type argument to an
interface. Value-type cases are held in a typed field on their wrapper - a
`struct Money { public decimal Amount; }` case ends up as `Root.Money.Value` of type `Money`, not
`object` - so **nothing is boxed**.

One shape is rejected: an **interface** case type, reported as `UNION012` - see below.

`examples/04_UnionTypes.cs` has a working `AppError` union over a sealed class, an enum, a struct,
and `string` in one declaration:

```csharp
public sealed class DatabaseError(string Table, int Code)
{
    public string Table { get; } = Table;
    public int Code { get; } = Code;
}

public enum NetworkError { Timeout, Refused, DnsFailure }

public readonly struct ValidationError(int Line, int Column)
{
    public int Line { get; } = Line;
    public int Column { get; } = Column;
}

public abstract partial record AppError : IUnion<DatabaseError, NetworkError, ValidationError, string>;
```

```csharp
// Each element converts implicitly - no need to name the wrapper explicitly.
var errors = new AppError[]
{
    new DatabaseError("orders", 1205),
    NetworkError.Timeout,
    new ValidationError(42, 7),
    "plain message"
};
```

One shape is rejected on purpose: **interfaces**. The generator always emits an implicit
conversion operator for each case, and C# forbids a user-defined conversion to or from an
interface (`CS0552`). Rather than let that surface as a confusing compiler error deep in generated
code, the generator checks for it itself and reports **UNION012**, naming the offending case, then
stops generating for that union entirely:

```csharp
public interface IShape;
public record Cat(string Name);

// error UNION012: Union 'Pet' has case type 'IShape', which is an interface; C# forbids a
// user-defined conversion to or from an interface, so the generated implicit conversion cannot
// compile.
public abstract partial record Pet : IUnion<IShape, Cat>;
```

## Migrating from 1.x

Version 2.0.0 removes the `[Union]`/`[Union<T1..T8>]` attributes entirely - `IUnion<T1..T8>`
replaces them, not alongside them. There is no compatibility shim: a 1.x project upgrading to
2.0.0 gets compile errors wherever `[Union]` or `[Union<...>]` appears, with nothing in the error
text pointing at what replaced it. This section is that pointer.

### Plain unions

Change the case types from nested `partial record`s to ordinary standalone types, drop the
attribute, and move its type arguments (if it had any) into a base-list `IUnion<...>`:

```csharp
// 1.x
[Union]
public partial record Pet
{
    public partial record Cat(string Name, int Lives);
    public partial record Dog(string Name);
}

// 2.0.0
public record Cat(string Name, int Lives);
public record Dog(string Name);

public abstract partial record Pet : IUnion<Cat, Dog>;
```

The root must now be written `public abstract partial record` - `abstract` was implicit before
(the generator added it silently); under `IUnion<...>` a root missing either keyword is reported
as **UNION014**, not silently corrected.

Everything the generator produces - `Match`/`MatchAsync`, `Is{Case}`, `TryGet{Case}`, the implicit
conversions, the wrapper types themselves (`Pet.Cat`, `Pet.Dog`) - keeps the same shape. Call sites
that only use that surface do not change at all.

### `Option<T>` and `ResultOf<T, E>`

Both are still `IUnion<...>` roots under the hood (`Option<T> : IUnion<Some<T>, None>`,
`ResultOf<T, E> : IUnion<Ok<T>, Fail<E>>`), and neither one's public API moved. If your code only
calls `Match`, `MatchAsync`, `Some`/`None`, `Ok`/`Fail`, `Map`, `Bind`, or switches on the wrapper
types, **there is nothing to change** - those call sites compile unmodified against 2.0.0. The
break only reaches code that declared its *own* `[Union]` types, not code that merely consumed
`Option<T>`/`ResultOf<T, E>`.

### Keeping a wrapper's name stable

If a 1.x case type's nested name mattered to callers (`Pet.Cat` used explicitly, e.g. in a
`catch`-style pattern or a public signature), `[UnionCaseName<T>]` pins the 2.0.0 wrapper name to
match:

```csharp
[UnionCaseName<Cat>("Cat")]
public abstract partial record Pet : IUnion<Cat, Dog>;
```

This is also the mechanism for resolving a name collision the default rules can't - see
[Wrapper names](#wrapper-names) below.

### Wrapper names

Each case gets a wrapper name, derived from the case type unless overridden. The rules, checked
in order:

1. **The case type's own short name**, by default - `Cat` → `Pet.Cat`.
2. **Namespace-prefixed**, when two cases' short names collide - `Farm.Cat` and `Wild.Cat` both
   start as `Cat`, so they become `FarmCat` and `WildCat`.
3. **Generic case types keep their bare name** - `Some<T>` → `Some`, `List<string>` → `List`.
   A union normally carries one case per generic definition, so the type arguments add nothing.
   When a union really does hold two constructions of the same definition, both get the
   argument-qualified form instead: `IUnion<Box<int>, Box<string>>` yields `BoxOfInt32` and
   `BoxOfString`.
4. **`{Element}Array` for arrays** - `int[]` → `Int32Array`.
5. **`TupleOf...` for tuples**, named or not - `(int X, int Y)` → `TupleOfInt32Int32` (element
   names do not affect the wrapper name; see [UNION009](#union009-implicit-conversions-omitted-for-the-whole-union)
   for what happens when that causes two cases to collide).
6. **CLR names, not keywords** - `int` → `Int32`, `string` → `String`, so the wrapper is always a
   legal identifier.
7. **`[UnionCaseName<T>("...")]` always wins** - an explicit override is applied before any of the
   rules above are considered, for that case type.

```csharp
namespace Farm { public record Cat(string Name); }
namespace Wild { public record Cat(string Species); }

// Both named "Cat" - rule 2 (namespace prefix) resolves the collision automatically.
public abstract partial record Feline : IUnion<Farm.Cat, Wild.Cat>;
// => Feline.FarmCat, Feline.WildCat

// Overriding explicitly (rule 7) instead of relying on the namespace prefix:
[UnionCaseName<Farm.Cat>("Domestic")]
[UnionCaseName<Wild.Cat>("Feral")]
public abstract partial record Feline2 : IUnion<Farm.Cat, Wild.Cat>;
// => Feline2.Domestic, Feline2.Feral
```

If two cases still collide after the namespace prefix - most often because the exact same type
is listed twice - the generator cannot invent a third name and reports **UNION008**:

```csharp
public record Cat(string Name);

// error UNION008: case types resolve to the same wrapper name
public abstract partial record Pet : IUnion<Cat, Cat>;
```

#### `[UnionCaseName<T>]` on a case that closes over the root's own type parameter

`[UnionCaseName<T>("...")]` is itself an attribute, so its own type argument is bound by the same
rule as any other attribute argument: it must be **closed** - it cannot mention the root's type
parameter either. That matters whenever you do want to rename such a case - say a `Wrapped<T>`
whose default name `Wrapped` collides with something else - since it only ever appears as the
open generic in the declaration:

```csharp
public abstract partial record Option<T> : IUnion<Some<T>, None>;
```

The override works anyway, by naming a **closed stand-in** instead - any fully-constructed
version of the same open generic:

```csharp
[UnionCaseName<Wrapped<int>>("Payload")]   // renames Wrapped<T>, not just Wrapped<int>
public abstract partial record Envelope<T> : IUnion<Wrapped<T>, None>;
```

The naming logic matches this by **original definition**: `Some<int>` and the case type `Some<T>`
share the same unbound definition (`Some<>`), so the override applies to the case as declared,
whatever `T` ends up being at any particular closed `Option<int>` or `Option<string>`. This
matters only for a case that is open with respect to the root's type parameters; a fully closed
case type (like `None` above) is matched by exact type instead, so two distinct closed cases -
`Some<int>` and `Some<string>` as two *separate* entries in an `IUnion<...>` list, say - are never
conflated by this fallback.

## Switch Expressions

Union cases are real nested types, so you can use a plain C# `switch` instead of `Match()`. Each
wrapper has exactly one positional member - the case value itself - so the pattern binds one
variable per arm, named after the case type, and you reach its members through that:

```csharp
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle(var circle) => Math.PI * circle.Radius * circle.Radius,
    Shape.Rectangle(var rectangle) => rectangle.Width * rectangle.Height,
    Shape.Triangle(var triangle) => 0.5 * triangle.Base * triangle.Height
};
```

Note there is **no discard arm** (`_`), and this still compiles. That takes some explaining.

### The problem with a discard arm

By default, the C# compiler treats reference-type hierarchies as open: another assembly could always add a subtype. So it emits `CS8509` ("switch expression is not exhaustive") and pushes you toward a discard arm.

That discard is where exhaustiveness goes to die:

```csharp
// Later, someone adds Shape.Pentagon to the union.
// This switch keeps compiling - and silently returns 0 for pentagons.
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle c => Math.PI * c.Radius * c.Radius,
    Shape.Rectangle r => r.Width * r.Height,
    Shape.Triangle t => 0.5 * t.Base * t.Height,
    _ => 0   // ← swallows the new case, no warning anywhere
};
```

### How the package fixes it

An `IUnion<...>` hierarchy **is** closed - the generator emits a private constructor on the root and seals every case, so nothing outside can derive from it. The package ships two analyzers that act on this:

| ID | Kind | What it does |
| --- | --- | --- |
| **UNION004** | Warning | Reports union cases a `switch` does not handle, **by name** - with a code fix that adds them |
| **UNION005** | Suppressor | Suppresses `CS8509` when every case is handled, so no discard arm is needed |
| **UNION006/007** | Suppressor | Suppresses the IDE's "Add default case" suggestions (`IDE0010`, `IDE0072`) for the same reason |

The suppressions matter as much as the warning: `CS8509`, `IDE0010` and `IDE0072` all steer you
toward the discard arm that hides future cases. On a closed union that arm is unreachable code,
so the package stands those suggestions down and lets UNION004 speak instead.

Together they invert the default behaviour: the discard arm becomes unnecessary, and a missing case becomes loud.

```csharp
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle(var circle) => Math.PI * circle.Radius * circle.Radius,
    Shape.Rectangle(var rectangle) => rectangle.Width * rectangle.Height
    // warning UNION004: Switch on union 'Shape' does not handle variant 'Triangle'
    // warning CS8509: switch expression is not exhaustive
};
```

`CS8509` is only suppressed once every case is handled - an incomplete switch keeps both
warnings, so it cannot slip through either way.

Add `Shape.Pentagon` to the union and every `switch` that ignores it lights up - which is exactly what you want from a closed set of cases.

### Code fix: fill in the missing cases

`UNION004` ships with a code fix. Put the caret on the warning and pick **"Add missing union cases"**
(<kbd>Ctrl</kbd>+<kbd>.</kbd> in Visual Studio, <kbd>Alt</kbd>+<kbd>Enter</kbd> in Rider):

```csharp
// before
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle(var circle) => Math.PI * circle.Radius * circle.Radius
};

// after
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle(var circle) => Math.PI * circle.Radius * circle.Radius,
    Shape.Rectangle(var rectangle) => throw new System.NotImplementedException(),
    Shape.Triangle(var triangle) => throw new System.NotImplementedException()
};
```

Each added arm deconstructs its case, binding one variable named after the case type - `var
rectangle`, not `var value` - since every wrapper's sole positional member is the case value
itself. A case with no positional members (an empty record, for instance) gets a bare type
pattern instead. The body is `throw new NotImplementedException()`, so an arm you forget to
finish fails loudly rather than returning a plausible default.

An existing discard arm stays last, keeping the added arms reachable. The fix also supports
**Fix All** in document, project, or solution.

It can be applied from the command line too:

```bash
dotnet format analyzers --diagnostics UNION004 --severity warn
```

Both work on switch **statements** too, which the compiler does not check for exhaustiveness at all:

```csharp
switch (shape)
{
    case Shape.Circle c: return Area(c);
    case Shape.Rectangle r: return Area(r);
    // warning UNION004: ... does not handle variant 'Triangle'
}
```

### What counts as handling a case

A pattern only counts when it matches **every** value of that case:

| Pattern | Covers the case? |
| --- | --- |
| `Shape.Circle` | ✅ |
| `Shape.Circle c` | ✅ |
| `Shape.Circle { }` | ✅ |
| `Shape.Circle(var circle)` | ✅ all subpatterns irrefutable |
| `Shape.Circle(_)` | ✅ |
| `Shape.Circle or Shape.Rectangle` | ✅ both |
| `Shape.Circle c when c.Value.Radius > 0` | ❌ guarded, can fail |
| `Shape.Circle { Value.Radius: 5 }` | ❌ matches a subset |

### Configuration

`UNION004` is an ordinary analyzer rule, so it can be tuned per project or per file:

```ini
# .editorconfig
dotnet_diagnostic.UNION004.severity = error       # none | silent | suggestion | warning | error
```

Under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` a missing case fails the build.

### Switch or Match?

Both are supported; they trade off differently.

| | `Match()` | `switch` |
| --- | --- | --- |
| Missing case | compile **error** (arity mismatch) | UNION004 **warning** (tunable to error) |
| Case identified by | **position** | **type name** |
| Reordering handlers | silently changes behaviour | impossible to get wrong |
| Async | `MatchAsync` overloads | not applicable |
| Partial handling | not possible | natural, with a discard arm |

`Match()` gives the harder guarantee, since a missing handler cannot compile at all. `switch` is safer against a subtler mistake: because arms name their type, two handlers of the same shape cannot be swapped by accident. Use named arguments (`onCircle:`, `onRectangle:`) when you stay with `Match()`.

## Comparison with C# 15 union types

C# 15 adds a `union` keyword (.NET 11, GA November 2026). It solves the same problem with a
different model, so the two are worth comparing directly - including where this package loses.

### Two different models

This package is a **tag union**: a closed hierarchy where the generator emits one sealed wrapper
per case, deriving from the root.

```csharp
public record Cat(string Name);          // a normal, standalone type
public record Dog(string Name);

public abstract partial record Pet : IUnion<Cat, Dog>;
```

C# 15 is a **type union**: it composes types that already exist independently, the same way this
package does, but with a different runtime shape underneath.

```csharp
public record Cat(string Name);
public record Dog(string Name);
public union Pet(Cat, Dog);              // just names the closed set
```

The compiler lowers that declaration to roughly:

```csharp
[Union] public struct Pet : IUnion
{
    public Pet(Cat value) => Value = value;
    public Pet(Dog value) => Value = value;
    public object? Value { get; }
}
```

One `object?` field. Everything else follows from that choice.

### Where this package is stronger

**No invalid state.** A struct always has an implicit parameterless constructor, and nothing can
prevent it. `default(Pet)` is a `Pet` whose `Value` is `null` - a union that is none of its cases:

```csharp
Pet pet = default;                        // legal, no warning
var arr = new Pet[10];                    // ten of them
dict.TryGetValue(key, out var pet);       // and here
```

The compiler then *requires* a `null` arm in every switch. With this package's generated
hierarchy, the root's private constructor and sealed cases make that state unrepresentable - there
is no `null` arm to write.

**No boxing.** Because `Value` is `object?`, a value-type case is boxed on assignment - the docs
are explicit that the generated form "always boxes value-type cases". A `union IntOrString(int, string)`
allocates 24 bytes on the heap to hold a 4-byte `int`, so the struct meant to avoid allocation
allocates anyway. This package's cases hold their data in typed fields, with no boxing at any
point - true for a value-type case exactly as much as a reference-type one.

(C# 15 offers a way out, but you have to build it yourself: a hand-written union implementing the
*non-boxing access pattern* - `HasValue` plus a `TryGetValue` per case, over your own tag and
fields - which the compiler then prefers over `Value`. That is the tagged-struct design written by
hand; the `union` keyword gives you the `switch` syntax, not the storage.)

**Async matching.** `MatchAsync` overloads are generated for you, both on the instance and as
`Task<TRoot>` extensions for fluent chaining (see [Async Pattern Matching](#async-pattern-matching)).
`switch` is not awaitable, so the C# 15 model has no equivalent - you write that plumbing yourself.

**Cases closing over the root's own type parameter.** `Option<T> : IUnion<Some<T>, None>` and
`ResultOf<T, E> : IUnion<Ok<T>, Fail<E>>` express a case whose shape depends on the union root's
*own* generic parameter. C# 15's cases are independent types with no relationship to the `union`
declaration beyond being listed in it, so there is nothing for them to close over - a union
keyword equivalent of `Option<T>` is not expressible at all. See
[Why an interface, and not an attribute?](#why-an-interface-and-not-an-attribute) for why this
package can do it and an attribute-based design could not have.

**Available today**, on net8.0 and net9.0, with no preview SDK.

### Where C# 15 is still stronger

**Ad hoc unions.** `(A or B or C) x = ...` composes a union inline, with no declaration. A source
generator cannot offer that.

**Native and dependency-free** - language syntax, with IDE and debugger support built in, no
package reference and no build-time source generator.

### Side by side

| | This package (`IUnion<...>`) | C# 15 `union` |
| --- | --- | --- |
| Invalid state | **impossible** | `default` has a null `Value` |
| Boxing of value-type cases | **never** | always (unless you hand-write a non-boxing union) |
| Indirection to reach the data | 1 hop | 2 hops (`Value`, then the object) |
| Cases usable as types | ✅ the standalone case type, plus `Root.Case` | ✅ standalone types |
| Same type in several unions | ✅ `Cat` can be a case of `Pet` and of `Animal` at once | ✅ |
| Cases closing over the root's own type parameter | ✅ `Option<T>`, `ResultOf<T, E>` | ❌ not expressible - cases are independent types |
| Ad hoc unions | ❌ | ✅ |
| Async matching | ✅ `MatchAsync` + `Task<TRoot>` extensions | ❌ |
| Exhaustive `switch` | ✅ via UNION004/005 | ✅ built into the compiler |
| Available on | net8.0+ | .NET 11 |

What is left, on this package's side, is ad hoc unions and native language syntax with zero
dependency - real advantages for a throwaway `(A or B) x` that never gets a name. What this
package keeps that C# 15 cannot reach at all, regardless of how the comparison shifts elsewhere,
is a case that closes over the union root's own type parameter - the shape `Option<T>` and
`ResultOf<T, E>` are built from.

### Related: the `closed` modifier

C# 15 also adds `closed`, which restricts derivation to the declaring assembly so the compiler can
check exhaustiveness itself:

```csharp
public closed record class JobStatus;
public record class Queued : JobStatus;
public record class Failed(string Error) : JobStatus;
```

This is the mechanism UNION004 and UNION005 emulate today. This package's generated hierarchy is
already closed in fact - a private constructor makes external derivation a compile error - but
Roslyn does not infer exhaustiveness from that, which is why the analyzers exist.

### Does this package become obsolete?

No, for two reasons.

It keeps working on net8.0 and net9.0, which C# 15 does not reach.

More to the point, the `[Union]` attribute contract in `System.Runtime.CompilerServices`
**accepts classes**, not only structs: a type qualifies by carrying the attribute, exposing
single-parameter constructors, and a `Value` property. So when .NET 11 is generally available, the
generator can emit those members alongside the existing hierarchy, and the *native* compiler will
pattern-match these unions with built-in exhaustiveness - while keeping the sealed hierarchy, the
absent invalid state, and zero boxing. That combination is not available from the `union` keyword
on its own.

### References

- [Union types - C# reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union)
- [`closed` modifier - C# reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/closed)
- [Explore union types in C# 15 - .NET Blog](https://devblogs.microsoft.com/dotnet/csharp-15-union-types/)

*Comparison written against .NET 11 preview documentation (August 2026); details may change before GA.*

## Real-World Examples

### Payment Methods

```csharp
public record CreditCard(string Number, string Cvv, DateTime Expiry);
public record PayPal(string Email);
public record BankTransfer(string Iban, string Bic);
public record Cryptocurrency(string WalletAddress, string Currency);

public abstract partial record PaymentMethod
    : IUnion<CreditCard, PayPal, BankTransfer, Cryptocurrency>;

decimal CalculateFee(PaymentMethod method, decimal amount) => method.Match(
    creditCard => amount * 0.029m + 0.30m,      // 2.9% + $0.30
    payPal => amount * 0.034m + 0.30m,          // 3.4% + $0.30
    bankTransfer => 0m,                          // Free
    crypto => amount * 0.01m                     // 1%
);

string GetDisplayName(PaymentMethod method) => method.Match(
    creditCard => $"Credit Card ****{creditCard.Number[^4..]}",
    payPal => $"PayPal ({payPal.Email})",
    bankTransfer => $"Bank Transfer ({bankTransfer.Iban})",
    crypto => $"{crypto.Currency} Wallet"
);
```

### API Response States

```csharp
public record Success<T>(T Data, int StatusCode);
public record Error(string Message, int StatusCode);
public record Loading;
public record NotStarted;

public abstract partial record ApiResponse<T>
    : IUnion<Success<T>, Error, Loading, NotStarted>;

void HandleUserResponse(ApiResponse<User> response) => response.Match(
    success => DisplayUser(success.Data),
    error => ShowError(error.Message),
    loading => ShowSpinner(),
    notStarted => ShowPlaceholder()
);

// Usage with state management
ApiResponse<User> userState = new Loading();
userState = await FetchUser()
    ? new Success<User>(user, 200)
    : new Error("Not found", 404);
```

### Log Levels with Context

```csharp
public record Info(string Message, DateTime Timestamp);
public record Warning(string Message, string Details, DateTime Timestamp);
public record LogError(Exception Exception, string Message, DateTime Timestamp);
public record Debug(string Message, Dictionary<string, object> Context, DateTime Timestamp);

public abstract partial record LogEntry : IUnion<Info, Warning, LogError, Debug>;

void WriteLog(LogEntry entry) => entry.Match(
    info => _logger.LogInformation("[{Time}] {Message}", info.Timestamp, info.Message),
    warning => _logger.LogWarning("[{Time}] {Message}: {Details}",
        warning.Timestamp, warning.Message, warning.Details),
    error => _logger.LogError(error.Exception, "[{Time}] {Message}",
        error.Timestamp, error.Message),
    debug => _logger.LogDebug("[{Time}] {Message} {Context}",
        debug.Timestamp, debug.Message, JsonSerializer.Serialize(debug.Context))
);
```

### Command Pattern

```csharp
public record CreateUser(string Email, string Name);
public record UpdateUser(int Id, string Name);
public record DeleteUser(int Id);
public record SendEmail(int UserId, string Subject, string Body);

public abstract partial record Command
    : IUnion<CreateUser, UpdateUser, DeleteUser, SendEmail>;

async Task<ResultOf<string, string>> ExecuteCommand(Command command) =>
    await command.Match(
        create => CreateUserAsync(create.Email, create.Name),
        update => UpdateUserAsync(update.Id, update.Name),
        delete => DeleteUserAsync(delete.Id),
        sendEmail => SendEmailAsync(sendEmail.UserId, sendEmail.Subject, sendEmail.Body)
    );
```

## Advanced Patterns

### Union with Empty Cases

```csharp
public record Connected;
public record Connecting;
public record Disconnected;
public record NetworkError(string Message);

public abstract partial record NetworkStatus
    : IUnion<Connected, Connecting, Disconnected, NetworkError>;

// Empty cases don't need parameters, and the implicit conversion still applies
NetworkStatus status = new Connected();
```

### Nested Unions

```csharp
public record Validation(List<string> Errors);
public record Network(int StatusCode);
public record Internal(Exception Exception);

public abstract partial record ErrorDetails : IUnion<Validation, Network, Internal>;

public record Success(string Message);
public record Warning(string Message, string Details);
public record Failure(string Message, ErrorDetails Details);

public abstract partial record Result : IUnion<Success, Warning, Failure>;

void LogResult(Result result) => result.Match(
    success => Console.WriteLine($"✓ {success.Message}"),
    warning => Console.WriteLine($"⚠ {warning.Message}: {warning.Details}"),
    failure => failure.Details.Match(
        validation => Console.WriteLine($"✗ Validation: {string.Join(", ", validation.Errors)}"),
        network => Console.WriteLine($"✗ Network error: HTTP {network.StatusCode}"),
        internalError => Console.WriteLine($"✗ Internal error: {internalError.Exception.Message}")
    )
);
```

### Generic Unions

`Option<T>` ships with the package; this is the shape it is built from - a case closing over the
root's own type parameter, which only the interface form can express (see
[Why an interface, and not an attribute?](#why-an-interface-and-not-an-attribute)). The wrapper is
named `Some` with no override needed: rule 3 keeps a generic case type's bare name, and this union
holds only one construction of `Some<>`:

```csharp
public sealed record Some<T>(T Value);
public sealed record None;

public abstract partial record Option<T> : IUnion<Some<T>, None>;

Option<int> ParseInt(string input) =>
    int.TryParse(input, out var result)
        ? new Some<int>(result)     // implicit conversion, same as any other case
        : new None();

// Usage
var result = ParseInt("42").Match(
    some => $"Parsed: {some.Value}",
    none => "Invalid number"
);
```

## Best Practices

### 1. Use Descriptive Case Names

```csharp
// ✅ Good - clear intent
public record Pending(DateTime CreatedAt);
public record Completed(DateTime CompletedAt, string TransactionId);
public record Failed(DateTime FailedAt, string Reason);

public abstract partial record PaymentStatus : IUnion<Pending, Completed, Failed>;

// ❌ Bad - unclear
public record Status1(DateTime Time);
public record Status2(DateTime Time, string Id);
public record Status3(DateTime Time, string Reason);

public abstract partial record PaymentStatus2 : IUnion<Status1, Status2, Status3>;
```

### 2. Include Relevant Data in Each Case

```csharp
// ✅ Good - each case has the data it needs
public record HttpSuccess(string Body, int StatusCode, Dictionary<string, string> Headers);
public record Redirect(string Location, int StatusCode);
public record ClientError(string Message, int StatusCode);
public record ServerError(string Message, int StatusCode, string? StackTrace);

public abstract partial record HttpResponse
    : IUnion<HttpSuccess, Redirect, ClientError, ServerError>;

// ❌ Bad - forcing all data into all cases
public record Response(string? Body, string? Location, string? Message, int StatusCode);

public abstract partial record HttpResponse2 : IUnion<Response>;
```

### 3. Exhaustive Matching

With `Match()`, a missing case is a compile error, because the generated method takes one handler per case:

```csharp
// ✅ Compiles - all cases handled
shape.Match(
    circle => CalculateCircleArea(circle),
    rectangle => CalculateRectangleArea(rectangle),
    triangle => CalculateTriangleArea(triangle)
);

// ❌ Won't compile - missing triangle case
shape.Match(
    circle => CalculateCircleArea(circle),
    rectangle => CalculateRectangleArea(rectangle)
    // Error: no overload takes 2 arguments
);
```

Since handlers are matched **by position**, prefer named arguments so that reordering cannot silently change behaviour:

```csharp
shape.Match(
    onCircle: circle => CalculateCircleArea(circle),
    onRectangle: rectangle => CalculateRectangleArea(rectangle),
    onTriangle: triangle => CalculateTriangleArea(triangle)
);
```

With a `switch`, exhaustiveness is enforced by the **UNION004** analyzer instead - see [Switch Expressions](#switch-expressions).

### 4. Use with ResultOf for Error Handling

```csharp
public record Required(string FieldName);
public record TooShort(string FieldName, int MinLength);
public record InvalidFormat(string FieldName, string Expected);

public abstract partial record ValidationError : IUnion<Required, TooShort, InvalidFormat>;

ResultOf<User, ValidationError> ValidateUser(string email, string name)
{
    if (string.IsNullOrEmpty(email))
        return ResultOf.Fail<User, ValidationError>(
            new ValidationError.Required(new Required("email")));

    if (name.Length < 2)
        return ResultOf.Fail<User, ValidationError>(
            new ValidationError.TooShort(new TooShort("name", 2)));

    return ResultOf.Ok<User, ValidationError>(new User(email, name));
}
```

## Comparison with Other Patterns

| Pattern            | Type Safety | Exhaustiveness  | Extensibility         |
| ------------------ | ----------- | --------------- | ---------------------- |
| **Union Types**    | ✅ Full     | ✅ Compile-time | ⚠️ Closed (by design) |
| Inheritance + `is` | ⚠️ Runtime  | ❌ No           | ✅ Open               |
| Enums              | ✅ Full     | ⚠️ Switch only  | ❌ No data            |
| Interfaces         | ❌ No       | ❌ No           | ✅ Open               |

**Union types are best when:**

- You have a fixed set of cases (closed set)
- Each case has different data
- You want compile-time exhaustiveness checking
- You're modeling domain concepts (states, commands, responses)

## Async Pattern Matching

The generator creates both instance `MatchAsync` overloads and extension methods on `Task<TRoot>`,
so you can await first and match after (or the other way around) without an intermediate variable.

### MatchAsync on Task<TRoot>

```csharp
// Async Match with TResult - handlers return Task<TResult>
Task<string> ProcessShapeAsync(Shape shape) => shape.Match(
    circle => Task.FromResult($"Circle with radius {circle.Radius}"),
    rectangle => Task.FromResult($"Rectangle {rectangle.Width}x{rectangle.Height}"),
    triangle => Task.FromResult($"Triangle base {triangle.Base}")
);

// Use MatchAsync on Task<Shape>
Task<Shape> shapeTask = GetShapeAsync();
string result = await shapeTask.MatchAsync(
    async circle => {
        await LogAsync($"Processing circle");
        return $"Circle with radius {circle.Radius}";
    },
    async rectangle => {
        await LogAsync($"Processing rectangle");
        return $"Rectangle {rectangle.Width}x{rectangle.Height}";
    },
    async triangle => {
        await LogAsync($"Processing triangle");
        return $"Triangle base {triangle.Base}";
    }
);
```

### MatchAsync without return value

```csharp
// Async Match without TResult - handlers return Task
Task<ApiResponse<User>> responseTask = FetchUserAsync(userId);

await responseTask.MatchAsync(
    async success => {
        await DisplayUserAsync(success.Data);
        await LogAsync("User displayed");
    },
    async error => {
        await ShowErrorAsync(error.Message);
        await LogAsync($"Error: {error.Message}");
    },
    async loading => await ShowSpinnerAsync(),
    async notStarted => await ShowPlaceholderAsync()
);
```

### Mixing sync and async handlers

```csharp
// Sync handlers with async Task - handlers return TResult (not Task<TResult>)
Task<PaymentMethod> paymentTask = GetPaymentMethodAsync();

decimal fee = await paymentTask.MatchAsync(
    creditCard => 0.029m,      // Sync calculation
    payPal => 0.034m,           // Sync calculation
    bankTransfer => 0m,         // Sync calculation
    crypto => 0.01m             // Sync calculation
);
```

### Real-world async example

```csharp
public record CreateUserCmd(string Email, string Name);
public record UpdateUserCmd(int Id, string Name);
public record DeleteUserCmd(int Id);

public abstract partial record UserCommand
    : IUnion<CreateUserCmd, UpdateUserCmd, DeleteUserCmd>;

// Process command asynchronously
Task<ResultOf<string, string>> ProcessCommandAsync(UserCommand command) =>
    command.MatchAsync(
        async create => await _userService.CreateAsync(create.Email, create.Name),
        async update => await _userService.UpdateAsync(update.Id, update.Name),
        async delete => await _userService.DeleteAsync(delete.Id)
    );

// Chain with Task
Task<UserCommand> commandTask = ReceiveCommandAsync();
var result = await commandTask.MatchAsync(
    async create => {
        var user = await _userService.CreateAsync(create.Email, create.Name);
        await _audit.LogAsync($"Created user: {user.Id}");
        return ResultOf.Ok<string, string>($"User {user.Id} created");
    },
    async update => {
        await _userService.UpdateAsync(update.Id, update.Name);
        await _audit.LogAsync($"Updated user: {update.Id}");
        return ResultOf.Ok<string, string>($"User {update.Id} updated");
    },
    async delete => {
        await _userService.DeleteAsync(delete.Id);
        await _audit.LogAsync($"Deleted user: {delete.Id}");
        return ResultOf.Ok<string, string>($"User {delete.Id} deleted");
    }
);
```

### Pipeline integration with Pipe

```csharp
var result = await GetUserIdAsync()
    .PipeAsync(FetchUserCommandAsync)           // Task<UserCommand>
    .MatchAsync(                                 // Extension on Task<UserCommand>
        async create => await ProcessCreateAsync(create),
        async update => await ProcessUpdateAsync(update),
        async delete => await ProcessDeleteAsync(delete)
    )
    .Pipe(LogResult);                           // Chain sync operation after async
```

## Generated Code

The source generator creates `Match` methods and async extensions for your union type. For example:

```csharp
public record Circle(double Radius);
public record Rectangle(double Width, double Height);

public abstract partial record Shape : IUnion<Circle, Rectangle>;

// Generates (inside Shape):
public sealed partial record Circle(global::Circle Value) : Shape
{
    public override string ToString() => Value?.ToString() ?? "Circle";
}

public sealed partial record Rectangle(global::Rectangle Value) : Shape
{
    public override string ToString() => Value?.ToString() ?? "Rectangle";
}

public static implicit operator Shape(global::Circle value) => new Circle(value);
public static implicit operator Shape(global::Rectangle value) => new Rectangle(value);

public bool IsCircle => this is Circle;
public bool IsRectangle => this is Rectangle;

public bool TryGetCircle([NotNullWhen(true)] out global::Circle? value) { /* ... */ }
public bool TryGetRectangle([NotNullWhen(true)] out global::Rectangle? value) { /* ... */ }

public TResult Match<TResult>(
    Func<global::Circle, TResult> onCircle,
    Func<global::Rectangle, TResult> onRectangle)
{
    return this switch
    {
        Circle wrapped => onCircle(wrapped.Value),
        Rectangle wrapped => onRectangle(wrapped.Value),
        _ => throw new InvalidOperationException("Invalid union state")
    };
}

public void Match(
    Action<global::Circle> onCircle,
    Action<global::Rectangle> onRectangle)
{
    switch (this)
    {
        case Circle wrapped:
            onCircle(wrapped.Value);
            break;
        case Rectangle wrapped:
            onRectangle(wrapped.Value);
            break;
        default:
            throw new InvalidOperationException("Invalid union state");
    }
}

public async Task<TResult> MatchAsync<TResult>(
    Func<global::Circle, Task<TResult>> onCircle,
    Func<global::Rectangle, Task<TResult>> onRectangle)
{
    return this switch
    {
        Circle wrapped => await onCircle(wrapped.Value),
        Rectangle wrapped => await onRectangle(wrapped.Value),
        _ => throw new InvalidOperationException("Invalid union state")
    };
}

public async Task MatchAsync(
    Func<global::Circle, Task> onCircle,
    Func<global::Rectangle, Task> onRectangle)
{
    switch (this)
    {
        case Circle wrapped:
            await onCircle(wrapped.Value);
            break;
        case Rectangle wrapped:
            await onRectangle(wrapped.Value);
            break;
        default:
            throw new InvalidOperationException("Invalid union state");
    }
}

// Generates (ShapeUnionExtensions static class):
public static class ShapeUnionExtensions
{
    // MatchAsync on Task<Shape> with async handlers returning Task<TResult>
    public static async Task<TResult> MatchAsync<TResult>(
        this Task<Shape> task,
        Func<global::Circle, Task<TResult>> onCircle,
        Func<global::Rectangle, Task<TResult>> onRectangle)
    {
        var result = await task;
        return await result.MatchAsync(onCircle, onRectangle);
    }

    // MatchAsync on Task<Shape> with async handlers returning Task
    public static async Task MatchAsync(
        this Task<Shape> task,
        Func<global::Circle, Task> onCircle,
        Func<global::Rectangle, Task> onRectangle)
    {
        var result = await task;
        await result.MatchAsync(onCircle, onRectangle);
    }

    // MatchAsync on Task<Shape> with sync handlers returning TResult
    public static async Task<TResult> MatchAsync<TResult>(
        this Task<Shape> task,
        Func<global::Circle, TResult> onCircle,
        Func<global::Rectangle, TResult> onRectangle)
    {
        var result = await task;
        return result.Match(onCircle, onRectangle);
    }
}
```

## Performance

- ✅ **No reflection** - All matching is compile-time type checks
- ✅ **Efficient dispatch** - Both `Match()` and `switch` compile to the same type checks
- ✅ **Inlining** - JIT can inline simple match expressions
- ℹ️ **Allocation** - Each case wrapper is a record, so constructing one is a heap allocation
  (~24 bytes). These are short-lived gen0 objects, which the .NET GC handles very cheaply. Long
  `Map`/`Bind` chains allocate one intermediate per step; that only matters in measured hot paths.

Cases are reference types by design: it is what makes the hierarchy closed and keeps an
invalid union state unrepresentable. A struct-based union cannot inherit, so it would have
to carry every case's fields at once (larger, copied on every call) or box its payload into
an `object?` (which allocates anyway).

## Diagnostics

| ID | Severity | Meaning |
| --- | --- | --- |
| `UNION001` | Error | Union generation failed (internal generator error) |
| `UNION004` | Warning | A `switch` does not handle every union case *(has a code fix)* |
| `UNION005` | *(suppressor)* | Suppresses `CS8509` when every case is handled |
| `UNION006` | *(suppressor)* | Suppresses `IDE0010` ("populate switch statement") when every case is handled |
| `UNION007` | *(suppressor)* | Suppresses `IDE0072` ("populate switch expression") when every case is handled |
| `UNION008` | Error | Case types resolve to the same wrapper name, even after namespace-prefix disambiguation |
| `UNION009` | Warning | Two cases share one CLR type once tuple/generic erasure is applied, so implicit conversions were omitted for the **whole union**, not just the colliding pair |
| `UNION012` | Error | A case type is an interface, which C# forbids as the target of a user-defined conversion |
| `UNION013` | Error | A root implements more than one `IUnion<...>` marker interface; nothing is generated for it |
| `UNION014` | Error | A root implementing `IUnion<...>` is not declared `abstract partial`; nothing is generated for it |

`UNION001` is a structural error and cannot be configured away: without it the generator cannot
report that it failed to emit valid code. `UNION004`, `UNION008`, `UNION009`, `UNION012`,
`UNION013`, and `UNION014` are normal analyzer rules and can be tuned through `.editorconfig`,
though tuning `UNION008`/`UNION009`/`UNION012`/`UNION013`/`UNION014` down does not make the
underlying shape any more legal - they diagnose conditions the generator cannot emit working code
for, not style preferences.

### `UNION013`: more than one `IUnion<...>` marker

A base list has no `AllowMultiple = false`-style guard the way the retired `[Union]` attribute
did, so nothing stops a root from naming two markers:

```csharp
// error UNION013: Type 'Pet' implements more than one IUnion<...> marker interface
// (Corsinvest.Fx.Functional.IUnion<Cat, Dog>, Corsinvest.Fx.Functional.IUnion<Cat, Dog, Cow>);
// a union root must implement exactly one, so nothing was generated for it.
public abstract partial record Pet : IUnion<Cat, Dog>, IUnion<Cat, Dog, Cow>;
```

Keep exactly one `IUnion<...>` in the base list.

### `UNION014`: root must be `abstract partial`

The generator always emits `public abstract partial record {Root}` for the root's second partial
declaration, so a root that omits either keyword does not mean what it says - `abstract` would be
silently added on the generator's side, and a missing `partial` fails with a bare `CS0260` that
does not explain why `IUnion<...>` requires it:

```csharp
// error UNION014: Type 'Pet' implements IUnion<...> but is not declared 'abstract partial'; add
// the missing abstract keyword(s) so the declaration means what the generated code assumes.
public partial record Pet : IUnion<Cat, Dog>;
```

Declare the root `public abstract partial record Pet : IUnion<Cat, Dog>;`.

### `UNION009`: implicit conversions omitted for the whole union

Two cases can have distinct wrapper names yet still erase to the *same* CLR type once generics
and tuple element names are stripped away - for example two named tuples that both become
`ValueTuple<int, int>`. The generator cannot emit two `implicit operator Root(ValueTuple<int,
int>)` overloads (that is `CS0557`, ambiguous user-defined conversions), so it falls back to a
single all-or-nothing switch: `EmitImplicitConversions` is one bool for the entire union, not one
per case. When any two cases collide, **no case in that union gets an implicit conversion** -
including cases whose CLR type collides with nothing at all - and the generator reports
**UNION009** once, naming the colliding pair, instead of silently emitting a broken subset. In a
union with three or more cases this is easy to misread as "only the colliding pair loses its
conversion" - it is not. Construct **every** case wrapper directly with `new Root.CaseName(value)`
once the union has any collision, even for the cases that were never part of it.

```csharp
// (int, int) and (int X, int Y) both erase to ValueTuple<int, int>. Only the unnamed tuple can
// take an explicit override here - a named tuple cannot be an attribute type argument (CS8970),
// and [UnionCaseName<T>] is itself an attribute.
[UnionCaseName<(int, int)>("Point")]
public abstract partial record Geo : IUnion<(int, int), (int X, int Y)>;
// warning UNION009: Union 'Geo' has case types 'Point, TupleOfInt32Int32' that share one CLR type...
```

## See Also

- [ResultOf<T, E>](ResultOf.md) - Combine with Result for error handling
- [Option<T>](Option.md) - Built-in union for optional values
- [Pipe Extensions](Pipe.md) - Chain transformations on union values
- [04_UnionTypes.cs](../../../../examples/04_UnionTypes.cs) - Runnable example: payment methods, API states, shapes, and a mixed-type union (class, enum, struct, string)
