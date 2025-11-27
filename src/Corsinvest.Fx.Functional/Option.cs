namespace Corsinvest.Fx.Functional;

/// <summary>
/// Represents an optional value that may or may not be present.
/// Use this type to make null handling explicit and type-safe.
/// </summary>
/// <typeparam name="T">The type of the optional value</typeparam>
/// <remarks>
/// This is a discriminated union type with two variants: Some (value present) and None (value absent).
/// Use this type to eliminate null reference exceptions and make nullability explicit in function signatures.
/// Supports LINQ query syntax, pattern matching, and async operations.
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// Option&lt;User&gt; FindUser(int id)
/// {
///     var user = _db.Find(id);
///     return user != null
///         ? Option.Some(user)
///         : Option.None&lt;User&gt;();
/// }
///
/// // Pattern matching
/// var result = FindUser(42);
/// result.Match(
///     some => Console.WriteLine($"Found: {some.Value.Name}"),
///     () => Console.WriteLine("User not found")
/// );
/// </code>
/// </example>
[Union]
public partial record Option<T>
{
    /// <summary>
    /// Represents a present value.
    /// </summary>
    /// <param name="Value">The contained value</param>
    public partial record Some(T Value);

    /// <summary>
    /// Represents an absent value.
    /// </summary>
    public partial record None();
}

/// <summary>
/// Provides factory methods for creating <see cref="Option{T}"/> instances.
/// </summary>
public static class Option
{
    /// <summary>
    /// Creates an option with a present value.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to wrap</param>
    /// <returns>An option containing the specified value</returns>
    /// <example>
    /// <code>
    /// var option = Option.Some(42);
    /// </code>
    /// </example>
    public static Option<T> Some<T>(T value) => new Option<T>.Some(value);

    /// <summary>
    /// Creates an empty option (no value present).
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <returns>An empty option</returns>
    /// <example>
    /// <code>
    /// var option = Option.None&lt;int&gt;();
    /// </code>
    /// </example>
    public static Option<T> None<T>() => new Option<T>.None();

    /// <summary>
    /// Creates an option from a nullable value.
    /// If the value is null, returns None; otherwise returns Some.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The nullable value</param>
    /// <returns>Some if value is not null, None otherwise</returns>
    /// <example>
    /// <code>
    /// string? nullableStr = GetNullableString();
    /// var option = Option.FromNullable(nullableStr);
    /// // option is None if nullableStr is null, Some otherwise
    /// </code>
    /// </example>
    public static Option<T> FromNullable<T>(T? value) where T : class
        => value is not null ? Some(value) : None<T>();

    /// <summary>
    /// Creates an option from a nullable struct.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The nullable struct</param>
    /// <returns>Some if value has a value, None otherwise</returns>
    /// <example>
    /// <code>
    /// int? nullableInt = GetNullableInt();
    /// var option = Option.FromNullable(nullableInt);
    /// </code>
    /// </example>
    public static Option<T> FromNullable<T>(T? value) where T : struct
        => value.HasValue ? Some(value.Value) : None<T>();
}
