/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using System.Reflection;

namespace Corsinvest.Fx.Functional.Tests;

/// <summary>
/// Covers the union marker interfaces and the case-name override attribute.
/// </summary>
public class UnionMarkerTests
{
    [Fact]
    public void IUnion_ExistsForArities_One_To_Eight()
    {
        var assembly = typeof(UnionCaseNameAttribute<>).Assembly;

        for (var arity = 1; arity <= 8; arity++)
        {
            var name = $"Corsinvest.Fx.Functional.IUnion`{arity}";
            Assert.NotNull(assembly.GetType(name));
        }
    }

    [Fact]
    public void IUnion_IsAnEmptyMarker()
    {
        // The interface carries case types, not behaviour: a member would force every
        // union root to implement it.
        Assert.Empty(typeof(IUnion<,>).GetMembers());
    }

    [Fact]
    public void IUnion_DoesNotClashWithTheBclUnionInterface()
    {
        // C# 15 ships System.Runtime.CompilerServices.IUnion with arity 0; ours is generic,
        // so the metadata names differ and both can be referenced together.
        Assert.Equal("Corsinvest.Fx.Functional", typeof(IUnion<>).Namespace);
        Assert.True(typeof(IUnion<>).IsGenericTypeDefinition);
    }

    [Fact]
    public void UnionCaseNameAttribute_CarriesTheOverrideName()
    {
        var attribute = new UnionCaseNameAttribute<string>("Text");

        Assert.Equal("Text", attribute.Name);
    }

    [Fact]
    public void GenericUnionAttribute_IsGone()
    {
        // The generic attribute forms (arity 1-8) were removed in favour of the interface.
        // The non-generic [Union] attribute is retired separately in Task 3.
        var assembly = typeof(UnionCaseNameAttribute<>).Assembly;

        Assert.Null(assembly.GetType("Corsinvest.Fx.Functional.UnionAttribute`2"));
    }
}
