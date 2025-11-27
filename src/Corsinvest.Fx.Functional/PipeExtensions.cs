namespace Corsinvest.Fx.Functional;

/// <summary>
/// Universal pipe extensions for fluent data transformation on any type.
/// Enables functional pipeline composition with forward data flow.
/// </summary>
/// <remarks>
/// The pipe pattern allows you to chain transformations in a left-to-right,
/// top-to-bottom manner that matches the order of execution, making code
/// more readable than nested function calls.
///
/// Example:
/// <code>
/// var result = 5.0
///     .Pipe(Power, 2)           // 5^2 = 25
///     .Pipe(x => x + 10)        // 25 + 10 = 35
///     .Pipe(Clamp, 0, 30);      // Clamp(35, 0, 30) = 30
///
/// // Instead of: Clamp(Power(5.0, 2) + 10, 0, 30)
/// </code>
/// </remarks>
public static class PipeExtensions
{
    // ============================================
    // Synchronous Pipe
    // ============================================

    /// <summary>
    /// Pipes a value through a transformation function.
    /// This is the fundamental pipe operation that takes a value and applies a function to it.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The value to transform</param>
    /// <param name="func">The transformation function</param>
    /// <returns>The transformed value</returns>
    /// <example>
    /// <code>
    /// var result = "hello"
    ///     .Pipe(s => s.ToUpper())      // "HELLO"
    ///     .Pipe(s => s + "!")          // "HELLO!"
    ///     .Pipe(s => s.Length);        // 6
    /// </code>
    /// </example>
    public static TOut Pipe<TIn, TOut>(this TIn value, Func<TIn, TOut> func)
        => func(value);

    /// <summary>
    /// Pipes a value through a transformation function with one additional parameter.
    /// Useful for functions that take the piped value as the first parameter.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="T2">The type of the second parameter</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The value to transform</param>
    /// <param name="func">The transformation function</param>
    /// <param name="arg2">The second argument to pass to the function</param>
    /// <returns>The transformed value</returns>
    /// <example>
    /// <code>
    /// static double Power(double x, int exp) => Math.Pow(x, exp);
    ///
    /// var result = 5.0
    ///     .Pipe(Power, 2);  // Power(5.0, 2) = 25.0
    /// </code>
    /// </example>
    public static TOut Pipe<TIn, T2, TOut>(this TIn value, Func<TIn, T2, TOut> func, T2 arg2)
        => func(value, arg2);

    /// <summary>
    /// Pipes a value through a transformation function with two additional parameters.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="T2">The type of the second parameter</typeparam>
    /// <typeparam name="T3">The type of the third parameter</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The value to transform</param>
    /// <param name="func">The transformation function</param>
    /// <param name="arg2">The second argument to pass to the function</param>
    /// <param name="arg3">The third argument to pass to the function</param>
    /// <returns>The transformed value</returns>
    /// <example>
    /// <code>
    /// static double Clamp(double value, double min, double max)
    ///     => Math.Max(min, Math.Min(max, value));
    ///
    /// var result = 35.0
    ///     .Pipe(Clamp, 0, 30);  // Clamp(35.0, 0, 30) = 30.0
    /// </code>
    /// </example>
    public static TOut Pipe<TIn, T2, T3, TOut>(this TIn value, Func<TIn, T2, T3, TOut> func, T2 arg2, T3 arg3)
        => func(value, arg2, arg3);

    // ============================================
    // Asynchronous Pipe
    // ============================================

    /// <summary>
    /// Pipes a value through an async transformation function.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The value to transform</param>
    /// <param name="func">The async transformation function</param>
    /// <returns>A task containing the transformed value</returns>
    /// <example>
    /// <code>
    /// var user = await userId
    ///     .PipeAsync(FetchUserAsync)
    ///     .PipeAsync(EnrichUserDataAsync)
    ///     .PipeAsync(SaveToCacheAsync);
    /// </code>
    /// </example>
    public static async Task<TOut> PipeAsync<TIn, TOut>(this TIn value, Func<TIn, Task<TOut>> func)
        => await func(value);

    /// <summary>
    /// Pipes a Task value through a transformation function.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="valueTask">The task containing the value to transform</param>
    /// <param name="func">The transformation function</param>
    /// <returns>A task containing the transformed value</returns>
    public static async Task<TOut> Pipe<TIn, TOut>(this Task<TIn> valueTask, Func<TIn, TOut> func)
    {
        var value = await valueTask;
        return func(value);
    }

