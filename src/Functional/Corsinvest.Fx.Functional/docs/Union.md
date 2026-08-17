# Union Types - Discriminated Unions

**Create type-safe union types with source generators**

## Overview

The `[Union]` attribute enables you to create discriminated unions (also known as sum types or tagged unions) in C#. Discriminated unions represent a value that can be one of several named cases, each potentially with different data.

The source generator creates:

- Type-safe construction
- Exhaustive pattern matching via `Match()` method
- Native `switch` support with compile-time exhaustiveness checking (see [Switch Expressions](#switch-expressions))
- `Is{Case}` properties and `TryGet{Case}` methods

## Basic Usage

### Defining a Union Type

```csharp
using Corsinvest.Fx.Functional;

[Union]
public partial record Shape
{
    public partial record Circle(double Radius);
    public partial record Rectangle(double Width, double Height);
    public partial record Triangle(double Base, double Height);
}
```

**Requirements:**

- The union type must be `partial`
- All cases must be `partial record`
- Cases must be nested inside the union type

### Pattern Matching

```csharp
double CalculateArea(Shape shape) => shape.Match(
    circle => Math.PI * circle.Radius * circle.Radius,
    rectangle => rectangle.Width * rectangle.Height,
    triangle => 0.5 * triangle.Base * triangle.Height
);

// Usage
var circle = new Shape.Circle(5.0);
var area = CalculateArea(circle); // 78.54
```

The `Match()` method is **exhaustive** - you must handle all cases, or it won't compile.

## The Generic Form (Union of T1 to T8)

`[Union]` requires the cases to be declared *inside* the union - they exist only as part of it.
`[Union<T1..T8>]` takes the opposite approach: the cases are ordinary, independently declared
types, and the attribute just names the closed set.

```csharp
using Corsinvest.Fx.Functional;

// Ordinary, standalone types - no attribute, no partial, reusable anywhere.
public record CreditCard(string Number, string Cvv, DateTime Expiry);
public record PayPal(string Email);
public record BankTransfer(string Iban, string Bic);

[Union<CreditCard, PayPal, BankTransfer>]
public abstract partial record PaymentMethod;
```

The generator emits, per case, a `sealed partial record` wrapper nested inside the root:

```csharp
public sealed partial record PaymentMethod.CreditCard(CreditCard Value) : PaymentMethod;
```

plus an implicit conversion from the case type, an `Is{Case}` property, a `TryGet{Case}` method,
and `Match`/`Match` (void)/`MatchAsync` overloads - the same member set `[Union]` generates, just
built from external types instead of nested ones:

```csharp
PaymentMethod method = new CreditCard("4111-1111-1111-1111", "123", DateTime.UtcNow);
                     // ^ implicit conversion from the case type

var fee = method.Match(
    onCreditCard: card => 0.029m,
    onPayPal: payPal => 0.034m,
    onBankTransfer: transfer => 0m
);

if (method.TryGetCreditCard(out var creditCard))
{
    Console.WriteLine(creditCard.Number);
}

var description = method switch
{
    PaymentMethod.CreditCard(var card) => $"Card ending in {card.Number[^4..]}",
    PaymentMethod.PayPal(var payPal) => $"PayPal {payPal.Email}",
    PaymentMethod.BankTransfer(var transfer) => $"Bank transfer to {transfer.Iban}"
};
```

Note one difference from `[Union]`: the generic form does not generate the `Task<TUnion>`
extension class (the fluent `someTask.MatchAsync(...)` shown in
[Async Pattern Matching](#async-pattern-matching)) - only the instance `Match`/`MatchAsync`
methods shown above. Everything else - `UNION004`/`005`/`006`/`007`, the closed hierarchy, the
`switch` support - applies identically to both forms.

**Case types can be almost anything**: classes, sealed classes, records, structs, record structs,
enums, delegates, `string`, `int` and other primitives, arrays, and closed generics (`List<T>`,
`Dictionary<K,V>`, and so on). Value-type cases are held in a typed field on their wrapper - a
`struct Money { public decimal Amount; }` case ends up as `Root.Money.Value` of type `Money`, not
`object` - so **nothing is boxed**, the same guarantee `[Union]` gives, extended to external value
types.

Two shapes do **not** work as case types today, both driven by plain C# rules rather than
anything the generator controls:

- **Interfaces** - the generator always emits an implicit conversion operator for each case, and
  C# forbids user-defined conversions to or from an interface (`CS0552`). An interface case type
  fails the build unconditionally.
- **Named tuples** (`(int X, int Y)`) - a tuple with element names cannot appear as an attribute
  type argument at all (`CS8970`); this is a general C# restriction on attributes, unrelated to
  unions. An *unnamed* tuple, `(int, int)`, works fine.

### Which form should I use?

|  | `[Union]` (nested) | `[Union<T1..T8>]` (generic) |
| --- | --- | --- |
| Case types | exist only inside the union | ordinary, independently declared |
| Reuse across unions | ❌ one union per case type, permanently | ✅ the same type can be a case in several unions |
| Cases closing over the root's own type parameter | ✅ only form that can express this | ❌ **CS8968** - illegal, see below |
| Declaration style | nested `partial record` per case | attribute type arguments + `[UnionCaseName<T>]` overrides |

Reach for the **generic form** when the cases are types you would plausibly declare and reuse on
their own - request/response payloads, domain entities, anything that exists independently of the
union.

Reach for the **nested form** when a case exists purely as part of the union, or - the one place
the generic form cannot go at all - when a case needs to **close over the union root's own type
parameter**. `Option<T>`'s `Some(T Value)` and `ResultOf<T, E>`'s `Ok(T Value)` / `Fail(E Error)`
are exactly this shape:

```csharp
// This does NOT compile: error CS8968 - an attribute type argument may not reference the
// decorated type's own type parameter.
// [Union<Some<T>, None>]
// public abstract partial record Option<T>;
```

`Some<T>` only exists as `Option<T>.Some`, so it can only be spelled with `T` still in scope -
which is exactly what an attribute argument cannot do. There is no generator change that works
around this; it is enforced by the compiler before the generator ever runs. That is why
`Option<T>` and `ResultOf<T, E>` stay on `[Union]` (see [Option<T>](Option.md) and
[ResultOf<T, E>](ResultOf.md)) - the two attributes are permanent alternatives, not a
migration path from one to the other.

### Wrapper names

Each case gets a wrapper name, derived from the case type unless overridden. The rules, checked
in order:

1. **The case type's own short name**, by default - `CreditCard` → `PaymentMethod.CreditCard`.
2. **Namespace-prefixed**, when two cases' short names collide - `Farm.Cat` and `Wild.Cat` both
   start as `Cat`, so they become `FarmCat` and `WildCat`.
3. **`ListOf`/`DictionaryOf`-style names for closed generics** - `List<string>` →
   `ListOfString`, `Dictionary<string, int>` → `DictionaryOfStringInt32`.
4. **`{Element}Array` for arrays** - `int[]` → `Int32Array`.
5. **`TupleOf...` for (unnamed) tuples** - `(int, int)` → `TupleOfInt32Int32`.
6. **CLR names, not keywords** - `int` → `Int32`, `string` → `String`, so the wrapper is always a
   legal identifier.
7. **`[UnionCaseName<T>("...")]` always wins** - an explicit override is applied before any of the
   rules above are considered, for that case type.

```csharp
namespace Farm { public record Cat(string Name); }
namespace Wild { public record Cat(string Species); }

// Both named "Cat" - rule 2 (namespace prefix) resolves the collision automatically.
[Union<Farm.Cat, Wild.Cat>]
public abstract partial record Feline;
// => Feline.FarmCat, Feline.WildCat

// Overriding explicitly (rule 7) instead of relying on the namespace prefix:
[Union<Farm.Cat, Wild.Cat>]
[UnionCaseName<Farm.Cat>("Domestic")]
[UnionCaseName<Wild.Cat>("Feral")]
public abstract partial record Feline2;
// => Feline2.Domestic, Feline2.Feral
```

If two cases still collide after the namespace prefix - most often because the exact same type
is listed twice - the generator cannot invent a third name and reports **UNION008**:

```csharp
public record Cat(string Name);

[Union<Cat, Cat>]                 // error UNION008: case types resolve to the same wrapper name
public abstract partial record Pet;
```

#### Generic roots need explicit names

On a **generic** root (`Box<T>`, not just `Box`), a wrapper name equal to its own case type's
name is rejected differently: the nested wrapper `Box<T>.Cat` shares the attribute's own lookup
scope, so it **shadows** the top-level `Cat` the attribute argument needs to resolve - and the
compiler rejects the whole declaration with `CS8968`, even though `Cat` never mentions `T`.

```csharp
public record Cat(string Name);
public record Dog(string Name);

// error CS8968, even though neither Cat nor Dog mentions T:
// the default wrapper name "Cat" shadows the case type "Cat" in the attribute's own scope.
[Union<Cat, Dog>]
public abstract partial record Box<T>;
```

**UNION010** catches this before it reaches the compiler's own, less helpful error, and names the
fix:

```csharp
[Union<Cat, Dog>]
[UnionCaseName<Cat>("CatCase")]   // required on a generic root
[UnionCaseName<Dog>("DogCase")]
public abstract partial record Box<T>;
```

With the overrides in place, `Box<T>` compiles and works like any other generic union:

```csharp
Box<int> box = new Cat("Whiskers");
var name = box.Match(
    onCatCase: cat => cat.Name,
    onDogCase: dog => dog.Name
);
```

#### `UNION009`: implicit conversions omitted for duplicate CLR types

Two cases can have distinct wrapper names yet still erase to the *same* CLR type once generics
and tuple element names are stripped away - for example two named tuples that both become
`ValueTuple<int, int>`. The generator cannot emit two `implicit operator Root(ValueTuple<int,
int>)` overloads (that is `CS0557`, ambiguous user-defined conversions), so when this happens it
omits the implicit conversion for **every** case that shares the CLR type and reports
**UNION009** instead - construct those wrappers directly with `new Root.CaseName(value)`.
In practice this is hard to hit with hand-written code, because named tuples cannot appear as
attribute type arguments at all (`CS8970`, above); it mainly guards against generic case types
whose type arguments happen to collide after erasure.

## Switch Expressions

Union cases are real nested types, so you can use a plain C# `switch` instead of `Match()`:

```csharp
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle(var radius) => Math.PI * radius * radius,
    Shape.Rectangle(var width, var height) => width * height,
    Shape.Triangle(var b, var h) => 0.5 * b * h
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

`[Union]` hierarchies **are** closed - the generator emits a private constructor on the root and seals every case, so nothing outside can derive from it. The package ships two analyzers that act on this:

| ID | Kind | What it does |
| --- | --- | --- |
| **UNION004** | Warning | Reports union cases a `switch` does not handle, **by name** - with a code fix that adds them |
| **UNION005** | Suppressor | Suppresses `CS8509` when every case is handled, so no discard arm is needed |
| **UNION006/007** | Suppressor | Suppresses the IDE's "Add default case" suggestions (`IDE0010`, `IDE0072`) for the same reason |

The suppressions matter as much as the warning: `CS8509`, `IDE0010` and `IDE0072` all steer you
toward the discard arm that hides future cases. On a closed union that arm is unreachable code,
so the package stands those suggestions down and lets UNION004 speak instead.

All four apply identically to `[Union<T1..T8>]` unions - the generic form emits the same sealed,
closed hierarchy, so the same analyzers recognize it. See [Diagnostics](#diagnostics) for the
generic-only diagnostics, `UNION008`-`UNION010`.

Together they invert the default behaviour: the discard arm becomes unnecessary, and a missing case becomes loud.

```csharp
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle(var radius) => Math.PI * radius * radius,
    Shape.Rectangle(var w, var h) => w * h
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
    Shape.Circle(var radius) => Math.PI * radius * radius
};

// after
double CalculateArea(Shape shape) => shape switch
{
    Shape.Circle(var radius) => Math.PI * radius * radius,
    Shape.Rectangle(var width, var height) => throw new System.NotImplementedException(),
    Shape.Triangle(var @base, var height) => throw new System.NotImplementedException()
};
```

Each added arm deconstructs its case, so the data is already in scope with names taken from the
record's positional members - keywords are escaped (`var @base`). A case with no positional
members gets a bare type pattern instead. The body is `throw new NotImplementedException()`, so an
arm you forget to finish fails loudly rather than returning a plausible default.

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
| `Shape.Circle(var r)` | ✅ all subpatterns irrefutable |
| `Shape.Circle(_)` | ✅ |
| `Shape.Circle or Shape.Rectangle` | ✅ both |
| `Shape.Circle c when c.Radius > 0` | ❌ guarded, can fail |
| `Shape.Circle { Radius: 5 }` | ❌ matches a subset |

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

`[Union]` is a **tag union**: a closed hierarchy where the cases are declared inside the union and
exist only as part of it.

```csharp
[Union]
public partial record Pet
{
    public partial record Cat(string Name);
    public partial record Dog(string Name);
}
```

`[Union<T1..T8>]` narrows that gap: the cases are ordinary, independently declared types, and the
attribute just names the closed set - the same composition C# 15 offers, kept inside this
package's closed-hierarchy model:

```csharp
public record Cat(string Name);          // a normal, standalone type
public record Dog(string Name);

[Union<Cat, Dog>]
public abstract partial record Pet;
```

C# 15 is a **type union**: it composes types that already exist independently, the same way, but
with a different runtime shape underneath.

```csharp
public record Cat(string Name);          // a normal, standalone type
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

The compiler then *requires* a `null` arm in every switch. With `[Union]`, the root's private
constructor and sealed cases make that state unrepresentable - there is no `null` arm to write.

**No boxing.** Because `Value` is `object?`, a value-type case is boxed on assignment - the docs
are explicit that the generated form "always boxes value-type cases". A `union IntOrString(int, string)`
allocates 24 bytes on the heap to hold a 4-byte `int`, so the struct meant to avoid allocation
allocates anyway. `[Union]` cases hold their data in typed fields, with no boxing at any point.

(C# 15 offers a way out, but you have to build it yourself: a hand-written union implementing the
*non-boxing access pattern* - `HasValue` plus a `TryGetValue` per case, over your own tag and
fields - which the compiler then prefers over `Value`. That is the tagged-struct design written by
hand; the `union` keyword gives you the `switch` syntax, not the storage.)

**Async matching.** `MatchAsync` overloads are generated for you on both forms; `[Union]` also
generates `Task<TUnion>` extensions for fluent chaining (see [Which form should I use?](#which-form-should-i-use)
for the one difference between the two). `switch` is not awaitable, so the C# 15 model has no
equivalent - you write that plumbing yourself.

**Available today**, on net8.0 and net9.0, with no preview SDK.

### Where C# 15 is still stronger

**Ad hoc unions.** `(A or B or C) x = ...` composes a union inline, with no declaration. A source
generator cannot offer that, in either form.

**Native and dependency-free** - language syntax, with IDE and debugger support built in, no
package reference and no build-time source generator.

A type belonging to several unions - the gap called out in earlier versions of this comparison -
is no longer C# 15's alone: `[Union<T1..T8>]` gives this package the same composability, for cases
that do not need to close over a generic root's own type parameter. `Cat` can be
`[Union<Cat, Dog>]`'s `Pet` and `[Union<Cat, Cow>]`'s `Animal` at once, exactly like
`union Pet(Cat, Dog)` and `union Animal(Cat, Cow)` would let it. The nested `[Union]` form keeps
the older restriction - a case declared inside a union belongs to exactly that union, permanently
- because that is what lets it close over the root's type parameter at all (see
[Which form should I use?](#which-form-should-i-use)).

### Side by side

| | `[Union]` (nested) | `[Union<T1..T8>]` (generic) | C# 15 `union` |
| --- | --- | --- | --- |
| Invalid state | **impossible** | **impossible** | `default` has a null `Value` |
| Boxing of value-type cases | **never** | **never** | always (unless you hand-write a non-boxing union) |
| Indirection to reach the data | 1 hop | 1 hop | 2 hops (`Value`, then the object) |
| Cases usable as types | ✅ `Pet.Cat` | ✅ `Pet.Cat`, plus the standalone `Cat` | ✅ standalone types |
| Same type in several unions | ❌ | ✅ | ✅ |
| Cases closing over the root's own type parameter | ✅ only form that can | ❌ CS8968 | n/a - not generic itself |
| Ad hoc unions | ❌ | ❌ | ✅ |
| Async matching | ✅ `MatchAsync` + `Task<TUnion>` extensions | ✅ `MatchAsync` (no `Task<TUnion>` extensions) | ❌ |
| Exhaustive `switch` | ✅ via UNION004/005 | ✅ via UNION004/005 | ✅ built into the compiler |
| Available on | net8.0+ | net8.0+ | .NET 11 |

Both attributes still buy the closed-hierarchy correctness C# 15's struct-backed union does not
have (no invalid state, no boxing); the generic form adds most of C# 15's composability on top,
while the nested form remains the only way to express a case that closes over the root's own type
parameter. What is left, on either side, is ad hoc unions and native language syntax - and
`Option<T>` / `ResultOf<T, E>` are exactly the case where the nested form's restriction (one union
per case, permanently) is not a cost, because their cases were never meant to be reused outside
them.

### Related: the `closed` modifier

C# 15 also adds `closed`, which restricts derivation to the declaring assembly so the compiler can
check exhaustiveness itself:

```csharp
public closed record class JobStatus;
public record class Queued : JobStatus;
public record class Failed(string Error) : JobStatus;
```

This is the mechanism UNION004 and UNION005 emulate today. A `[Union]` hierarchy is already closed
in fact - a private constructor makes external derivation a compile error - but Roslyn does not
infer exhaustiveness from that, which is why the analyzers exist.

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
[Union]
public partial record PaymentMethod
{
    public partial record CreditCard(string Number, string Cvv, DateTime Expiry);
    public partial record PayPal(string Email);
    public partial record BankTransfer(string Iban, string Bic);
    public partial record Cryptocurrency(string WalletAddress, string Currency);
}

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
[Union]
public partial record ApiResponse<T>
{
    public partial record Success(T Data, int StatusCode);
    public partial record Error(string Message, int StatusCode);
    public partial record Loading();
    public partial record NotStarted();
}

void HandleUserResponse(ApiResponse<User> response) => response.Match(
    success => DisplayUser(success.Data),
    error => ShowError(error.Message),
    loading => ShowSpinner(),
    notStarted => ShowPlaceholder()
);

// Usage with state management
ApiResponse<User> userState = new ApiResponse<User>.Loading();
userState = await FetchUser()
    ? new ApiResponse<User>.Success(user, 200)
    : new ApiResponse<User>.Error("Not found", 404);
```

### Log Levels with Context

```csharp
[Union]
public partial record LogEntry
{
    public partial record Info(string Message, DateTime Timestamp);
    public partial record Warning(string Message, string Details, DateTime Timestamp);
    public partial record Error(string Message, Exception Exception, DateTime Timestamp);
    public partial record Debug(string Message, Dictionary<string, object> Context, DateTime Timestamp);
}

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

### File System Operations

```csharp
[Union]
public partial record FileSystemEntry
{
    public partial record File(string Path, long Size, DateTime Modified);
    public partial record Directory(string Path, DateTime Modified);
    public partial record SymbolicLink(string Path, string Target);
    public partial record NotFound(string Path);
}

long GetSize(FileSystemEntry entry) => entry.Match(
    file => file.Size,
    directory => CalculateDirectorySize(directory.Path),
    symLink => GetSize(ReadLink(symLink.Target)),
    notFound => 0L
);

string GetIcon(FileSystemEntry entry) => entry.Match(
    file => "📄",
    directory => "📁",
    symLink => "🔗",
    notFound => "❌"
);
```

### Command Pattern

```csharp
[Union]
public partial record Command
{
    public partial record CreateUser(string Email, string Name);
    public partial record UpdateUser(int Id, string Name);
    public partial record DeleteUser(int Id);
    public partial record SendEmail(int UserId, string Subject, string Body);
}

async Task<ResultOf<string, string>> ExecuteCommand(Command command) =>
    await command.Match(
        create => CreateUserAsync(create.Email, create.Name),
        update => UpdateUserAsync(update.Id, update.Name),
        delete => DeleteUserAsync(delete.Id),
        sendEmail => SendEmailAsync(sendEmail.UserId, sendEmail.Subject, sendEmail.Body)
    );
```

### State Machine

```csharp
[Union]
public partial record OrderState
{
    public partial record Draft(List<OrderItem> Items);
    public partial record Submitted(int OrderId, DateTime SubmittedAt);
    public partial record Processing(int OrderId, string Status);
    public partial record Shipped(int OrderId, string TrackingNumber, DateTime ShippedAt);
    public partial record Delivered(int OrderId, DateTime DeliveredAt);
    public partial record Cancelled(int OrderId, string Reason, DateTime CancelledAt);
}

OrderState TransitionState(OrderState current, OrderEvent event) =>
    (current, event) switch
    {
        (OrderState.Draft draft, SubmitEvent) =>
            new OrderState.Submitted(SaveOrder(draft.Items), DateTime.UtcNow),

        (OrderState.Submitted submitted, ProcessEvent) =>
            new OrderState.Processing(submitted.OrderId, "Processing payment"),

        (OrderState.Processing processing, ShipEvent ship) =>
            new OrderState.Shipped(processing.OrderId, ship.TrackingNumber, DateTime.UtcNow),

        (OrderState.Shipped shipped, DeliverEvent) =>
            new OrderState.Delivered(shipped.OrderId, DateTime.UtcNow),

        (_, CancelEvent cancel) =>
            new OrderState.Cancelled(GetOrderId(current), cancel.Reason, DateTime.UtcNow),

        _ => current // Invalid transition, stay in current state
    };
```

## Advanced Patterns

### Union with Empty Cases

```csharp
[Union]
public partial record NetworkStatus
{
    public partial record Connected();
    public partial record Connecting();
    public partial record Disconnected();
    public partial record Error(string Message);
}

// Empty cases don't need parameters
var status = new NetworkStatus.Connected();
```

### Nested Unions

```csharp
[Union]
public partial record Result
{
    public partial record Success(string Message);
    public partial record Warning(string Message, string Details);
    public partial record Error(string Message, ErrorDetails Details);
}

[Union]
public partial record ErrorDetails
{
    public partial record Validation(List<string> Errors);
    public partial record Network(int StatusCode);
    public partial record Internal(Exception Exception);
}

void LogResult(Result result) => result.Match(
    success => Console.WriteLine($"✓ {success.Message}"),
    warning => Console.WriteLine($"⚠ {warning.Message}: {warning.Details}"),
    error => error.Details.Match(
        validation => Console.WriteLine($"✗ Validation: {string.Join(", ", validation.Errors)}"),
        network => Console.WriteLine($"✗ Network error: HTTP {network.StatusCode}"),
        internal => Console.WriteLine($"✗ Internal error: {internal.Exception.Message}")
    )
);
```

### Generic Unions

```csharp
[Union]
public partial record Option<T>
{
    public partial record Some(T Value);
    public partial record None();
}

Option<int> ParseInt(string input) =>
    int.TryParse(input, out var result)
        ? new Option<int>.Some(result)
        : new Option<int>.None();

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
[Union]
public partial record PaymentStatus
{
    public partial record Pending(DateTime CreatedAt);
    public partial record Completed(DateTime CompletedAt, string TransactionId);
    public partial record Failed(DateTime FailedAt, string Reason);
}

// ❌ Bad - unclear
[Union]
public partial record PaymentStatus
{
    public partial record Status1(DateTime Time);
    public partial record Status2(DateTime Time, string Id);
    public partial record Status3(DateTime Time, string Reason);
}
```

### 2. Include Relevant Data in Each Case

```csharp
// ✅ Good - each case has the data it needs
[Union]
public partial record HttpResponse
{
    public partial record Success(string Body, int StatusCode, Dictionary<string, string> Headers);
    public partial record Redirect(string Location, int StatusCode);
    public partial record ClientError(string Message, int StatusCode);
    public partial record ServerError(string Message, int StatusCode, string? StackTrace);
}

// ❌ Bad - forcing all data into all cases
[Union]
public partial record HttpResponse
{
    public partial record Response(string? Body, string? Location, string? Message, int StatusCode);
}
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
[Union]
public partial record ValidationError
{
    public partial record Required(string FieldName);
    public partial record TooShort(string FieldName, int MinLength);
    public partial record InvalidFormat(string FieldName, string Expected);
}

ResultOf<User, ValidationError> ValidateUser(string email, string name)
{
    if (string.IsNullOrEmpty(email))
        return ResultOf.Fail<User, ValidationError>(
            new ValidationError.Required("email"));

    if (name.Length < 2)
        return ResultOf.Fail<User, ValidationError>(
            new ValidationError.TooShort("name", 2));

    return ResultOf.Ok<User, ValidationError>(new User(email, name));
}
```

## Comparison with Other Patterns

| Pattern            | Type Safety | Exhaustiveness  | Extensibility         |
| ------------------ | ----------- | --------------- | --------------------- |
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

The generator also creates extension methods for `Task<TUnion>` to enable fluent async pattern matching:

### MatchAsync on Task<TUnion>

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
[Union]
public partial record Command
{
    public partial record CreateUser(string Email, string Name);
    public partial record UpdateUser(int Id, string Name);
    public partial record DeleteUser(int Id);
}

// Process command asynchronously
Task<ResultOf<string, string>> ProcessCommandAsync(Command command) =>
    command.MatchAsync(
        async create => await _userService.CreateAsync(create.Email, create.Name),
        async update => await _userService.UpdateAsync(update.Id, update.Name),
        async delete => await _userService.DeleteAsync(delete.Id)
    );

// Chain with Task
Task<Command> commandTask = ReceiveCommandAsync();
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
    .PipeAsync(FetchUserCommandAsync)           // Task<Command>
    .MatchAsync(                                 // Extension on Task<Command>
        async create => await ProcessCreateAsync(create),
        async update => await ProcessUpdateAsync(update),
        async delete => await ProcessDeleteAsync(delete)
    )
    .Pipe(LogResult);                           // Chain sync operation after async
```

## Generated Code

The source generator creates `Match` methods and async extensions for your union type. For example
(this is the nested `[Union]` form - see [The Generic Form](#the-generic-form-union-of-t1-to-t8) for what
`[Union<T1..T8>]` emits):

```csharp
[Union]
public partial record Shape
{
    public partial record Circle(double Radius);
    public partial record Rectangle(double Width, double Height);
}

// Generates (inside Shape class):
public TResult Match<TResult>(
    Func<Circle, TResult> onCircle,
    Func<Rectangle, TResult> onRectangle)
{
    return this switch
    {
        Circle circle => onCircle(circle),
        Rectangle rectangle => onRectangle(rectangle),
        _ => throw new InvalidOperationException("Invalid union state")
    };
}

public void Match(
    Action<Circle> onCircle,
    Action<Rectangle> onRectangle)
{
    switch (this)
    {
        case Circle circle:
            onCircle(circle);
            break;
        case Rectangle rectangle:
            onRectangle(rectangle);
            break;
        default:
            throw new InvalidOperationException("Invalid union state");
    }
}

public async Task<TResult> MatchAsync<TResult>(
    Func<Circle, Task<TResult>> onCircle,
    Func<Rectangle, Task<TResult>> onRectangle)
{
    return this switch
    {
        Circle circle => await onCircle(circle),
        Rectangle rectangle => await onRectangle(rectangle),
        _ => throw new InvalidOperationException("Invalid union state")
    };
}

public async Task MatchAsync(
    Func<Circle, Task> onCircle,
    Func<Rectangle, Task> onRectangle)
{
    switch (this)
    {
        case Circle circle:
            await onCircle(circle);
            break;
        case Rectangle rectangle:
            await onRectangle(rectangle);
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
        Func<Shape.Circle, Task<TResult>> onCircle,
        Func<Shape.Rectangle, Task<TResult>> onRectangle)
    {
        var result = await task;
        return await result.MatchAsync(onCircle, onRectangle);
    }

    // MatchAsync on Task<Shape> with async handlers returning Task
    public static async Task MatchAsync(
        this Task<Shape> task,
        Func<Shape.Circle, Task> onCircle,
        Func<Shape.Rectangle, Task> onRectangle)
    {
        var result = await task;
        await result.MatchAsync(onCircle, onRectangle);
    }

    // MatchAsync on Task<Shape> with sync handlers returning TResult
    public static async Task<TResult> MatchAsync<TResult>(
        this Task<Shape> task,
        Func<Shape.Circle, TResult> onCircle,
        Func<Shape.Rectangle, TResult> onRectangle)
    {
        var result = await task;
        return result.Match(onCircle, onRectangle);
    }
}

// Additional generated members:
public bool IsCircle => this is Circle;
public bool IsRectangle => this is Rectangle;

public bool TryGetCircle(out Circle circle) { /* ... */ }
public bool TryGetRectangle(out Rectangle rectangle) { /* ... */ }
```

## Performance

- ✅ **No reflection** - All matching is compile-time type checks
- ✅ **Efficient dispatch** - Both `Match()` and `switch` compile to the same type checks
- ✅ **Inlining** - JIT can inline simple match expressions
- ℹ️ **Allocation** - Cases are records, so each instance is a heap allocation (~24 bytes).
  These are short-lived gen0 objects, which the .NET GC handles very cheaply. Long `Map`/`Bind`
  chains allocate one intermediate per step; that only matters in measured hot paths.

Cases are reference types by design: it is what makes the hierarchy closed and keeps an
invalid union state unrepresentable. A struct-based union cannot inherit, so it would have
to carry every case's fields at once (larger, copied on every call) or box its payload into
an `object?` (which allocates anyway).

## Diagnostics

| ID | Severity | Meaning |
| --- | --- | --- |
| `UNION001` | Error | Union generation failed (internal generator error) |
| `UNION002` | Error | Type with `[Union]` must be declared `partial` |
| `UNION003` | Error | Union case must be declared `partial record` |
| `UNION004` | Warning | A `switch` does not handle every union case *(has a code fix)* |
| `UNION005` | *(suppressor)* | Suppresses `CS8509` when every case is handled |
| `UNION006` | *(suppressor)* | Suppresses `IDE0010` ("populate switch statement") when every case is handled |
| `UNION007` | *(suppressor)* | Suppresses `IDE0072` ("populate switch expression") when every case is handled |
| `UNION008` | Error | `[Union<T1..T8>]` case types resolve to the same wrapper name, even after namespace-prefix disambiguation |
| `UNION009` | Warning | Two `[Union<T1..T8>]` cases share one CLR type once tuple/generic erasure is applied, so their implicit conversions were omitted |
| `UNION010` | Warning | A generic root's default wrapper name shadows its own case type; add `[UnionCaseName<T>]` before the compiler reports `CS8968` |

`UNION001`-`UNION003` are structural errors and cannot be configured away: without them the
generator cannot emit valid code. `UNION004` and `UNION008`-`UNION010` are normal analyzer rules
and can be tuned through `.editorconfig`. `UNION008` is specific to `[Union<T1..T8>]`: nested
`[Union]` cases are literal, distinct type names, so they cannot collide the way generated wrapper
names can.

## See Also

- [ResultOf<T, E>](ResultOf.md) - Combine with Result for error handling
- [Option<T>](Option.md) - Built-in union for optional values
- [Pipe Extensions](Pipe.md) - Chain transformations on union values
- [04_UnionTypes.cs](../../../../examples/04_UnionTypes.cs) - Runnable example: payment methods, API states, shapes
