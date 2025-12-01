namespace Corsinvest.Fx.Functional;

/// <summary>
/// Extension methods for <see cref="Option{T}"/>.
/// </summary>
public static class OptionExtensions
{
    // ============================================
    // Unwrapping
    // ============================================

    /// <summary>
    /// Gets the value or returns a default value if None.
    /// </summary>
    public static T GetValueOr<T>(this Option<T> option, T defaultValue)
        => option.Match(
            some => some.Value,
            none => defaultValue
        );

    /// <summary>
    /// Gets the value or computes a default value if None.
    /// </summary>
    public static T GetValueOr<T>(this Option<T> option, Func<T> defaultFactory)
        => option.Match(
            some => some.Value,
            none => defaultFactory()
        );

    /// <summary>
    /// Gets the value or throws an exception if None.
    /// </summary>
    public static T GetValueOrThrow<T>(this Option<T> option, string? message = null)
        => option.Match(
            some => some.Value,
            none => throw new InvalidOperationException(message ?? "Option has no value")
        );

    /// <summary>
    /// Tries to get the value. Returns true if Some, false if None.
    /// </summary>
    public static bool TryGetValue<T>(this Option<T> option, out T value)
    {
        if (option.TryGetSome(out var some))
        {
            value = some.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Converts the option to a nullable reference.
    /// </summary>
    public static T? ToNullable<T>(this Option<T> option) where T : class
        => option.Match(
            some => some.Value,
            none => (T?)null
        );

    /// <summary>
    /// Converts the option to a nullable struct.
    /// </summary>
    public static T? ToNullableStruct<T>(this Option<T> option) where T : struct
        => option.Match<T?>(
            some => some.Value,
            none => null
        );

    // ============================================
    // Functional Combinators
    // ============================================

    /// <summary>
    /// Maps the value inside the option using the specified function.
    /// If None, returns None.
    /// </summary>
    public static Option<TResult> Map<T, TResult>(this Option<T> option, Func<T, TResult> mapper)
        => option.Match(
            some => Option.Some(mapper(some.Value)),
            none => Option.None<TResult>()
        );

    /// <summary>
    /// Maps the value and flattens the result (flatMap/bind).
    /// If None, returns None.
    /// </summary>
    public static Option<TResult> Bind<T, TResult>(this Option<T> option, Func<T, Option<TResult>> binder)
        => option.Match(
            some => binder(some.Value),
            none => Option.None<TResult>()
        );

    /// <summary>
    /// Filters the option based on a predicate.
    /// Returns None if the predicate fails or if already None.
    /// </summary>
    public static Option<T> Filter<T>(this Option<T> option, Func<T, bool> predicate)
        => option.Match(
            some => predicate(some.Value) ? option : Option.None<T>(),
            none => Option.None<T>()
        );

    /// <summary>
    /// Executes an action on the value if Some, does nothing if None.
    /// Returns the original option for chaining.
    /// </summary>
    public static Option<T> Tap<T>(this Option<T> option, Action<T> action)
    {
        option.Match(
            some => action(some.Value),
            none => { }
        );
        return option;
    }

    /// <summary>
    /// Flattens a nested Option into a single-level Option.
    /// If the outer option is None, returns None. If the outer option is Some(None), returns None.
    /// If the outer option is Some(Some(value)), returns Some(value).
    /// </summary>
    /// <typeparam name="T">The type of the inner value</typeparam>
    /// <param name="option">The nested option to flatten</param>
    /// <returns>A flattened option</returns>
    /// <remarks>
    /// <para>
    /// This method is useful when you have nested Option values from chaining operations.
    /// It simplifies <c>Option&lt;Option&lt;T&gt;&gt;</c> to <c>Option&lt;T&gt;</c>.
    /// </para>
    /// <para>
    /// <strong>Use cases:</strong>
    /// - When Map/Select returns an Option and you want to avoid nesting
    /// - When parsing/validation returns optional results of optional results
    /// - When chaining multiple optional lookups
    /// </para>
    /// </remarks>
    /// <example>
    /// Basic flattening:
    /// <code>
    /// Option&lt;Option&lt;int&gt;&gt; nested = Option.Some(Option.Some(42));
    /// Option&lt;int&gt; flat = nested.Flatten();  // Some(42)
    ///
    /// Option&lt;Option&lt;int&gt;&gt; nestedNone = Option.Some(Option.None&lt;int&gt;());
    /// Option&lt;int&gt; flatNone = nestedNone.Flatten();  // None
    /// </code>
    ///
    /// Real-world example - parsing with validation:
    /// <code>
    /// Option&lt;string&gt; TryParseInt(string input)
    ///     => int.TryParse(input, out var value)
    ///         ? Option.Some(input)
    ///         : Option.None&lt;string&gt;();
    ///
    /// Option&lt;int&gt; ValidatePositive(int value)
    ///     => value > 0
    ///         ? Option.Some(value)
    ///         : Option.None&lt;int&gt;();
    ///
    /// // Without Flatten - nested Option
    /// Option&lt;Option&lt;int&gt;&gt; nested = TryParseInt("42")
    ///     .Map(ValidatePositive);  // Option&lt;Option&lt;int&gt;&gt;
    ///
    /// // With Flatten - clean result
    /// Option&lt;int&gt; result = TryParseInt("42")
    ///     .Map(ValidatePositive)
    ///     .Flatten();  // Option&lt;int&gt;
    ///
    /// // Or use Bind directly to avoid nesting
    /// Option&lt;int&gt; result2 = TryParseInt("42")
    ///     .Bind(ValidatePositive);  // Option&lt;int&gt; - no Flatten needed
    /// </code>
    ///
    /// Dictionary lookup chain:
    /// <code>
    /// Dictionary&lt;string, Dictionary&lt;string, int&gt;&gt; nested = ...;
    ///
    /// Option&lt;int&gt; GetNestedValue(string outerKey, string innerKey)
    /// {
    ///     var outerOption = Option.FromNullable(
    ///         nested.TryGetValue(outerKey, out var outer) ? outer : null
    ///     );
    ///
    ///     var nestedOption = outerOption.Map(dict =>
    ///         Option.FromNullable(
    ///             dict.TryGetValue(innerKey, out var inner) ? (int?)inner : null
    ///         )
    ///     );  // Option&lt;Option&lt;int&gt;&gt;
    ///
    ///     return nestedOption.Flatten();  // Option&lt;int&gt;
    /// }
    /// </code>
    /// </example>
    public static Option<T> Flatten<T>(this Option<Option<T>> option)
        => option.Match(
            some => some.Value,
            none => Option.None<T>()
        );

    /// <summary>
    /// Returns the current option if it's Some, otherwise returns the alternative option.
    /// This allows you to specify a fallback option without unwrapping values.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="option">The primary option</param>
    /// <param name="alternative">The fallback option to use if the primary is None</param>
    /// <returns>The primary option if Some, otherwise the alternative</returns>
    /// <remarks>
    /// <para>
    /// <c>OrElse</c> is different from <c>GetValueOr</c>:
    /// - <c>GetValueOr</c> unwraps the option and returns a plain T value
    /// - <c>OrElse</c> keeps you in the Option context, returning Option&lt;T&gt;
    /// </para>
    /// <para>
    /// Use <c>OrElse</c> when you want to chain multiple optional operations before unwrapping.
    /// </para>
    /// </remarks>
    /// <example>
    /// Basic fallback chain:
    /// <code>
    /// Option&lt;User&gt; user = GetFromCache(id)
    ///     .OrElse(GetFromDatabase(id))
    ///     .OrElse(GetDefaultUser());
    /// </code>
    ///
    /// Configuration with fallbacks:
    /// <code>
    /// Option&lt;string&gt; GetConfig(string key)
    /// {
    ///     return GetFromEnvironment(key)
    ///         .OrElse(GetFromConfigFile(key))
    ///         .OrElse(GetFromDefaults(key));
    /// }
    /// </code>
    ///
    /// Comparison with GetValueOr:
    /// <code>
    /// // OrElse - stays in Option context
    /// Option&lt;int&gt; result1 = someOption
    ///     .OrElse(Option.Some(42))
    ///     .Map(x => x * 2);  // Can continue chaining
    ///
    /// // GetValueOr - unwraps to plain value
    /// int result2 = someOption
    ///     .GetValueOr(42)
    ///     * 2;  // Plain value, no longer Option
    /// </code>
    /// </example>
    public static Option<T> OrElse<T>(this Option<T> option, Option<T> alternative)
        => option.Match(
            some => option,
            none => alternative
        );

    /// <summary>
    /// Returns the current option if it's Some, otherwise computes and returns an alternative option.
    /// The alternative is only computed if needed (lazy evaluation).
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="option">The primary option</param>
    /// <param name="alternativeFactory">Function that produces the fallback option (called only if primary is None)</param>
    /// <returns>The primary option if Some, otherwise the result of calling alternativeFactory</returns>
    /// <remarks>
    /// <para>
    /// This overload is useful when computing the alternative is expensive and should only happen if needed.
    /// </para>
    /// </remarks>
    /// <example>
    /// Lazy computation of fallback:
    /// <code>
    /// Option&lt;User&gt; user = GetFromCache(id)
    ///     .OrElse(() => ExpensiveDatabaseLookup(id));  // Only called if cache miss
    /// </code>
    ///
    /// Fallback with side effects:
    /// <code>
    /// Option&lt;Config&gt; config = LoadFromFile()
    ///     .OrElse(() =>
    ///     {
    ///         _logger.LogWarning("Config file not found, using defaults");
    ///         return GetDefaultConfig();
    ///     });
    /// </code>
    ///
    /// Multiple lazy fallbacks:
    /// <code>
    /// Option&lt;Data&gt; data = GetFromCache()
    ///     .OrElse(() => GetFromPrimaryDb())
    ///     .OrElse(() => GetFromBackupDb())
    ///     .OrElse(() => GetFromArchive());
    /// </code>
    /// </example>
    public static Option<T> OrElse<T>(this Option<T> option, Func<Option<T>> alternativeFactory)
        => option.Match(
            some => option,
            none => alternativeFactory()
        );

    // ============================================
    // LINQ Support
    // ============================================

    /// <summary>
    /// LINQ Select support for Option.
    /// </summary>
    public static Option<TResult> Select<T, TResult>(this Option<T> option, Func<T, TResult> selector)
        => option.Map(selector);

    /// <summary>
    /// LINQ SelectMany support for Option (flatMap).
    /// </summary>
    public static Option<TResult> SelectMany<T, TCollection, TResult>(
        this Option<T> option,
        Func<T, Option<TCollection>> collectionSelector,
        Func<T, TCollection, TResult> resultSelector)
        => option.Bind(value => collectionSelector(value)
                                .Map(collection => resultSelector(value, collection)));

    /// <summary>
    /// LINQ Where support for Option (filter).
    /// </summary>
    public static Option<T> Where<T>(this Option<T> option, Func<T, bool> predicate)
        => option.Filter(predicate);

    // ============================================
    // Conversions
    // ============================================

    /// <summary>
    /// Converts an Option to a ResultOf.
    /// Some becomes Ok, None becomes Error with the specified error.
    /// </summary>
    public static ResultOf<T, E> ToResult<T, E>(this Option<T> option, E error)
        => option.Match(
            some => ResultOf.Ok<T, E>(some.Value),
            none => ResultOf.Fail<T, E>(error)
        );

    /// <summary>
    /// Converts an Option to a ResultOf with a computed error.
    /// </summary>
    public static ResultOf<T, E> ToResult<T, E>(this Option<T> option, Func<E> errorFactory)
        => option.Match(
            some => ResultOf.Ok<T, E>(some.Value),
            none => ResultOf.Fail<T, E>(errorFactory())
        );

    // ============================================
    // Async Extensions
    // ============================================

    /// <summary>
    /// Async version of Map.
    /// </summary>
    public static async Task<Option<TResult>> MapAsync<T, TResult>(
        this Option<T> option,
        Func<T, Task<TResult>> mapper)
        => option.IsSome
            ? Option.Some(await mapper(option.GetValueOrThrow()))
            : Option.None<TResult>();

    /// <summary>
    /// Async version of Bind.
    /// </summary>
    public static async Task<Option<TResult>> BindAsync<T, TResult>(
        this Option<T> option,
        Func<T, Task<Option<TResult>>> binder)
        => option.IsSome
            ? await binder(option.GetValueOrThrow())
            : Option.None<TResult>();

    /// <summary>
    /// Async version of Tap.
    /// </summary>
    public static async Task<Option<T>> TapAsync<T>(this Option<T> option, Func<T, Task> action)
    {
        if (option.IsSome)
        {
            await action(option.GetValueOrThrow());
        }
        return option;
    }

    /// <summary>
    /// Async version of Map for Task&lt;Option&lt;T&gt;&gt;.
    /// </summary>
    public static async Task<Option<TResult>> MapAsync<T, TResult>(
        this Task<Option<T>> optionTask,
        Func<T, TResult> mapper)
    {
        var option = await optionTask;
        return option.Map(mapper);
    }

    /// <summary>
    /// Async version of Bind for Task&lt;Option&lt;T&gt;&gt;.
    /// </summary>
    public static async Task<Option<TResult>> BindAsync<T, TResult>(
        this Task<Option<T>> optionTask,
        Func<T, Option<TResult>> binder)
    {
        var option = await optionTask;
        return option.Bind(binder);
    }
}