    /// <summary>
    /// Pipes a Task value through an async transformation function.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="valueTask">The task containing the value to transform</param>
    /// <param name="func">The async transformation function</param>
    /// <returns>A task containing the transformed value</returns>
    /// <example>
    /// <code>
    /// var result = await GetDataAsync()
    ///     .PipeAsync(ValidateAsync)
    ///     .PipeAsync(TransformAsync)
    ///     .PipeAsync(SaveAsync);
    /// </code>
    /// </example>
    public static async Task<TOut> PipeAsync<TIn, TOut>(this Task<TIn> valueTask, Func<TIn, Task<TOut>> func)
    {
        var value = await valueTask;
        return await func(value);
    }

    /// <summary>
    /// Pipes a value through an async transformation function with one additional parameter.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="T2">The type of the second parameter</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The value to transform</param>
    /// <param name="func">The async transformation function</param>
    /// <param name="arg2">The second argument to pass to the function</param>
    /// <returns>A task containing the transformed value</returns>
    public static async Task<TOut> PipeAsync<TIn, T2, TOut>(this TIn value, Func<TIn, T2, Task<TOut>> func, T2 arg2)
        => await func(value, arg2);

    /// <summary>
    /// Pipes a value through an async transformation function with two additional parameters.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="T2">The type of the second parameter</typeparam>
    /// <typeparam name="T3">The type of the third parameter</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The value to transform</param>
    /// <param name="func">The async transformation function</param>
    /// <param name="arg2">The second argument to pass to the function</param>
    /// <param name="arg3">The third argument to pass to the function</param>
    /// <returns>A task containing the transformed value</returns>
    public static async Task<TOut> PipeAsync<TIn, T2, T3, TOut>(this TIn value, Func<TIn, T2, T3, Task<TOut>> func, T2 arg2, T3 arg3)
        => await func(value, arg2, arg3);

    // ============================================
    // Tap (Side Effects)
    // ============================================

