namespace Corsinvest.Fx.Functional;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// Provides type-safe error handling following the Railway-Oriented Programming pattern.
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
/// <typeparam name="E">The type of the error value</typeparam>
/// <remarks>
/// <para>
/// This is a discriminated union type with two variants: Ok (success) and Fail (failure).
/// Use this type to make errors explicit in your function signatures, forcing callers to handle both cases.
/// Supports LINQ query syntax, pattern matching, and async operations.
/// </para>
/// <para>
/// <strong>Dual Naming:</strong> ResultOf provides both concise names (<c>IsOk</c>, <c>Tap</c>) for functional
/// programming style, and explicit names (<c>IsSuccess</c>, <c>OnSuccess</c>) for FluentResults compatibility.
/// All aliases are zero-cost and compile to identical IL code.
/// </para>
/// <para>
/// <strong>Global Usings:</strong> Factory methods <c>Ok()</c> and <c>Fail()</c> are globally available when
/// <c>EnableFunctionalGlobalUsings</c> is true (default). Use <c>Try()</c> methods via
/// <c>EnableTryGlobalUsings</c> for exception handling.
/// </para>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// enum DbError { NotFound, ConnectionLost }
///
/// ResultOf&lt;User, DbError&gt; GetUser(int id)
/// {
///     if (!_db.Exists(id))
///         return Fail&lt;User, DbError&gt;(DbError.NotFound);  // Globally available
///
///     return Ok&lt;User, DbError&gt;(_db.Get(id));  // Globally available
/// }
///
/// // Pattern matching
/// var result = GetUser(42);
/// result.Match(
///     ok => Console.WriteLine($"Found: {ok.Value.Name}"),
///     fail => Console.WriteLine($"Error: {fail.ErrorValue}")
/// );
///
/// // Dual naming support
/// if (result.IsOk)          // Concise functional style
///     ProcessUser(result);
///
/// if (result.IsSuccess)     // Explicit FluentResults style
///     ProcessUser(result);
/// </code>
/// </example>
[Union]
public partial record ResultOf<T, E>
{
    /// <summary>
    /// Represents a successful operation with a value.
    /// </summary>
    /// <param name="Value">The success value</param>
    public partial record Ok(T Value);

    /// <summary>
    /// Represents a failed operation with an error.
    /// </summary>
    /// <param name="ErrorValue">The error value</param>
    public partial record Fail(E ErrorValue);

    /// <summary>
    /// Gets a value indicating whether this result represents a successful operation.
    /// Alias for the auto-generated <c>IsOk</c> property. Use <c>IsOk</c> for concise code
    /// or <c>IsSuccess</c> for more explicit, FluentResults-compatible code.
    /// </summary>
    public bool IsSuccess => this is Ok;

    /// <summary>
    /// Gets a value indicating whether this result represents a failed operation.
    /// Alias for the auto-generated <c>IsFail</c> property. Use <c>IsFail</c> for concise code
    /// or <c>IsFailure</c> for more explicit, FluentResults-compatible code.
    /// </summary>
    public bool IsFailure => this is Fail;
}


/// <summary>
/// Provides factory methods for creating <see cref="ResultOf{T, E}"/> instances.
/// </summary>
public static class ResultOf
{
    /// <summary>
    /// Creates a successful result with a value and typed error.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the error value</typeparam>
    /// <param name="value">The success value</param>
    /// <returns>A successful result containing the specified value</returns>
    /// <example>
    /// <code>
    /// var result = ResultOf.Ok&lt;int, string&gt;(42);
    /// </code>
    /// </example>
    public static ResultOf<T, E> Ok<T, E>(T value) => new ResultOf<T, E>.Ok(value);

    /// <summary>
    /// Creates a failed result with a typed error.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the error value</typeparam>
    /// <param name="error">The error value</param>
    /// <returns>A failed result containing the specified error</returns>
    /// <example>
    /// <code>
    /// var result = ResultOf.Fail&lt;int, string&gt;("error");
    /// </code>
    /// </example>
    public static ResultOf<T, E> Fail<T, E>(E error) => new ResultOf<T, E>.Fail(error);

