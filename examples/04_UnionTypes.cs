/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

// Payment methods: the case types are ordinary records, declared on their own, so they can be
// reused in other unions and passed around independently of PaymentMethod.
public record CreditCard(string Number, string ExpiryDate);
public record PayPal(string Email);
public record BankTransfer(string Iban, string Bic);

// IUnion<...> composes existing types. The generator emits one sealed nested wrapper per case
// (PaymentMethod.CreditCard and so on), each deriving from PaymentMethod, which is what keeps
// the hierarchy closed and lets a plain switch match on it.
public abstract partial record PaymentMethod : IUnion<CreditCard, PayPal, BankTransfer>;

// Union: API response states
public record Loading;
public record Success(UserData User);
public record Failure(string Message);

public abstract partial record ApiResponse : IUnion<Loading, Success, Failure>;

// Union: Geometric shapes
public record Circle(double Radius);
public record Rectangle(double Width, double Height);
public record Triangle(double SideA, double SideB, double SideC);

public abstract partial record Shape : IUnion<Circle, Rectangle, Triangle>;

// Union: a case type can be ANY type, not just a record. The wrapper contains its case rather
// than inheriting from it, so `sealed`, `struct` and `enum` are all fine - and a value-type case
// lives in a typed field, so nothing is boxed.
public sealed class DatabaseError(string Table, int Code)          // sealed class
{
    public string Table { get; } = Table;
    public int Code { get; } = Code;
}

public enum NetworkError { Timeout, Refused, DnsFailure }          // enum

public readonly struct ValidationError(int Line, int Column)       // struct
{
    public int Line { get; } = Line;
    public int Column { get; } = Column;
}

// string too: sealed BCL type
public abstract partial record AppError : IUnion<DatabaseError, NetworkError, ValidationError, string>;

// Data models
public record UserData(int Id, string Name, string Email);

// Every union in this file uses IUnion<...>: the cases are standalone types, declared on their
// own, so the same case type can take part in more than one union (or be used independently of
// it). This is also the only shape that can express a union whose cases close over the root's own
// type parameter - which is why Option<T> and ResultOf<T,E> use it.

