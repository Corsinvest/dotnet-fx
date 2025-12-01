using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 02: ResultOf&lt;T, E&gt; Validation
///
/// Demonstrates multi-step validation with type-safe errors:
/// - Input validation with specific error types
/// - Chaining validations with Railway-Oriented Programming
/// - Error accumulation and reporting
/// </summary>
public static class ResultOfValidation
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 02: ResultOf<T, E> Validation ═══\n");

        // Test cases
        var testCases = new[]
        {
            ("alice@example.com", "Alice", "25"),
            ("invalid-email", "Bob", "30"),
            ("bob@example.com", "B", "35"),
            ("charlie@example.com", "Charlie", "not-a-number"),
            (string.Empty, string.Empty, string.Empty)
        };

        foreach (var (email, name, ageStr) in testCases)
        {
            Console.WriteLine($"📝 Validating: email='{email}', name='{name}', age='{ageStr}'");

            var result = ValidateAndCreateUser(email, name, ageStr);

            result.Match(
                ok => Console.WriteLine($"   ✅ Success: User {ok.Value.Name} (ID: {ok.Value.Id}) created"),
                fail => Console.WriteLine($"   ❌ Error: {fail.ErrorValue}")
            );

            Console.WriteLine();
        }

        // Example: Using LINQ query syntax for validation pipeline
        Console.WriteLine("🔗 Validation Pipeline with LINQ:\n");

        var pipelineResult = ValidateUserPipeline("john@example.com", "John", "28");
        pipelineResult.Match(
            ok => Console.WriteLine($"   ✅ Pipeline success: {ok.Value}"),
            fail => Console.WriteLine($"   ❌ Pipeline error: {fail.ErrorValue}")
        );
    }

    // Main validation function using Railway-Oriented Programming
    private static ResultOf<User, ValidationError> ValidateAndCreateUser(string email, string name, string ageStr) =>
        // Validate each field, short-circuit on first error
        ValidateEmail(email)
            .Bind(_ => ValidateName(name))
            .Bind(_ => ValidateAge(ageStr))
            .Map(age => new User(
                Id: Random.Shared.Next(1000, 9999),
                Name: name,
                Email: email,
                Age: age
            ));

    // Validate email
    private static ResultOf<string, ValidationError> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return ResultOf.Fail<string, ValidationError>(ValidationError.EmailRequired);
        }

        if (!email.Contains("@"))
        {
            return ResultOf.Fail<string, ValidationError>(ValidationError.EmailInvalid);
        }

        return ResultOf.Ok<string, ValidationError>(email);
    }

    // Validate name
    private static ResultOf<string, ValidationError> ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ResultOf.Fail<string, ValidationError>(ValidationError.NameRequired);
        }

        if (name.Length < 2)
        {
            return ResultOf.Fail<string, ValidationError>(ValidationError.NameTooShort);
        }

        return ResultOf.Ok<string, ValidationError>(name);
    }

    // Validate age
    private static ResultOf<int, ValidationError> ValidateAge(string ageStr)
    {
        if (string.IsNullOrWhiteSpace(ageStr))
        {
            return ResultOf.Fail<int, ValidationError>(ValidationError.AgeRequired);
        }

        if (!int.TryParse(ageStr, out var age))
        {
            return ResultOf.Fail<int, ValidationError>(ValidationError.AgeInvalid);
        }

        if (age < 0 || age > 150)
        {
            return ResultOf.Fail<int, ValidationError>(ValidationError.AgeOutOfRange);
        }

        return ResultOf.Ok<int, ValidationError>(age);
    }

    // Example using LINQ query syntax
    private static ResultOf<string, ValidationError> ValidateUserPipeline(string email, string name, string ageStr)
    {
        var result =
            from validEmail in ValidateEmail(email)
            from validName in ValidateName(name)
            from validAge in ValidateAge(ageStr)
            select $"User: {validName} <{validEmail}>, Age: {validAge}";

        return result;
    }

    // Validation errors enum
    private enum ValidationError
    {
        EmailRequired,
        EmailInvalid,
        NameRequired,
        NameTooShort,
        AgeRequired,
        AgeInvalid,
        AgeOutOfRange
    }

    // User record
    private record User(int Id, string Name, string Email, int Age);
}
