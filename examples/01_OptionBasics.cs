using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 01: Option&lt;T&gt; Basics
///
/// Demonstrates how Option&lt;T&gt; handles nullable values safely:
/// - Parsing with Option.FromNullable
/// - Dictionary lookups
/// - Configuration loading
/// - Chaining with Map/Bind
/// </summary>
public static class OptionBasics
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 01: Option<T> Basics ═══\n");

        // 1. Parsing user input safely
        Console.WriteLine("1️⃣  Safe Parsing");
        var userInput1 = "42";
        var userInput2 = "not a number";

        var parsed1 = ParseInt(userInput1);
        var parsed2 = ParseInt(userInput2);

        Console.WriteLine($"  Parse '{userInput1}': {FormatOption(parsed1)}");
        Console.WriteLine($"  Parse '{userInput2}': {FormatOption(parsed2)}");

        // 2. Dictionary lookup
        Console.WriteLine("\n2️⃣  Dictionary Lookup");
        var config = new Dictionary<string, string>
        {
            ["database"] = "localhost",
            ["port"] = "5432"
        };

        var database = GetConfigValue(config, "database");
        var timeout = GetConfigValue(config, "timeout");

        Console.WriteLine($"  Config 'database': {FormatOption(database)}");
        Console.WriteLine($"  Config 'timeout': {FormatOption(timeout)} (using default: {timeout.GetValueOr("30")})");

        // 3. Chaining operations
        Console.WriteLine("\n3️⃣  Chaining with Map");
        var age = ParseInt("25")
            .Map(n => n * 2)
            .Map(n => $"{n} years");

        Console.WriteLine($"  Age doubled: {FormatOption(age)}");

        // 4. Null handling with FromNullable
        Console.WriteLine("\n4️⃣  Null Handling");
        string? nullableString = GetUserName(1);
        string? nullName = GetUserName(999);

        var userName = Option.FromNullable(nullableString);
        var missingUser = Option.FromNullable(nullName);

        Console.WriteLine($"  User #1: {FormatOption(userName)}");
        Console.WriteLine($"  User #999: {FormatOption(missingUser)}");

        // 5. Pattern matching
        Console.WriteLine("\n5️⃣  Pattern Matching");
        var result = FindUserById(42);
        result.Match(
            some => Console.WriteLine($"  Found user: {some.Value.Name} ({some.Value.Email})"),
            none => Console.WriteLine("  User not found")
        );

        // 6. Unwrapping with defaults
        Console.WriteLine("\n6️⃣  Unwrapping Options");
        var existingUser = FindUserById(1).GetValueOr(new User(0, "Guest", "guest@example.com"));
        var missingUserDefault = FindUserById(999).GetValueOr(new User(0, "Guest", "guest@example.com"));

        Console.WriteLine($"  User #1: {existingUser.Name}");
        Console.WriteLine($"  User #999: {missingUserDefault.Name} (default)");
    }

    // Helper: Parse string to int safely
    private static Option<int> ParseInt(string input)
        => int.TryParse(input, out var result)
            ? Option.Some(result)
            : Option.None<int>();

    // Helper: Get config value
    private static Option<string> GetConfigValue(Dictionary<string, string> config, string key)
        => config.TryGetValue(key, out var value)
            ? Option.Some(value)
            : Option.None<string>();

    // Helper: Simulate user name lookup
    private static string? GetUserName(int id)
        => id switch
        {
            1 => "Alice",
            2 => "Bob",
            _ => null
        };

    // Helper: Find user by ID
    private static Option<User> FindUserById(int id)
    {
        var users = new Dictionary<int, User>
        {
            [1] = new User(1, "Alice", "alice@example.com"),
            [2] = new User(2, "Bob", "bob@example.com")
        };

        return users.TryGetValue(id, out var user)
            ? Option.Some(user)
            : Option.None<User>();
    }

    // Helper: Format option for display
    private static string FormatOption<T>(Option<T> option)
        => option.Match(
            some => $"Some({some.Value})",
            none => "None"
        );

    // Simple user record
    private record User(int Id, string Name, string Email);
}