    /// <summary>
    /// Creates a successful result with a value (using string as error type).
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <param name="value">The success value</param>
    /// <returns>A successful result containing the specified value</returns>
    /// <example>
    /// <code>
    /// var result = ResultOf.Ok(42); // ResultOf&lt;int, string&gt;
    /// </code>
    /// </example>
    public static ResultOf<T, string> Ok<T>(T value) => new ResultOf<T, string>.Ok(value);

    /// <summary>
    /// Creates a failed result with a string error message.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <param name="error">The error message</param>
    /// <returns>A failed result containing the specified error message</returns>
    /// <example>
    /// <code>
    /// var result = ResultOf.Fail&lt;int&gt;("Not found"); // ResultOf&lt;int, string&gt;
    /// </code>
    /// </example>
    public static ResultOf<T, string> Fail<T>(string error) => new ResultOf<T, string>.Fail(error);

    /// <summary>
    /// Collects validation errors by running all validators and accumulating failures.
    /// Unlike railway-oriented programming which short-circuits on first error,
    /// this method runs ALL validators and collects all errors.
    /// </summary>
    /// <typeparam name="T">The type of the value to validate</typeparam>
    /// <typeparam name="E">The type of the error</typeparam>
    /// <param name="value">The value to validate</param>
    /// <param name="validators">Array of validator functions that return ResultOf</param>
    /// <returns>
    /// A result containing either the validated value (if all validators pass)
    /// or a list of all validation errors
    /// </returns>
    /// <example>
    /// <code>
    /// var result = ResultOf.CollectErrors(user,
    ///     u => u.Age >= 18
    ///         ? ResultOf.Ok(u)
    ///         : ResultOf.Fail&lt;User&gt;("Must be 18+"),
    ///     u => !string.IsNullOrEmpty(u.Email)
    ///         ? ResultOf.Ok(u)
    ///         : ResultOf.Fail&lt;User&gt;("Email required"),
    ///     u => u.Name.Length > 2
    ///         ? ResultOf.Ok(u)
    ///         : ResultOf.Fail&lt;User&gt;("Name too short")
    /// );
    /// // If validation fails: result.ErrorValue = ["Must be 18+", "Email required"]
    /// </code>
    /// </example>
    public static ResultOf<T, List<E>> CollectErrors<T, E>(T value, params Func<T, ResultOf<T, E>>[] validators)
    {
        if (validators.Length == 0) { return Ok<T, List<E>>(value); }

        var errors = new List<E>();
        foreach (var validator in validators)
        {
            var result = validator(value);
            if (result.TryGetFail(out var err)) { errors.Add(err.ErrorValue); }
        }

        return errors.Count != 0
            ? Fail<T, List<E>>(errors)
            : Ok<T, List<E>>(value);
    }

    /// <summary>
    /// Validates a value using simple predicate functions with error messages.
    /// This is a more concise alternative to <see cref="CollectErrors{T,E}"/> for simple validations.
    /// All validations are executed and errors are collected (no short-circuiting).
    /// </summary>
    /// <typeparam name="T">The type of the value to validate</typeparam>
    /// <typeparam name="E">The type of the error</typeparam>
    /// <param name="value">The value to validate</param>
    /// <param name="validations">
    /// Array of tuples containing a predicate (validation rule) and an error value.
    /// The predicate should return true if validation passes, false if it fails.
    /// </param>
    /// <returns>
    /// A result containing either the validated value (if all predicates pass)
    /// or a list of all validation errors
    /// </returns>
    /// <example>
    /// <code>
    /// var result = ResultOf.Validate(user,
    ///     (u => u.Age >= 18, "Must be 18 or older"),
    ///     (u => !string.IsNullOrEmpty(u.Email), "Email is required"),
    ///     (u => u.Name.Length > 2, "Name must be longer than 2 characters")
    /// );
    ///
    /// if (result.IsSuccess)
    ///     Console.WriteLine($"Valid user: {result.GetValueOrThrow().Name}");
    /// else
    ///     Console.WriteLine($"Errors: {string.Join(", ", result.GetValueOrThrow().ErrorValue)}");
    /// </code>
    /// </example>
    public static ResultOf<T, List<E>> Validate<T, E>(T value, params (Func<T, bool> Predicate, E Error)[] validations)
    {
        if (validations.Length == 0) { return Ok<T, List<E>>(value); }

        var errors = validations
            .Where(v => !v.Predicate(value))
            .Select(v => v.Error)
            .ToList();

        return errors.Count != 0
            ? Fail<T, List<E>>(errors)
            : Ok<T, List<E>>(value);
    }

