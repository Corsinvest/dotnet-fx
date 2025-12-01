namespace Corsinvest.Fx.CompileTime;

/// <summary>
/// Defines cache strategy for compile-time method execution
/// </summary>
public enum CacheStrategy
{
    /// <summary>
    /// Cache normally during compilation session (default)
    /// </summary>
    Default = 0,

    /// <summary>
    /// Cache persistently across builds in JSON file (for deterministic methods)
    /// </summary>
    Persistent = 1,

    /// <summary>
    /// Never cache the result (for non-deterministic methods like DateTime.Now)
    /// </summary>
    Never = 2
}
