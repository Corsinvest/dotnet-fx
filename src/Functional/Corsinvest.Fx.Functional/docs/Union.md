# Union Types - Discriminated Unions

**Create type-safe union types with source generators**

## Overview

The `[Union]` attribute enables you to create discriminated unions (also known as sum types or tagged unions) in C#. Discriminated unions represent a value that can be one of several named cases, each potentially with different data.

The source generator creates:

- Type-safe construction
- Exhaustive pattern matching via `Match()` method
- Compile-time verification of all cases

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

The compiler enforces exhaustive matching - you must handle all cases:

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
    // Error: Missing triangle handler
);
```

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

- ✅ **Zero overhead** - Compiles to efficient switch expressions
- ✅ **No reflection** - All matching is compile-time
- ✅ **Value types** - Records are efficient
- ✅ **Inlining** - JIT can inline simple match expressions

## See Also

- [ResultOf<T, E>](ResultOf.md) - Combine with Result for error handling
- [Option<T>](Option.md) - Built-in union for optional values
- [Pipe Extensions](Pipe.md) - Chain transformations on union values
- [Examples](Examples.md) - More real-world usage patterns