    // ============================================
    // Exception Handling
    // ============================================

    /// <summary>
    /// Executes a function and catches any exceptions, converting them to a Result.
    /// Use this to safely execute operations that might throw exceptions.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <param name="func">The function to execute that might throw an exception</param>
    /// <returns>
    /// A result containing either the function's return value on success,
    /// or the caught exception on failure
    /// </returns>
    /// <example>
    /// <code>
    /// // Parse with exception handling
    /// var result = ResultOf.Try(() => int.Parse("42"));
    /// // result.IsSuccess = true, result.Value = 42
    ///
    /// var failedResult = ResultOf.Try(() => int.Parse("invalid"));
    /// // failedResult.IsFailure = true, failedResult.ErrorValue = FormatException
    ///
    /// // Use with pattern matching
    /// var output = result.Match(
    ///     ok => $"Parsed: {ok.Value}",
    ///     error => $"Error: {error.ErrorValue.Message}"
    /// );
    /// </code>
    /// </example>
    public static ResultOf<T, Exception> Try<T>(Func<T> func)
    {
        try
        {
            return Ok<T, Exception>(func());
        }
        catch (Exception ex)
        {
            return Fail<T, Exception>(ex);
        }
    }

    /// <summary>
    /// Executes a function and catches any exceptions, mapping them to a custom error type.
    /// Use this when you want to convert exceptions to your own error representation.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the custom error</typeparam>
    /// <param name="func">The function to execute that might throw an exception</param>
    /// <param name="errorMapper">Function to map the caught exception to your error type</param>
    /// <returns>
    /// A result containing either the function's return value on success,
    /// or the mapped error on failure
    /// </returns>
    /// <example>
    /// <code>
    /// // Map exception to string
    /// var result = ResultOf.Try(
    ///     () => int.Parse("42"),
    ///     ex => $"Parse failed: {ex.Message}"
    /// );
    ///
    /// // Map to custom error type
    /// enum ParseError { InvalidFormat, Overflow }
    ///
    /// var result2 = ResultOf.Try(
    ///     () => int.Parse("999999999999"),
    ///     ex => ex is FormatException ? ParseError.InvalidFormat : ParseError.Overflow
    /// );
    /// </code>
    /// </example>
    public static ResultOf<T, E> Try<T, E>(Func<T> func, Func<Exception, E> errorMapper)
    {
        try
        {
            return Ok<T, E>(func());
        }
        catch (Exception ex)
        {
            return Fail<T, E>(errorMapper(ex));
        }
    }

