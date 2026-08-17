namespace Corsinvest.Fx.Functional;

/// <summary>
/// Marks a partial record as a discriminated union whose cases are the supplied type arguments.
/// </summary>
/// <remarks>
/// <para>
/// Unlike a nested-case declaration, the case types are ordinary standalone types, so the same
/// type can take part in several unions. The generator emits one sealed nested wrapper per case
/// that derives from the union root, which is what keeps the hierarchy closed and lets
/// <c>switch</c> work directly on the union.
/// </para>
/// <para>
/// Any type can be a case: classes, sealed classes, records, structs, enums, primitives, arrays,
/// closed generics and unnamed tuples. Value-type cases are stored in typed fields, so nothing is
/// boxed. Interfaces cannot be case types: the generator always emits an implicit conversion
/// operator, and C# forbids user-defined conversions to or from an interface. A tuple with element
/// names cannot appear as an attribute type argument at all; use an unnamed tuple instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public record Cat(string Name);
/// public record Dog(string Name);
///
/// [Union&lt;Cat, Dog&gt;]
/// public abstract partial record Pet;
///
/// Pet pet = new Cat("Whiskers");
/// var name = pet switch
/// {
///     Pet.Cat(var cat) =&gt; cat.Name,
///     Pet.Dog(var dog) =&gt; dog.Name
/// };
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4, T5> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4, T5, T6> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4, T5, T6, T7> : Attribute;

/// <inheritdoc cref="UnionAttribute{T1}"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class UnionAttribute<T1, T2, T3, T4, T5, T6, T7, T8> : Attribute;

/// <summary>
/// Overrides the generated wrapper name for one case type of a union.
/// </summary>
/// <typeparam name="T">The case type whose wrapper is being renamed.</typeparam>
/// <example>
/// <code>
/// [Union&lt;Farm.Cat, Wild.Cat&gt;]
/// [UnionCaseName&lt;Farm.Cat&gt;("Domestic")]
/// [UnionCaseName&lt;Wild.Cat&gt;("Feral")]
/// public abstract partial record Feline;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class UnionCaseNameAttribute<T>(string name) : Attribute
{
    /// <summary>The wrapper name to use for <typeparamref name="T"/>.</summary>
    public string Name { get; } = name;
}
