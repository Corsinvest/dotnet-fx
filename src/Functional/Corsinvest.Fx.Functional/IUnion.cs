namespace Corsinvest.Fx.Functional;

/// <summary>
/// Marks a partial record as a discriminated union whose cases are the supplied type arguments.
/// </summary>
/// <remarks>
/// <para>
/// The case types are ordinary standalone types, so the same type can take part in several
/// unions. The generator emits one sealed nested wrapper per case, deriving from the union root,
/// which keeps the hierarchy closed and lets a plain <c>switch</c> match on it.
/// </para>
/// <para>
/// Because the marker is part of the type's declaration rather than an attribute, a case may
/// reference the root's own type parameters - <c>Option&lt;T&gt; : IUnion&lt;Some&lt;T&gt;, None&gt;</c>.
/// An attribute cannot express that: its arguments are metadata, resolved before the decorated
/// type is bound.
/// </para>
/// <para>
/// Any type can be a case: classes, records, structs, enums, interfaces, primitives, arrays,
/// closed generics and tuples. Value-type cases are stored in typed fields, so nothing is boxed.
/// </para>
/// </remarks>
/// <typeparam name="T1">The first case type.</typeparam>
/// <example>
/// <code>
/// public record Cat(string Name);
/// public record Dog(string Name);
///
/// public abstract partial record Pet : IUnion&lt;Cat, Dog&gt;;
///
/// Pet pet = new Cat("Whiskers");
/// var name = pet switch
/// {
///     Pet.Cat(var cat) =&gt; cat.Name,
///     Pet.Dog(var dog) =&gt; dog.Name
/// };
/// </code>
/// </example>
public interface IUnion<T1>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
public interface IUnion<T1, T2>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
public interface IUnion<T1, T2, T3>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
public interface IUnion<T1, T2, T3, T4>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
/// <typeparam name="T5">The fifth case type.</typeparam>
public interface IUnion<T1, T2, T3, T4, T5>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
/// <typeparam name="T5">The fifth case type.</typeparam>
/// <typeparam name="T6">The sixth case type.</typeparam>
public interface IUnion<T1, T2, T3, T4, T5, T6>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
/// <typeparam name="T5">The fifth case type.</typeparam>
/// <typeparam name="T6">The sixth case type.</typeparam>
/// <typeparam name="T7">The seventh case type.</typeparam>
public interface IUnion<T1, T2, T3, T4, T5, T6, T7>;

/// <inheritdoc cref="IUnion{T1}"/>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
/// <typeparam name="T4">The fourth case type.</typeparam>
/// <typeparam name="T5">The fifth case type.</typeparam>
/// <typeparam name="T6">The sixth case type.</typeparam>
/// <typeparam name="T7">The seventh case type.</typeparam>
/// <typeparam name="T8">The eighth case type.</typeparam>
public interface IUnion<T1, T2, T3, T4, T5, T6, T7, T8>;