    /// <summary>
    /// Executes an async function and catches any exceptions, converting them to a Result.
    /// Use this to safely execute async operations that might throw exceptions.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <param name="func">The async function to execute that might throw an exception</param>
    /// <returns>
    /// A task that resolves to a result containing either the function's return value on success,
    /// or the caught exception on failure
    /// </returns>
    /// <example>
    /// <code>
    /// // Async operation with exception handling
    /// var result = await ResultOf.TryAsync(async () =>
    /// {
    ///     var response = await httpClient.GetAsync("https://api.example.com/data");
    ///     response.EnsureSuccessStatusCode();
    ///     return await response.Content.ReadAsStringAsync();
    /// });
    ///
    /// result.Match(
    ///     ok => Console.WriteLine($"Success: {ok.Value}"),
    ///     error => Console.WriteLine($"Error: {error.ErrorValue.Message}")
    /// );
    /// </code>
    /// </example>
    public static async Task<ResultOf<T, Exception>> TryAsync<T>(Func<Task<T>> func)
    {
        try
        {
            return Ok<T, Exception>(await func());
        }
        catch (Exception ex)
        {
            return Fail<T, Exception>(ex);
        }
    }

    /// <summary>
    /// Executes an async function and catches any exceptions, mapping them to a custom error type.
    /// Use this when you want to convert exceptions from async operations to your own error representation.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    /// <typeparam name="E">The type of the custom error</typeparam>
    /// <param name="func">The async function to execute that might throw an exception</param>
    /// <param name="errorMapper">Function to map the caught exception to your error type</param>
    /// <returns>
    /// A task that resolves to a result containing either the function's return value on success,
    /// or the mapped error on failure
    /// </returns>
    /// <example>
    /// <code>
    /// // Map exception to custom error
    /// enum ApiError { NetworkError, InvalidData, Unauthorized }
    ///
    /// var result = await ResultOf.TryAsync(
    ///     async () => await FetchUserAsync(42),
    ///     ex => ex is HttpRequestException ? ApiError.NetworkError : ApiError.InvalidData
    /// );
    ///
    /// // Use in pipeline
    /// var user = await ResultOf.TryAsync(
    ///         async () => await FetchUserAsync(42),
    ///         ex => $"Fetch failed: {ex.Message}"
    ///     )
    ///     .MapAsync(async user => await EnrichUserAsync(user));
    /// </code>
    /// </example>
    public static async Task<ResultOf<T, E>> TryAsync<T, E>(Func<Task<T>> func, Func<Exception, E> errorMapper)
    {
        try
        {
            return Ok<T, E>(await func());
        }
        catch (Exception ex)
        {
            return Fail<T, E>(errorMapper(ex));
        }
    }

    // ============================================
    // Combining Results
    // ============================================

    /// <summary>
    /// Combines multiple results into a single result containing an array of values.
    /// If any result is a failure, returns a failure containing all errors.
    /// Use this when you have multiple independent operations and want to collect all errors.
    /// </summary>
    /// <typeparam name="T">The type of the success values</typeparam>
    /// <typeparam name="E">The type of the errors</typeparam>
    /// <param name="results">The results to combine</param>
    /// <returns>
    /// A result containing either an array of all success values (if all succeed),
    /// or a list of all errors (if any fail)
    /// </returns>
    /// <example>
    /// <code>
    /// // Validate multiple fields independently
    /// var emailResult = ValidateEmail(user.Email);
    /// var ageResult = ValidateAge(user.Age);
    /// var nameResult = ValidateName(user.Name);
    ///
    /// var combined = ResultOf.Combine(emailResult, ageResult, nameResult);
    /// // If all succeed: Ok with [email, age, name]
    /// // If any fail: Fail with list of all errors
    ///
    /// combined.Match(
    ///     ok => Console.WriteLine($"All valid: {ok.Value.Length} fields"),
    ///     errors => Console.WriteLine($"Errors: {string.Join(", ", errors.ErrorValue)}")
    /// );
    /// </code>
    /// </example>
    public static ResultOf<T[], List<E>> Combine<T, E>(params ResultOf<T, E>[] results)
    {
        if (results.Length == 0) { return Ok<T[], List<E>>([]); }

        var errors = new List<E>();
        var values = new List<T>();

        foreach (var result in results)
        {
            result.Match(
                ok => values.Add(ok.Value),
                error => errors.Add(error.ErrorValue)
            );
        }

        return errors.Count > 0
            ? Fail<T[], List<E>>(errors)
            : Ok<T[], List<E>>([.. values]);
    }

