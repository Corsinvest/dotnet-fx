# Pipe Extensions - Functional Pipeline Composition

**Universal pipe operator for fluent data transformation in C#**

## Overview

Pipe extensions bring F#-style forward piping to C#, so a chain of transformations reads in the
order it runs. They are extension methods on `T` and on `Task<T>`, so they work on **any** type -
your own, the BCL's, primitives - with no interface to implement and no wrapper to allocate.

```csharp
using Corsinvest.Fx.Functional;
```

## The problem

Composing functions in C# nests them inside-out, so the first step to run is the last one you read:

```csharp
// ❌ Reads right-to-left, inside-out
var result = FormatOutput(AddPrefix(ToUpper(Trim(input))));
```

The usual workaround is a chain of locals, which names four things you do not care about:

```csharp
// ❌ Four throwaway names
var trimmed = Trim(input);
var upper = ToUpper(trimmed);
var prefixed = AddPrefix(upper);
var result = FormatOutput(prefixed);
```

## The solution

```csharp
// ✅ Reads in the order it runs
var result = input
    .Pipe(Trim)
    .Pipe(ToUpper)
    .Pipe(AddPrefix)
    .Pipe(FormatOutput);
```

---

## `Pipe` - transform

Applies a function to the value and returns its result. This is the whole idea; everything else is
a variation on it.

```csharp
var result = "hello"
    .Pipe(s => s.ToUpper())      // "HELLO"
    .Pipe(s => s + "!")          // "HELLO!"
    .Pipe(s => s.Length);        // 6
```

The type may change at every step - `string` → `string` → `int` above.

### Extra arguments

When the function takes the piped value **first** and other parameters after, pass them to `Pipe`
instead of writing a lambda. Overloads exist for one and two extra arguments:

```csharp
static double Power(double x, double exponent) => Math.Pow(x, exponent);
static double Clamp(double x, double min, double max) => Math.Max(min, Math.Min(max, x));

var result = 5.0
    .Pipe(Power, 2.0)           // 25
    .Pipe(x => x + 10)          // 35
    .Pipe(Clamp, 0.0, 30.0);    // 30
```

Note the `2.0` rather than `2`: the extra argument's type is inferred from the literal you pass, not
from the method being piped to, so `Pipe(Power, 2)` infers `Func<double, int, double>` and fails to
match `Power` (CS0123). Write the literal in the parameter's own type, or pass a typed variable.

## `Tap` - side effect

Runs an action on the value and returns **the value itself**, unchanged. Use it to log, measure or
assert in the middle of a chain without breaking it:

```csharp
var result = GetData()
    .Pipe(Validate)
    .Tap(data => Console.WriteLine($"Processing: {data}"))
    .Pipe(Transform)
    .Tap(result => _logger.LogInformation("Result: {Result}", result))
    .Pipe(Save);
```

`TapIf` only runs the action when a condition holds:

```csharp
var result = order
    .Pipe(Validate)
    .TapIf(_config.Verbose, o => _logger.LogDebug("Order: {Order}", o))
    .Pipe(Submit);
```

## `PipeIf` - conditional transform

Applies the function only when the condition holds; otherwise the value passes through untouched.
Because the value has to survive the "otherwise" branch, the function's input and output types must
match.

```csharp
var result = data
    .Pipe(Validate)
    .PipeIf(shouldNormalize, Normalize)
    .PipeIf(shouldEnrich, Enrich)
    .Pipe(Save);
```

The condition can also be a predicate on the value itself:

```csharp
var text = input
    .PipeIf(s => s.Length > 100, Truncate)
    .Pipe(Escape);
```

## `PipeEither` - branch

Picks one of two functions. Unlike `PipeIf`, both branches produce a value, so the result type is
free to differ from the input:

```csharp
var message = user
    .PipeEither(
        user.IsAdmin,
        admin => $"Welcome back, {admin.Name}",
        guest => $"Hello, {guest.Name}");
```

---

## Async