/// <summary>
/// Example 04: Union Types - Discriminated Unions
///
/// Demonstrates custom discriminated unions for:
/// - Payment methods (CreditCard, PayPal, BankTransfer)
/// - API responses (Success, Failure, Loading)
/// - Shapes (Circle, Rectangle, Triangle)
/// - Pattern matching and exhaustive handling
/// </summary>
public static class UnionTypes
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 04: Union Types ═══\n");

        // Example 1: Payment methods
        Console.WriteLine("1️⃣  Payment Methods\n");

        // The generated implicit conversions mean a case type can be assigned directly.
        var payments = new PaymentMethod[]
        {
            new CreditCard("1234-5678-9012-3456", "12/25"),
            new PayPal("alice@example.com"),
            new BankTransfer("IT60X0542811101000000123456", "BCITITMMXXX")
        };

        foreach (var payment in payments)
        {
            var description = payment.Match(
                creditCard => $"Credit Card ending in {creditCard.Number[^4..]}",
                payPal => $"PayPal account {payPal.Email}",
                bankTransfer => $"Bank transfer to {bankTransfer.Iban}"
            );

            var fee = CalculatePaymentFee(payment);
            Console.WriteLine($"   {description}");
            Console.WriteLine($"   Processing fee: ${fee:F2}\n");
        }

        // Example 2: API Responses
        Console.WriteLine("2️⃣  API Response States\n");

        var responses = new ApiResponse[]
        {
            new Loading(),
            new Success(new UserData(1, "Alice", "alice@example.com")),
            new Failure("Network timeout")
        };

        foreach (var response in responses)
        {
            HandleApiResponse(response);
            Console.WriteLine();
        }

        // Example 3: Geometric shapes
        Console.WriteLine("3️⃣  Geometric Shapes\n");

        var shapes = new Shape[]
        {
            new Circle(5.0),
            new Rectangle(4.0, 6.0),
            new Triangle(3.0, 4.0, 5.0)
        };

        foreach (var shape in shapes)
        {
            var area = CalculateArea(shape);
            var perimeter = CalculatePerimeter(shape);

            var description = shape.Match(
                circle => $"Circle (radius: {circle.Radius})",
                rectangle => $"Rectangle ({rectangle.Width}x{rectangle.Height})",
                triangle => $"Triangle (sides: {triangle.SideA}, {triangle.SideB}, {triangle.SideC})"
            );

            Console.WriteLine($"   {description}");
            Console.WriteLine($"   Area: {area:F2}, Perimeter: {perimeter:F2}\n");
        }

        // Example 4: Switch expression instead of Match()
        Console.WriteLine("4️⃣  Switch Expression (exhaustiveness-checked)\n");

        foreach (var payment in payments)
        {
            Console.WriteLine($"   {DescribePayment(payment)}");
        }

        Console.WriteLine();
        foreach (var response in responses)
        {
            Console.WriteLine($"   {DescribeResponse(response)}");
        }

        // Example 5: case types that are not records
        Console.WriteLine("\n5️⃣  Mixed Case Types (class, enum, struct, string)\n");

        var errors = new AppError[]
        {
            new AppError.DatabaseError(new DatabaseError("orders", 1205)),
            new AppError.NetworkError(NetworkError.Timeout),
            new AppError.ValidationError(new ValidationError(42, 7)),
            new AppError.String("plain message")
        };

        foreach (var error in errors)
        {
            Console.WriteLine($"   {DescribeError(error)}");
        }
    }

    // A union case can be any type. Note what each arm receives: the wrapper hands over the case
    // itself, so `db` is a DatabaseError, `net` is the enum value, `validation` is the struct.
    //
    // The enum and the struct are stored in typed fields rather than in an object, so neither is
    // boxed - unlike the C# 15 `union` keyword, which always boxes value-type cases.
    //
    // The wrapper for `string` is named String (rule: primitives and BCL types use their CLR
    // name), which is why the arm reads AppError.String rather than AppError.string.
    private static string DescribeError(AppError error)
        => error switch
        {
            AppError.DatabaseError(var db) => $"🗄️  {db.Table} failed with code {db.Code}",
            AppError.NetworkError(var net) => $"🌐 network: {net}",
            AppError.ValidationError(var validation) => $"📋 line {validation.Line}, col {validation.Column}",
            AppError.String(var message) => $"💬 {message}"
        };

    // Union cases are real types, so a plain switch works alongside Match().
    //
    // Note there is no discard arm: normally the compiler would demand one (CS8509), because it
    // treats reference-type hierarchies as open. The UNION005 suppressor knows this hierarchy is
    // closed and stands the warning down.
    //
    // Add a fourth case type to the IUnion<...> list and UNION004 flags every switch that does
    // not handle it - the failure a discard arm would have hidden. The "Add missing union cases"
    // code fix then fills the arm in.
    private static string DescribePayment(PaymentMethod payment)
        => payment switch
        {
            PaymentMethod.CreditCard(var card) => $"💳 Card ending in {card.Number[^4..]}",
            PaymentMethod.PayPal(var payPal) => $"🅿️  PayPal {payPal.Email}",
            PaymentMethod.BankTransfer(var transfer) => $"🏦 Transfer to {transfer.Iban}"
        };

    // Unlike the positional Match(), switch arms name the type they handle, so
    // reordering them cannot silently change behaviour.
    private static string DescribeResponse(ApiResponse response)
        => response switch
        {
            ApiResponse.Loading => "⏳ Loading...",
            ApiResponse.Success(var success) => $"✅ {success.User.Name} ({success.User.Email})",
            ApiResponse.Failure(var failure) => $"❌ {failure.Message}"
        };

    // Calculate payment processing fee
    private static decimal CalculatePaymentFee(PaymentMethod payment)
        => payment.Match(
            creditCard => 2.5m,  // Fixed fee for credit cards
            payPal => 1.5m,       // Lower fee for PayPal
            bankTransfer => 0.0m  // Free for bank transfers
        );

    // Handle API response with different UI states
    private static void HandleApiResponse(ApiResponse response) => response.Match(
            onLoading: _ => Console.WriteLine("   ⏳ Loading..."),
            onSuccess: data => Console.WriteLine($"   ✅ Success: User {data.User.Name} ({data.User.Email})"),
            onFailure: err => Console.WriteLine($"   ❌ Error: {err.Message}")
        );

    // Calculate area of shape
    private static double CalculateArea(Shape shape)
        => shape.Match(
            circle => Math.PI * circle.Radius * circle.Radius,
            rectangle => rectangle.Width * rectangle.Height,
            triangle =>
            {
                // Heron's formula
                var s = (triangle.SideA + triangle.SideB + triangle.SideC) / 2.0;
                return Math.Sqrt(s * (s - triangle.SideA) * (s - triangle.SideB) * (s - triangle.SideC));
            }
        );

    // Calculate perimeter of shape
    private static double CalculatePerimeter(Shape shape)
        => shape.Match(
            circle => 2 * Math.PI * circle.Radius,
            rectangle => 2 * (rectangle.Width + rectangle.Height),
            triangle => triangle.SideA + triangle.SideB + triangle.SideC
        );
}

