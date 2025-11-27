namespace Corsinvest.Fx.Functional;

/// <summary>
/// Marks a partial record as a discriminated union.
/// The record must contain nested partial records representing the union variants.
/// </summary>
/// <remarks>
/// The generator will create:
/// - Pattern matching methods (Match, MatchAsync)
/// - Type checking properties (Is{Variant})
/// - Safe extraction methods (TryGet{Variant})
///
/// Functional extensions (Map, Bind, Tap) are available separately via extension methods.
/// </remarks>
/// <example>
/// <code>
/// [Union]
/// public partial record Result&lt;T, E&gt;
/// {
///     public partial record Ok(T Value);
///     public partial record Error(E Error);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute : Attribute { }