    /// <summary>
    /// Combines multiple results with different value types into a single result.
    /// If any result is a failure, returns a failure containing all errors.
    /// This overload allows combining results of different types.
    /// </summary>
    /// <typeparam name="T1">The type of the first success value</typeparam>
    /// <typeparam name="T2">The type of the second success value</typeparam>
    /// <typeparam name="E">The type of the errors</typeparam>
    /// <param name="result1">The first result</param>
    /// <param name="result2">The second result</param>
    /// <returns>
    /// A result containing either a tuple of both success values (if both succeed),
    /// or a list of all errors (if any fail)
    /// </returns>
    /// <example>
    /// <code>
    /// var userResult = GetUser(42);      // ResultOf&lt;User, string&gt;
    /// var configResult = GetConfig();    // ResultOf&lt;Config, string&gt;
    ///
    /// var combined = ResultOf.Combine(userResult, configResult);
    /// // ResultOf&lt;(User, Config), List&lt;string&gt;&gt;
    ///
    /// combined.Match(
    ///     ok => ProcessUserWithConfig(ok.Value.Item1, ok.Value.Item2),
    ///     errors => LogErrors(errors.ErrorValue)
    /// );
    /// </code>
    /// </example>
    public static ResultOf<(T1, T2), List<E>> Combine<T1, T2, E>(ResultOf<T1, E> result1,
                                                                 ResultOf<T2, E> result2)
    {
        var errors = new List<E>();

        var hasValue1 = result1.TryGetValue(out var value1);
        var hasValue2 = result2.TryGetValue(out var value2);

        if (!hasValue1)
        {
            result1.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }

        if (!hasValue2)
        {
            result2.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }


        return errors.Count > 0
            ? Fail<(T1, T2), List<E>>(errors)
            : Ok<(T1, T2), List<E>>((value1, value2));
    }

    /// <summary>
    /// Combines three results with different value types into a single result.
    /// </summary>
    public static ResultOf<(T1, T2, T3), List<E>> Combine<T1, T2, T3, E>(ResultOf<T1, E> result1,
                                                                         ResultOf<T2, E> result2,
                                                                         ResultOf<T3, E> result3)
    {
        var errors = new List<E>();

        var hasValue1 = result1.TryGetValue(out var value1);
        var hasValue2 = result2.TryGetValue(out var value2);
        var hasValue3 = result3.TryGetValue(out var value3);

        if (!hasValue1)
        {
            result1.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }

        if (!hasValue2)
        {
            result2.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }

        if (!hasValue3)
        {
            result3.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }


        return errors.Count > 0
            ? Fail<(T1, T2, T3), List<E>>(errors)
            : Ok<(T1, T2, T3), List<E>>((value1, value2, value3));
    }

    /// <summary>
    /// Combines four results with different value types into a single result.
    /// </summary>
    public static ResultOf<(T1, T2, T3, T4), List<E>> Combine<T1, T2, T3, T4, E>(ResultOf<T1, E> result1,
                                                                                 ResultOf<T2, E> result2,
                                                                                 ResultOf<T3, E> result3,
                                                                                 ResultOf<T4, E> result4)
    {
        var errors = new List<E>();

        var hasValue1 = result1.TryGetValue(out var value1);
        var hasValue2 = result2.TryGetValue(out var value2);
        var hasValue3 = result3.TryGetValue(out var value3);
        var hasValue4 = result4.TryGetValue(out var value4);

        if (!hasValue1)
        {
            result1.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }

        if (!hasValue2)
        {
            result2.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }

        if (!hasValue3)
        {
            result3.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }


        if (!hasValue4)
        {
            result4.Match(_ => { }, error => errors.Add(error.ErrorValue));
        }


        return errors.Count > 0
            ? Fail<(T1, T2, T3, T4), List<E>>(errors)
            : Ok<(T1, T2, T3, T4), List<E>>((value1, value2, value3, value4));
    }
}
