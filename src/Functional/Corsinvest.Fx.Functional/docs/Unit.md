# Unit - The Type With One Value

**A stand-in for `void`, usable everywhere a type is required**

## Overview

`void` is not a type in C#. It is a keyword that says a method returns nothing, and it cannot appear
as a type argument - `ResultOf<void, Exception>` does not compile.

That is a problem, because operations that produce no value fail just like any other: saving a file,
deleting a record, checking a stock level. `Unit` fills the hole:

```csharp
public readonly struct Unit
{
    public static readonly Unit Value = new();
}
```

No fields, one value, `Unit.Value`. It is the type that carries no information - which is exactly
what you want when there is no information to carry.

## Where it shows up

### `Try` over an `Action`

An action returns nothing but can still throw, so `Try` hands back the one thing worth knowing -
whether it threw:

```csharp
ResultOf<Unit, Exception> saved = TryHelper.Try(() => File.WriteAllText(path, content));

saved.Match(
    ok => Console.WriteLine("Saved"),
    fail => Console.WriteLine($"Failed: {fail.ErrorValue.Message}")
);
```

### A step that either passes or fails

A validation or a precondition has nothing to return on success. `Unit` says so, and keeps the step
in the same railway as every other:

```csharp
static ResultOf<Unit, OrderError> CheckInventory(Product product, int quantity)
    => product.Stock < quantity
        ? ResultOf.Fail<Unit, OrderError>(OrderError.InsufficientStock)
        : ResultOf.Ok<Unit, OrderError>(Unit.Value);

var result = ValidateOrder(order)
    .Bind(o => CheckInventory(o.Product, o.Quantity))
    .Bind(_ => ChargeCustomer(order))
    .Bind(_ => ShipOrder(order));
```

The `_` in each subsequent `Bind` is the tell: there is genuinely nothing to name.

## Why not `bool`

`ResultOf<bool, OrderError>` looks like it would work, and it is the usual first instinct. It does
not, for two reasons:

- **The `bool` is ambiguous.** Whether the operation succeeded is already `IsOk`. A second boolean
  invites the reader to ask what *else* it means - and eventually someone answers that question
  differently from you.
- **It admits states that cannot exist.** `Ok(false)` is a value the type permits and your code has
  no meaning for. `Unit` has exactly one value, so there is nothing to misread.

The same argument rules out `object`, `int`, or a placeholder `null`.

## Cost

A `readonly struct` with no fields. It does not allocate, and passing one copies nothing worth
measuring. Where it is a type argument to a generic - `ResultOf<Unit, E>` - the enclosing `ResultOf`
allocates as it always would; `Unit` adds nothing to it.

## Elsewhere

Every language with a type system that takes this seriously has the same type under a different
name: `unit` in F# and OCaml, `()` in Rust and Haskell, `Unit` in Scala and Kotlin. C# has `void`,
which is a keyword rather than a type - hence this one.

---

## See also

- **[Try Functions](./Try.md)** - where `Unit` most often appears
- **[ResultOf<T, E>](./ResultOf.md)** - the type `Unit` is usually a parameter of
