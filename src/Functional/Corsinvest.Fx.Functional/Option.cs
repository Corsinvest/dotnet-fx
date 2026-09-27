/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using System.Diagnostics.CodeAnalysis;

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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static Option<T> Some<T>(T value)
    {
        // A Some holding null is the one thing Option<T> exists to rule out: it reports IsSome
        // while carrying nothing, so the NullReferenceException it was meant to prevent surfaces
        // later, inside a Map or Bind, far from where the null entered. Nullable analysis catches
        // the obvious cases, but it is advisory - a `!`, an unannotated library, or a
        // reference-type T in a nullable-oblivious context all walk straight past it. Use
        // FromNullable to turn a possible null into None.
        //
        // The `default(T) is null` guard is what keeps the check free on a value type. Both
        // ArgumentNullException.ThrowIfNull (which takes `object?`) and a bare `value is null`
        // box a value-type T on every call just to compare it against null - measured at 48 B
        // per Some(42) against 24 B without the check, for a test that cannot fail. Guarding on
        // `default(T)` instead tests a constant the compiler folds per constructed type, so for
        // a value-type T the whole condition is discarded before the boxing comparison is
        // reached, and Some(42) costs exactly what it did with no check at all.
        if (default(T) is null && value is null) { ThrowValueNull(); }

        return new Option<T>.Some(new Some<T>(value));
    }

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
    /// <remarks>
    /// Returns a shared instance rather than allocating: a None carries no data, so every one of
    /// them is equal to every other. Record equality is by value, which keeps the sharing
    /// invisible - the result compares, matches, switches and hashes exactly as a freshly
    /// constructed None would.
    /// </remarks>
    public static Option<T> None<T>() => Option<T>.None.Shared;

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

    /// <summary>
    /// Throws for <see cref="Some{T}"/>'s null check, kept out of line so the method that calls it
    /// stays small enough for the JIT to inline.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowValueNull()
        => throw new ArgumentNullException(
            "value",
            "Option.Some cannot hold null - it would report IsSome while carrying nothing. "
            + "Use Option.FromNullable to turn a possible null into None.");
}
