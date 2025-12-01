namespace Corsinvest.Fx.CompileTime;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class CompileTimeAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds before a warning is generated.
    /// Default is 1000ms (1 second). Set to -1 to disable performance warnings for this method.
    /// </summary>
    public int PerformanceWarningThresholdMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to suppress all warnings for this method.
    /// </summary>
    public bool SuppressWarnings { get; set; }

    /// <summary>
    /// Gets or sets the timeout in milliseconds for method execution.
    /// If the method takes longer than this time, it will be cancelled.
    /// Default is -1 (use global CompileTimeTimeout setting).
    /// </summary>
    public int TimeoutMs { get; set; } = -1;

    /// <summary>
    /// Gets or sets the cache strategy for this method.
    /// Default is Default (cache during compilation session only).
    /// </summary>
    public CacheStrategy Cache { get; set; } = CacheStrategy.Default;

    /// <summary>
    /// Gets or sets whether this method should be processed by CompileTime.
    /// Set to false to temporarily disable compile-time execution for this method.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
