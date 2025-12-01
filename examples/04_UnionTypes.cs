using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

// Union: Payment methods
[Union]
public partial record PaymentMethod
{
    public partial record CreditCard(string Number, string ExpiryDate);
    public partial record PayPal(string Email);
    public partial record BankTransfer(string Iban, string Bic);
}

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

        var payments = new PaymentMethod[]
        {
            new PaymentMethod.CreditCard("1234-5678-9012-3456", "12/25"),
            new PaymentMethod.PayPal("alice@example.com"),
            new PaymentMethod.BankTransfer("IT60X0542811101000000123456", "BCITITMMXXX")
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
    }

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

