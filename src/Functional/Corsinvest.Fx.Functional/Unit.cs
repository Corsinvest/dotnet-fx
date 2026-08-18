/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

namespace Corsinvest.Fx.Functional;

/// <summary>
/// The type with exactly one value, standing in for <see langword="void"/> wherever a type is
/// required.
/// </summary>
/// <remarks>
/// <para>
/// <see langword="void"/> is a keyword, not a type: <c>ResultOf&lt;void, Exception&gt;</c> does not
/// compile. An operation that produces no value can still fail, though - saving a file, deleting a
/// record, checking a precondition - so those need something to put in the success slot.
/// <see cref="Unit"/> is that something: one value, carrying no information, which is precisely
/// what there is to say.
/// </para>
/// <para>
/// A <see cref="bool"/> would seem to do the same job and does not. Success is already
/// <c>IsOk</c>, so a second boolean invites the reader to wonder what else it means, and
/// <c>Ok(false)</c> is a state the type permits while the code has no meaning for it.
/// <see cref="Unit"/> has one value, so there is nothing to misread.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ResultOf&lt;Unit, Exception&gt; saved = TryHelper.Try(() =&gt; File.WriteAllText(path, content));
///
/// static ResultOf&lt;Unit, OrderError&gt; CheckInventory(Product product, int quantity)
///     =&gt; product.Stock &lt; quantity
///         ? ResultOf.Fail&lt;Unit, OrderError&gt;(OrderError.InsufficientStock)
///         : ResultOf.Ok&lt;Unit, OrderError&gt;(Unit.Value);
/// </code>
/// </example>
public readonly struct Unit
{
    /// <summary>The only value of this type.</summary>
    public static readonly Unit Value = new();
}