    /// <summary>
    /// Executes a side effect on the value and returns the original value unchanged.
    /// Use this for logging, debugging, or other operations that shouldn't modify the pipeline.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to perform the side effect on</param>
    /// <param name="action">The side effect action</param>
    /// <returns>The original value, unchanged</returns>
    /// <example>
    /// <code>
    /// var result = GetData()
    ///     .Pipe(Validate)
    ///     .Tap(data => Console.WriteLine($"Processing: {data}"))
    ///     .Pipe(Transform)
    ///     .Tap(result => _logger.LogInfo($"Result: {result}"))
    ///     .Pipe(Save);
    /// </code>
    /// </example>
    public static T Tap<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }

    /// <summary>
    /// Executes an async side effect on the value and returns the original value unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to perform the side effect on</param>
    /// <param name="action">The async side effect action</param>
    /// <returns>A task containing the original value, unchanged</returns>
    /// <example>
    /// <code>
    /// var user = await FetchUserAsync(userId)
    ///     .PipeAsync(DeserializeUser)
    ///     .TapAsync(u => LogAsync($"User: {u.Name}"))
    ///     .PipeAsync(EnrichUserData)
    ///     .PipeAsync(SaveToCache);
    /// </code>
    /// </example>
    public static async Task<T> TapAsync<T>(this T value, Func<T, Task> action)
    {
        await action(value);
        return value;
    }

    /// <summary>
    /// Executes a side effect on the value only if the condition is true,
    /// and returns the original value unchanged.
    /// Useful for logging, debugging, or other operations that should
    /// not modify the pipeline.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to perform the side effect on</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="action">The side effect action to execute if condition is true</param>
    /// <returns>The original value, unchanged</returns>
    /// <example>
    /// <code>
    /// var result = data
    ///     .Pipe(Transform)
    ///     .TapIf(shouldLog, x => Console.WriteLine($"Processing: {x}"))
    ///     .Pipe(Save);
    /// </code>
    /// </example>
    public static T TapIf<T>(this T value, bool condition, Action<T> action)
    {
        if (condition)
        {
            action(value);
        }
        return value;
    }

    /// <summary>
    /// Executes an asynchronous side effect on the value only if the condition is true,
    /// and returns the original value unchanged.
    /// Useful for logging, auditing, or async operations in a pipeline.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to perform the side effect on</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="action">The async side effect action to execute if condition is true</param>
    /// <returns>A task containing the original value, unchanged</returns>
    /// <example>
    /// <code>
    /// var result = await data
    ///     .PipeAsync(TransformAsync)
    ///     .TapIfAsync(shouldLog, async x => await LogAsync(x))
    ///     .PipeAsync(SaveAsync);
    /// </code>
    /// </example>
    public static async Task<T> TapIfAsync<T>(this T value, bool condition, Func<T, Task> action)
    {
        if (condition)
        {
            await action(value);
        }
        return value;
    }

    /// <summary>
    /// Executes an asynchronous side effect on a Task value only if the condition is true,
    /// and returns the original value unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueTask">The task containing the value</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="action">The async side effect action to execute if condition is true</param>
    /// <returns>A task containing the original value, unchanged</returns>
    public static async Task<T> TapIfAsync<T>(this Task<T> valueTask, bool condition, Func<T, Task> action)
    {
        var value = await valueTask;
        if (condition)
        {
            await action(value);
        }
        return value;
    }

    /// <summary>
    /// Executes a side effect on a Task value and returns the original value unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueTask">The task containing the value</param>
    /// <param name="action">The side effect action</param>
    /// <returns>A task containing the original value, unchanged</returns>
    public static async Task<T> Tap<T>(this Task<T> valueTask, Action<T> action)
    {
        var value = await valueTask;
        action(value);
        return value;
    }

    /// <summary>
    /// Executes an async side effect on a Task value and returns the original value unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueTask">The task containing the value</param>
    /// <param name="action">The async side effect action</param>
    /// <returns>A task containing the original value, unchanged</returns>
    public static async Task<T> TapAsync<T>(this Task<T> valueTask, Func<T, Task> action)
    {
        var value = await valueTask;
        await action(value);
        return value;
    }

    // ============================================
    // Conditional Pipe
    // ============================================

    /// <summary>
    /// Conditionally pipes a value through a transformation.
    /// If the condition is true, applies the transformation; otherwise returns the original value.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to potentially transform</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="func">The transformation function to apply if condition is true</param>
    /// <returns>The transformed value if condition is true, otherwise the original value</returns>
    /// <example>
    /// <code>
    /// var result = data
    ///     .Pipe(Validate)
    ///     .PipeIf(shouldNormalize, Normalize)
    ///     .PipeIf(shouldEnrich, Enrich)
    ///     .Pipe(Save);
    /// </code>
    /// </example>
    public static T PipeIf<T>(this T value, bool condition, Func<T, T> func)
        => condition ? func(value) : value;

    /// <summary>
    /// Conditionally pipes a value through a transformation based on a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to potentially transform</param>
    /// <param name="predicate">The predicate to evaluate on the value</param>
    /// <param name="func">The transformation function to apply if predicate returns true</param>
    /// <returns>The transformed value if predicate is true, otherwise the original value</returns>
    /// <example>
    /// <code>
    /// var result = text
    ///     .PipeIf(s => s.Length > 100, Truncate)
    ///     .PipeIf(s => string.IsNullOrWhiteSpace(s), _ => "default");
    /// </code>
    /// </example>
    public static T PipeIf<T>(this T value, Func<T, bool> predicate, Func<T, T> func)
        => predicate(value) ? func(value) : value;

    /// <summary>
    /// Conditionally pipes a value through an async transformation.
    /// If the condition is true, applies the async transformation; otherwise returns the original value.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to potentially transform</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="func">The async transformation function to apply if condition is true</param>
    /// <returns>A task containing the transformed value if condition is true, otherwise the original value</returns>
    /// <example>
    /// <code>
    /// var result = await data
    ///     .PipeAsync(ValidateAsync)
    ///     .PipeIfAsync(shouldEnrich, EnrichAsync)
    ///     .PipeIfAsync(shouldNormalize, NormalizeAsync)
    ///     .PipeAsync(SaveAsync);
    /// </code>
    /// </example>
    public static async Task<T> PipeIfAsync<T>(this T value, bool condition, Func<T, Task<T>> func)
        => condition ? await func(value) : value;

    /// <summary>
    /// Conditionally pipes a value through an async transformation based on a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to potentially transform</param>
    /// <param name="predicate">The predicate to evaluate on the value</param>
    /// <param name="func">The async transformation function to apply if predicate returns true</param>
    /// <returns>A task containing the transformed value if predicate is true, otherwise the original value</returns>
    /// <example>
    /// <code>
    /// var result = await text
    ///     .PipeIfAsync(s => s.Length > 100, TruncateAsync)
    ///     .PipeIfAsync(s => string.IsNullOrWhiteSpace(s), _ => Task.FromResult("default"));
    /// </code>
    /// </example>
    public static async Task<T> PipeIfAsync<T>(this T value, Func<T, bool> predicate, Func<T, Task<T>> func)
        => predicate(value) ? await func(value) : value;

    /// <summary>
    /// Conditionally pipes a Task value through an async transformation.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueTask">The task containing the value to potentially transform</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="func">The async transformation function to apply if condition is true</param>
    /// <returns>A task containing the transformed value if condition is true, otherwise the original value</returns>
    public static async Task<T> PipeIfAsync<T>(this Task<T> valueTask, bool condition, Func<T, Task<T>> func)
    {
        var value = await valueTask;
        return condition ? await func(value) : value;
    }

    /// <summary>
    /// Conditionally pipes a Task value through an async transformation based on a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueTask">The task containing the value to potentially transform</param>
    /// <param name="predicate">The predicate to evaluate on the value</param>
    /// <param name="func">The async transformation function to apply if predicate returns true</param>
    /// <returns>A task containing the transformed value if predicate is true, otherwise the original value</returns>
    /// <example>
    /// <code>
    /// var result = await LoadDataAsync()
    ///     .PipeIfAsync(x => x > 100, TruncateAsync)
    ///     .PipeIfAsync(x => string.IsNullOrWhiteSpace(x), _ => Task.FromResult("default"));
    /// </code>
    /// </example>
    public static async Task<T> PipeIfAsync<T>(this Task<T> valueTask, Func<T, bool> predicate, Func<T, Task<T>> func)
    {
        var value = await valueTask;
        return predicate(value) ? await func(value) : value;
    }

    /// <summary>
    /// Pipes a value through one of two transformations based on a condition.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The value to transform</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="ifTrue">The transformation to apply if condition is true</param>
    /// <param name="ifFalse">The transformation to apply if condition is false</param>
    /// <returns>The result of applying either ifTrue or ifFalse transformation</returns>
    /// <example>
    /// <code>
    /// var result = user
    ///     .PipeEither(user.IsAdmin, ProcessAsAdmin, ProcessAsUser);
    /// </code>
    /// </example>
    public static TOut PipeEither<TIn, TOut>(this TIn value, bool condition, Func<TIn, TOut> ifTrue, Func<TIn, TOut> ifFalse)
        => condition ? ifTrue(value) : ifFalse(value);

    /// <summary>
    /// Pipes a value through one of two async transformations based on a condition.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="value">The value to transform</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="ifTrue">The async transformation to apply if condition is true</param>
    /// <param name="ifFalse">The async transformation to apply if condition is false</param>
    /// <returns>A task containing the result of applying either ifTrue or ifFalse transformation</returns>
    /// <example>
    /// <code>
    /// var result = await user
    ///     .PipeEitherAsync(user.IsAdmin, ProcessAsAdminAsync, ProcessAsUserAsync);
    /// </code>
    /// </example>
    public static async Task<TOut> PipeEitherAsync<TIn, TOut>(this TIn value, bool condition, Func<TIn, Task<TOut>> ifTrue, Func<TIn, Task<TOut>> ifFalse)
        => condition ? await ifTrue(value) : await ifFalse(value);

    /// <summary>
    /// Pipes a task value through one of two async transformations based on a condition.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="valueTask">The task containing the value to transform</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="ifTrue">The async transformation to apply if condition is true</param>
    /// <param name="ifFalse">The async transformation to apply if condition is false</param>
    /// <returns>A task containing the result of applying either ifTrue or ifFalse transformation</returns>
    /// <example>
    /// <code>
    /// var result = await LoadUserAsync()
    ///     .PipeEitherAsync(user => user.IsAdmin, ProcessAsAdminAsync, ProcessAsUserAsync);
    /// </code>
    /// </example>
    public static async Task<TOut> PipeEitherAsync<TIn, TOut>(this Task<TIn> valueTask, bool condition, Func<TIn, Task<TOut>> ifTrue, Func<TIn, Task<TOut>> ifFalse)
    {
        var value = await valueTask;
        return condition ? await ifTrue(value) : await ifFalse(value);
    }

    /// <summary>
    /// Pipes a task value through one of two async transformations based on a predicate.
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="valueTask">The task containing the value to transform</param>
    /// <param name="predicate">The predicate to evaluate on the value</param>
    /// <param name="ifTrue">The async transformation to apply if predicate returns true</param>
    /// <param name="ifFalse">The async transformation to apply if predicate returns false</param>
    /// <returns>A task containing the result of applying either ifTrue or ifFalse transformation</returns>
    /// <example>
    /// <code>
    /// var result = await LoadUserAsync()
    ///     .PipeEitherAsync(u => u.IsAdmin, ProcessAsAdminAsync, ProcessAsUserAsync);
    /// </code>
    /// </example>
    public static async Task<TOut> PipeEitherAsync<TIn, TOut>(this Task<TIn> valueTask, Func<TIn, bool> predicate, Func<TIn, Task<TOut>> ifTrue, Func<TIn, Task<TOut>> ifFalse)
    {
        var value = await valueTask;
        return predicate(value) ? await ifTrue(value) : await ifFalse(value);
    }
}
