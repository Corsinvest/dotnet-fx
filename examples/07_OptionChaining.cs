using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 07: Option Chaining
///
/// Demonstrates advanced Option patterns:
/// - OrElse for providing alternatives
/// - Flatten for unwrapping nested Options
/// - Chaining multiple optional lookups
/// - Complex navigation through optional data structures
/// </summary>
public static class OptionChaining
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 07: Option Chaining (OrElse & Flatten) ═══\n");

        // Example 1: OrElse - Fallback chains
        Console.WriteLine("1️⃣  OrElse - Fallback Chains\n");

        var primaryEmail = Option.None<string>();
        var secondaryEmail = Option.Some("backup@example.com");
        var defaultEmail = Option.Some("default@example.com");

        var email = primaryEmail
            .OrElse(secondaryEmail)
            .OrElse(defaultEmail);

        Console.WriteLine($"   Primary: {FormatOption(primaryEmail)}");
        Console.WriteLine($"   Secondary: {FormatOption(secondaryEmail)}");
        Console.WriteLine($"   Final email: {email.GetValueOr("no-email")}");

        // Example 2: OrElse with functions (lazy evaluation)
        Console.WriteLine("\n2️⃣  OrElse with Lazy Evaluation\n");

        var cacheResult = GetFromCache("user:123");
        var dbResult = cacheResult.OrElse(() =>
        {
            Console.WriteLine("   → Cache miss, fetching from database...");
            return GetFromDatabase("user:123");
        });

        Console.WriteLine($"   Cache: {FormatOption(cacheResult)}");
        Console.WriteLine($"   Final: {FormatOption(dbResult)}");

        // Example 3: Flatten - Unwrapping nested Options
        Console.WriteLine("\n3️⃣  Flatten - Unwrapping Nested Options\n");

        var nestedSome = Option.Some(Option.Some(42));
        var nestedNone1 = Option.Some(Option.None<int>());
        var nestedNone2 = Option.None<Option<int>>();

        Console.WriteLine($"   Some(Some(42)).Flatten() = {FormatOption(nestedSome.Flatten())}");
        Console.WriteLine($"   Some(None).Flatten() = {FormatOption(nestedNone1.Flatten())}");
        Console.WriteLine($"   None.Flatten() = {FormatOption(nestedNone2.Flatten())}");

        // Example 4: Flatten in practice - parsing with validation
        Console.WriteLine("\n4️⃣  Flatten in Practice - Parsing Chain\n");

        var inputs = new[] { "42", "not-a-number", null };

        foreach (var input in inputs)
        {
            var result = ParseWithValidation(input).Flatten();
            Console.WriteLine($"   Input: '{input ?? "null"}' → {FormatOption(result)}");
        }

        // Example 5: Complex navigation with Option chaining
        Console.WriteLine("\n5️⃣  Complex Navigation - User Profile Lookup\n");

        var users = CreateTestData();

        var userId1 = 1;
        var userId2 = 2;
        var userId3 = 999;

        var profilePic1 = GetUserProfilePicture(users, userId1);
        var profilePic2 = GetUserProfilePicture(users, userId2);
        var profilePic3 = GetUserProfilePicture(users, userId3);

        Console.WriteLine($"   User #{userId1} profile pic: {profilePic1.GetValueOr("default.png")}");
        Console.WriteLine($"   User #{userId2} profile pic: {profilePic2.GetValueOr("default.png")}");
        Console.WriteLine($"   User #{userId3} profile pic: {profilePic3.GetValueOr("default.png")}");

        // Example 6: OrElse cascade with multiple fallbacks
        Console.WriteLine("\n6️⃣  OrElse Cascade - Multiple Fallbacks\n");

        var settingSources = new[]
        {
            ("User setting", GetUserSetting("theme")),
            ("Team setting", GetTeamSetting("theme")),
            ("Company default", GetCompanyDefault("theme")),
            ("System default", Option.Some("light"))
        };

        var theme = settingSources[0].Item2
            .OrElse(settingSources[1].Item2)
            .OrElse(settingSources[2].Item2)
            .OrElse(settingSources[3].Item2);

        foreach (var (source, value) in settingSources)
        {
            Console.WriteLine($"   {source}: {FormatOption(value)}");
        }
        Console.WriteLine($"   → Final theme: {theme.GetValueOr("light")}");

        // Example 7: Flatten with Map - avoiding nested Options
        Console.WriteLine("\n7️⃣  Flatten with Map - Avoiding Nesting\n");

        var userIdToLookup = Option.Some(1);

        // Without Flatten - nested Option<Option<string>>
        var nestedResult = userIdToLookup.Map(id => FindUserEmail(id));
        Console.WriteLine($"   With Map only: Option<Option<string>> (nested)");

        // With Flatten - clean Option<string>
        var flatResult = userIdToLookup.Map(id => FindUserEmail(id)).Flatten();
        Console.WriteLine($"   With Map + Flatten: {FormatOption(flatResult)}");

        // Alternative: Use Bind to avoid nesting entirely
        var bindResult = userIdToLookup.Bind(id => FindUserEmail(id));
        Console.WriteLine($"   With Bind (better): {FormatOption(bindResult)}");
    }

    // Helper: Get from cache (simulated miss)
    private static Option<User> GetFromCache(string key)
        => Option.None<User>();

    // Helper: Get from database (simulated hit)
    private static Option<User> GetFromDatabase(string key)
        => Option.Some(new User(123, "Alice", "alice@example.com"));

    // Helper: Parse with validation returning nested Option
    private static Option<Option<int>> ParseWithValidation(string? input)
    {
        if (input is null)
        {
            return Option.None<Option<int>>();
        }

        if (!int.TryParse(input, out var number))
        {
            return Option.Some(Option.None<int>());
        }

        // Additional validation: only accept positive numbers
        if (number < 0)
        {
            return Option.Some(Option.None<int>());
        }

        return Option.Some(Option.Some(number));
    }

    // Helper: Complex navigation through optional data
    private static Option<string> GetUserProfilePicture(Dictionary<int, UserProfile> users, int userId)
        => FindUserProfile(users, userId)
            .Bind(profile => profile.Settings)
            .Bind(settings => settings.ProfilePicture);

    private static Option<UserProfile> FindUserProfile(Dictionary<int, UserProfile> users, int userId)
        => users.TryGetValue(userId, out var profile)
            ? Option.Some(profile)
            : Option.None<UserProfile>();

    // Helper: Settings lookups
    private static Option<string> GetUserSetting(string key) => Option.None<string>();
    private static Option<string> GetTeamSetting(string key) => Option.None<string>();
    private static Option<string> GetCompanyDefault(string key) => Option.Some("dark");

    // Helper: Find user email
    private static Option<string> FindUserEmail(int userId)
        => userId switch
        {
            1 => Option.Some("alice@example.com"),
            2 => Option.Some("bob@example.com"),
            _ => Option.None<string>()
        };

    // Helper: Create test data
    private static Dictionary<int, UserProfile> CreateTestData() => new Dictionary<int, UserProfile>
    {
        [1] = new UserProfile(
                1,
                "Alice",
                Option.Some(new UserSettings(
                    Option.Some("alice_avatar.png"),
                    Option.Some("dark")
                ))
            ),
        [2] = new UserProfile(
                2,
                "Bob",
                Option.None<UserSettings>()  // No settings
            )
    };

    // Helper: Format option for display
    private static string FormatOption<T>(Option<T> option)
        => option.Match(
            some => $"Some({some.Value})",
            none => "None"
        );

    // Domain models
    private record User(int Id, string Name, string Email);
    private record UserProfile(int UserId, string Name, Option<UserSettings> Settings);
    private record UserSettings(Option<string> ProfilePicture, Option<string> Theme);
}