Every operation has an async form, and the extensions apply to `Task<T>` as well as to `T`. That is
what lets a chain mix sync and async steps without an `await` in the middle:

```csharp
var user = await userId
    .PipeAsync(FetchUserAsync)             // int -> Task<User>
    .TapAsync(u => LogAsync($"User: {u.Name}"))
    .PipeAsync(EnrichUserDataAsync)
    .Pipe(u => u.Name.ToUpper());          // sync step on a Task<User>
```

The naming rule is worth stating once, because it decides which overload you get:

- **`Pipe`** takes a **sync** function. On a `Task<T>` it awaits first, then applies it.
- **`PipeAsync`** takes an **async** function - one returning `Task<TOut>`.

The same split applies to `Tap`/`TapAsync`, `PipeIf`/`PipeIfAsync`, and
`PipeEither`/`PipeEitherAsync`.

```csharp
var report = await request
    .PipeAsync(LoadOrderAsync)                          // async step
    .Pipe(order => order.Lines.Count)                   // sync step, still on the Task
    .PipeIfAsync(count => count > 100, ArchiveAsync)    // conditional async step
    .PipeEitherAsync(
        count => count == 0,
        _ => EmptyReportAsync(),
        count => FullReportAsync(count));
```

---

## Complete API

Each row lists the sync form; the async counterparts take the same shape with `Async` appended, and
each is also available as an extension on `Task<T>`.

| Method | Signature | Returns |
|--------|-----------|---------|
| `Pipe` | `T.Pipe(Func<T, TOut>)` | the function's result |
| `Pipe` | `T.Pipe(Func<T, T2, TOut>, T2)` | result, with one extra argument |
| `Pipe` | `T.Pipe(Func<T, T2, T3, TOut>, T2, T3)` | result, with two extra arguments |
| `Tap` | `T.Tap(Action<T>)` | **the original value** |
| `TapIf` | `T.TapIf(bool, Action<T>)` | the original value, action runs only if true |
| `PipeIf` | `T.PipeIf(bool, Func<T, T>)` | transformed if true, else unchanged |
| `PipeIf` | `T.PipeIf(Func<T, bool>, Func<T, T>)` | same, with a predicate on the value |
| `PipeEither` | `T.PipeEither(bool, Func<T, TOut>, Func<T, TOut>)` | one branch's result |

Async-only overloads worth knowing:

| Method | Notes |
|--------|-------|
| `PipeEitherAsync(Func<T, bool>, …)` | predicate form, on `Task<T>` |
| `PipeIfAsync(Func<T, bool>, …)` | predicate form, sync and `Task<T>` |
| `TapIfAsync(bool, Func<T, Task>)` | sync and `Task<T>` |

---

## Cost

`Pipe` is `func(value)` and `Tap` is `action(value); return value;` - the JIT inlines both, so a
pipeline compiles to what the nested calls would have compiled to. What does cost something is a
lambda that **captures**: `Pipe(x => x + offset)` allocates a closure, exactly as it would anywhere
else. Pass the value as an argument (`Pipe(Add, offset)`) on a hot path.

---

## Working with `ResultOf` and `Option`

`Pipe` transforms whatever value it is handed, including a `ResultOf<T, E>` or an `Option<T>` -
which is not usually what you want, since it hands your function the container rather than what is
inside it. Use `Map`/`Bind` for that, and reach for `Pipe` when a whole pipeline is the thing being
transformed:

```csharp
// ❌ f receives the ResultOf itself
result.Pipe(r => r.SomeProperty);

// ✅ Map reaches inside
result.Map(value => value.SomeProperty);

// ✅ Pipe is right for handing the finished result somewhere else
var response = ValidateOrder(order)
    .Bind(SaveOrder)
    .Pipe(ToHttpResponse);
```

See [ResultOf](./ResultOf.md) and [Option](./Option.md) for the container-aware operators.

---

## See also

- **[ResultOf<T, E>](./ResultOf.md)** - type-safe error handling
- **[Option<T>](./Option.md)** - optional values
- **[Union Types](./Union.md)** - discriminated unions
