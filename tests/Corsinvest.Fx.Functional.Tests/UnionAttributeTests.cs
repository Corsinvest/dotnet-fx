using Corsinvest.Fx.Functional;

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
}
