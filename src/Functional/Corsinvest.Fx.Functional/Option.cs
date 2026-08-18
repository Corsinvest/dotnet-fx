namespace Corsinvest.Fx.Functional;

/// <summary>Represents the absence of a value.</summary>
public sealed record None;

/// <summary>Represents a present value.</summary>
/// <typeparam name="T">The type of the value</typeparam>
/// <param name="Value">The contained value</param>
public sealed record Some<T>(T Value);

/// <summary>
/// Represents an optional value that may or may not be present.
/// </summary>
/// <typeparam name="T">The type of the optional value</typeparam>
/// <remarks>
/// A discriminated union with two cases, <see cref="Some{T}"/> and <see cref="None"/>, declared
/// through <see cref="IUnion{T1,T2}"/>. The cases are standalone types; the generated wrappers
/// <c>Option&lt;T&gt;.Some</c> and <c>Option&lt;T&gt;.None</c> are what a <c>switch</c> matches on.
/// </remarks>
/// <example>
/// <code>
/// var name = FindUser(42) switch
/// {
///     Option&lt;User&gt;.Some(var some) =&gt; some.Value.Name,
///     Option&lt;User&gt;.None =&gt; "unknown"
/// };
/// </code>
/// </example>
public abstract partial record Option<T> : IUnion<Some<T>, None>;

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
    public static Option<T> Some<T>(T value) => new Option<T>.Some(new Some<T>(value));

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
    public static Option<T> None<T>() => new Option<T>.None(new None());

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
