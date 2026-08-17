using System.Reflection;

namespace Corsinvest.Fx.Functional.Tests;

public class UnionAttributeTests
{
    [Fact]
    public void UnionAttribute_CanBeConstructed()
    {
        var attr = new UnionAttribute();
        Assert.NotNull(attr);
    }

    [Fact]
    public void UnionAttribute_HasCorrectAttributeUsage()
    {
        var attrType = typeof(UnionAttribute);
        var usageAttr = attrType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        Assert.NotNull(usageAttr);
        Assert.True(usageAttr.ValidOn.HasFlag(AttributeTargets.Class));
        Assert.True(usageAttr.ValidOn.HasFlag(AttributeTargets.Struct));
        Assert.False(usageAttr.AllowMultiple);
        Assert.False(usageAttr.Inherited);
    }

    [Fact]
    public void UnionAttribute_InheritsFromAttribute()
    {
        var attr = new UnionAttribute();
        Assert.IsAssignableFrom<Attribute>(attr);
    }

    [Fact]
    public void GenericUnionAttribute_ExistsForArities_One_To_Eight()
    {
        var assembly = typeof(UnionAttribute<>).Assembly;

        for (var arity = 1; arity <= 8; arity++)
        {
            var name = $"Corsinvest.Fx.Functional.UnionAttribute`{arity}";
            Assert.NotNull(assembly.GetType(name));
        }
    }

    [Fact]
    public void GenericUnionAttribute_TargetsClassesOnly_AndIsNotMultiple()
    {
        var usage = typeof(UnionAttribute<,>).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    [Fact]
    public void UnionCaseNameAttribute_CarriesTheOverrideName()
    {
        var attribute = new UnionCaseNameAttribute<string>("Text");

        Assert.Equal("Text", attribute.Name);
    }
}
