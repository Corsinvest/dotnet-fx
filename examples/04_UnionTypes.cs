using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

// Payment methods: the case types are ordinary records, declared on their own, so they can be
// reused in other unions and passed around independently of PaymentMethod.
public record CreditCard(string Number, string ExpiryDate);
public record PayPal(string Email);
public record BankTransfer(string Iban, string Bic);

// [Union<...>] composes existing types. The generator emits one sealed nested wrapper per case
// (PaymentMethod.CreditCard and so on), each deriving from PaymentMethod, which is what keeps
// the hierarchy closed and lets a plain switch match on it.
[Union<CreditCard, PayPal, BankTransfer>]
public abstract partial record PaymentMethod;

// Union: API response states
[Union]
public partial record ApiResponse
{
    public partial record Loading();
    public partial record Success(UserData User);
    public partial record Error(string Message);
}

// Union: Geometric shapes
[Union]
public partial record Shape
{
    public partial record Circle(double Radius);
    public partial record Rectangle(double Width, double Height);
    public partial record Triangle(double SideA, double SideB, double SideC);
}

// Data models
public record UserData(int Id, string Name, string Email);

// This file shows both ways to declare a union.
//
// PaymentMethod uses [Union<CreditCard, PayPal, BankTransfer>]: the cases are external types,
// declared on their own, so the same type can take part in more than one union.
//
// ApiResponse and Shape use [Union] with nested cases: the cases exist only as part of the
// union. That form is also the only one that can express a union whose cases close over the
// root's own type parameter - which is why Option<T> and ResultOf<T,E> use it.

/// <summary>
/// Example 04: Union Types - Discriminated Unions
///
/// Demonstrates custom discriminated unions for:
/// - Payment methods (CreditCard, PayPal, BankTransfer)
/// - API responses (Success, Error, Loading)
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
            new ApiResponse.Loading(),
            new ApiResponse.Success(new UserData(1, "Alice", "alice@example.com")),
            new ApiResponse.Error("Network timeout")
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
            new Shape.Circle(5.0),
            new Shape.Rectangle(4.0, 6.0),
            new Shape.Triangle(3.0, 4.0, 5.0)
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
    }

    // Union cases are real types, so a plain switch works alongside Match().
    //
    // Note there is no discard arm: normally the compiler would demand one (CS8509), because it
    // treats reference-type hierarchies as open. The UNION005 suppressor knows this hierarchy is
    // closed and stands the warning down.
    //
    // Add a fourth case type to the [Union<...>] list and UNION004 flags every switch that does
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
            ApiResponse.Success(var user) => $"✅ {user.Name} ({user.Email})",
            ApiResponse.Error(var message) => $"❌ {message}"
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
            onError: err => Console.WriteLine($"   ❌ Error: {err.Message}")
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

