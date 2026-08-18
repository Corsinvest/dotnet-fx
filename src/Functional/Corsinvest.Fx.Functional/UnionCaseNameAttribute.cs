/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

namespace Corsinvest.Fx.Functional;

/// <summary>
/// Overrides the generated wrapper name for one case type of a union.
/// </summary>
/// <typeparam name="T">The case type whose wrapper is being renamed.</typeparam>
/// <example>
/// <code>
/// [UnionCaseName&lt;Farm.Cat&gt;("Domestic")]
/// [UnionCaseName&lt;Wild.Cat&gt;("Feral")]
/// public abstract partial record Feline : IUnion&lt;Farm.Cat, Wild.Cat&gt;;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class UnionCaseNameAttribute<T>(string name) : Attribute
{
    /// <summary>The wrapper name to use for <typeparamref name="T"/>.</summary>
    public string Name { get; } = name;
}
