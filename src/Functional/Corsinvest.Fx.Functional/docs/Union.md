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

C# 15 is a **type union**: it composes types that already exist independently.

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

**Async matching.** `MatchAsync` overloads and `Task<TUnion>` extensions are generated for you.
`switch` is not awaitable, so the C# 15 model has no equivalent - you write that plumbing yourself.

**Available today**, on net8.0 and net9.0, with no preview SDK.

### Where C# 15 is stronger

**A type can belong to several unions.** `Cat` is an ordinary type, so it can appear in
`union Pet(Cat, Dog)` and `union Animal(Cat, Cow)` at once. Inheritance allows only one base, so
a `[Union]` case belongs to exactly one union - permanently.

**Ad hoc unions.** `(A or B or C) x = ...` composes a union inline, with no declaration. A source
generator cannot offer that.

**Native and dependency-free** - language syntax, with IDE and debugger support built in.

### Side by side

| | `[Union]` | C# 15 `union` |
| --- | --- | --- |
| Invalid state | **impossible** | `default` has a null `Value` |
| Boxing of value-type cases | **never** | always (unless you hand-write a non-boxing union) |
| Indirection to reach the data | 1 hop | 2 hops (`Value`, then the object) |
| Cases usable as types | ✅ `Pet.Cat` | ✅ standalone types |
| Same type in several unions | ❌ | ✅ |
| Ad hoc unions | ❌ | ✅ |
| Async matching | ✅ `MatchAsync` | ❌ |
| Exhaustive `switch` | ✅ via UNION004/005 | ✅ built into the compiler |
| Available on | net8.0+ | .NET 11 |

The trade is consistent: a closed hierarchy buys correctness (no invalid state, no boxing) at the
cost of composability (one union per type, no ad hoc unions). Which side matters depends on what
you are modelling - `Option<T>` and `ResultOf<T, E>` are exactly the case where an unrepresentable
invalid state is worth more than reuse.

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

The source generator creates `Match` methods and async extensions for your union type. For example:

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

`UNION001`-`UNION003` are structural errors and cannot be configured away: without them the
generator cannot emit valid code. `UNION004` is a normal analyzer rule and can be tuned through
`.editorconfig`.

## See Also

- [ResultOf<T, E>](ResultOf.md) - Combine with Result for error handling
- [Option<T>](Option.md) - Built-in union for optional values
- [Pipe Extensions](Pipe.md) - Chain transformations on union values
- [04_UnionTypes.cs](../../../../examples/04_UnionTypes.cs) - Runnable example: payment methods, API states, shapes
