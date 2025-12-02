using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 06: Combined Patterns
///
/// Demonstrates the power of combining multiple patterns:
/// - Option + ResultOf + Pipe together
/// - Real-world user registration flow
/// - Complex error handling with type-safe errors
/// - Data transformation pipelines with validation
/// </summary>
public static class CombinedPatterns
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 06: Combined Patterns (Option + ResultOf + Pipe) ═══\n");

        // Scenario: User registration with validation, database lookup, and email sending
        var testUsers = new[]
        {
            ("alice@example.com", "Alice", "25"),
            (string.Empty, "Bob", "30"),                              // Missing email
            ("charlie@example.com", "Charlie", "invalid"),  // Invalid age
            ("bob@example.com", "Bob", "28")                // Duplicate email
        };

        foreach (var (email, name, ageStr) in testUsers)
        {
            Console.WriteLine($"🔄 Registering: {name} ({email}, age={ageStr})");

            var result = RegisterUser(email, name, ageStr);

            result.Match(
                ok => Console.WriteLine($"   ✅ Success: User {ok.Value.Name} registered with ID {ok.Value.Id}"),
                fail => Console.WriteLine($"   ❌ Failed: {fail.ErrorValue}")
            );

            Console.WriteLine();
        }

        // Scenario 2: Configuration loading with defaults
        Console.WriteLine("🔧 Configuration Loading with Defaults:\n");

        var config = LoadConfigWithDefaults();
        Console.WriteLine($"   Database: {config.Database}");
        Console.WriteLine($"   Port: {config.Port}");
        Console.WriteLine($"   Timeout: {config.TimeoutSeconds}s");
        Console.WriteLine($"   MaxRetries: {config.MaxRetries}");
    }

    // Main registration pipeline combining all patterns
    private static ResultOf<RegisteredUser, RegistrationError> RegisterUser(string email, string name, string ageStr)
    {
        // Step 1: Parse and validate input (ResultOf + Pipe)
        var validationResult = ValidateEmail(email)
            .Bind(_ => ValidateName(name))
            .Bind(_ => ParseAge(ageStr))
            .Map(age => new ValidatedInput(email, name, age));

        // Step 2: If validation fails, short-circuit
        if (validationResult.IsFail)
        {
            return ResultOf.Fail<RegisteredUser, RegistrationError>(RegistrationError.ValidationFailed);
        }

        var input = validationResult.GetValueOrThrow();

        // Step 3: Check if email already exists (Option -> ResultOf)
        var existingUserCheck = FindUserByEmail(input.Email)
            .Match(
                some => ResultOf.Fail<Unit, RegistrationError>(RegistrationError.EmailAlreadyExists),
                none => ResultOf.Ok<Unit, RegistrationError>(Unit.Value)
            );

        if (existingUserCheck.IsFail)
        {
            return ResultOf.Fail<RegisteredUser, RegistrationError>(RegistrationError.EmailAlreadyExists);
        }

        // Step 4: Create user (Pipe for transformation)
        var userId = Random.Shared.Next(1000, 9999);
        var user = new User(userId, input.Name, input.Email, input.Age)
            .Pipe(u => SaveUser(u))  // Side effect: save to "database"
            .Pipe(u => new RegisteredUser(u.Id, u.Name, u.Email, DateTime.UtcNow));

        // Step 5: Send welcome email (Option for nullable result)
        SendWelcomeEmail(user.Email)
            .Match(
                some => Console.WriteLine($"   → Welcome email sent to {some.Value}"),
                none => Console.WriteLine($"   → Warning: Could not send welcome email")
            );

        return ResultOf.Ok<RegisteredUser, RegistrationError>(user);
    }

    // Validation functions returning ResultOf
    private static ResultOf<string, ValidationError> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return ResultOf.Fail<string, ValidationError>(ValidationError.Required);
        }

        if (!email.Contains("@"))
        {
            return ResultOf.Fail<string, ValidationError>(ValidationError.Invalid);
        }

        return ResultOf.Ok<string, ValidationError>(email);
    }

    private static ResultOf<string, ValidationError> ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ResultOf.Fail<string, ValidationError>(ValidationError.Required);
        }

        return ResultOf.Ok<string, ValidationError>(name);
    }

    private static ResultOf<int, ValidationError> ParseAge(string ageStr)
    {
        if (!int.TryParse(ageStr, out var age))
        {
            return ResultOf.Fail<int, ValidationError>(ValidationError.Invalid);
        }

        if (age < 0 || age > 150)
        {
            return ResultOf.Fail<int, ValidationError>(ValidationError.Invalid);
        }

        return ResultOf.Ok<int, ValidationError>(age);
    }

    // Database simulation using Option
    private static Option<User> FindUserByEmail(string email)
    {
        // Simulate existing users
        var existingUsers = new Dictionary<string, User>
        {
            ["bob@example.com"] = new User(1, "Bob", "bob@example.com", 30)
        };

        return existingUsers.TryGetValue(email, out var user)
            ? Option.Some(user)
            : Option.None<User>();
    }

    private static User SaveUser(User user)
    {
        Console.WriteLine($"   → User saved to database: {user.Name}");
        return user;
    }

    // Email service returning Option
    private static Option<string> SendWelcomeEmail(string email)
    {
        // Simulate email sending (could fail)
        if (email.Contains("invalid"))
        {
            return Option.None<string>();
        }

        return Option.Some(email);
    }

    // Configuration loading with Option + Pipe for defaults
    private static AppConfig LoadConfigWithDefaults()
    {
        var rawConfig = new Dictionary<string, string>
        {
            ["database"] = "localhost",
            ["port"] = "5432"
            // timeout and maxRetries are missing
        };

        return new AppConfig(
            Database: GetConfigValue(rawConfig, "database").GetValueOr("localhost"),
            Port: GetConfigValue(rawConfig, "port")
                .Bind(ParseInt)
                .GetValueOr(5432),
            TimeoutSeconds: GetConfigValue(rawConfig, "timeout")
                .Bind(ParseInt)
                .GetValueOr(30),  // Default timeout
            MaxRetries: GetConfigValue(rawConfig, "maxRetries")
                .Bind(ParseInt)
                .GetValueOr(3)    // Default retries
        );
    }

    private static Option<string> GetConfigValue(Dictionary<string, string> config, string key)
        => config.TryGetValue(key, out var value)
            ? Option.Some(value)
            : Option.None<string>();

    private static Option<int> ParseInt(string input)
        => int.TryParse(input, out var result)
            ? Option.Some(result)
            : Option.None<int>();

    // Domain models
    private record ValidatedInput(string Email, string Name, int Age);
    private record User(int Id, string Name, string Email, int Age);
    private record RegisteredUser(int Id, string Name, string Email, DateTime RegisteredAt);
    private record AppConfig(string Database, int Port, int TimeoutSeconds, int MaxRetries);

    // Error types
    private enum ValidationError { Required, Invalid }
    private enum RegistrationError
    {
        ValidationFailed,
        EmailAlreadyExists,
        DatabaseError
    }
}
