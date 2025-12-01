using Corsinvest.Fx.Unsafe.Asm;
using System.Runtime.InteropServices;

namespace Corsinvest.Fx.Unsafe.Tests;

public class AttributeTests
{
    [Fact]
    public void InlineAsmAttribute_Constructor_WithValidBytecode_Works()
    {
        var bytecode = new byte[] { 0x48, 0x89, 0xC8, 0xC3 };
        var attr = new InlineAsmAttribute(bytecode, Architecture.X64, Platform.Any);

        Assert.Equal(bytecode, attr.Bytecode);
        Assert.Equal(Architecture.X64, attr.Architecture);
        Assert.Equal(Platform.Any, attr.Platform);
    }

    [Fact]
    public void InlineAsmAttribute_Constructor_DefaultArchitecture_IsX64()
    {
        var bytecode = new byte[] { 0xC3 };
        var attr = new InlineAsmAttribute(bytecode);

        Assert.Equal(Architecture.X64, attr.Architecture);
        Assert.Equal(Platform.Any, attr.Platform);
    }

    [Fact]
    public void InlineAsmAttribute_Constructor_WithNullBytecode_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InlineAsmAttribute(null!, Architecture.X64, Platform.Any));
    }

    [Fact]
    public void InlineAsmAttribute_AllArchitectures_Work()
    {
        var bytecode = new byte[] { 0xC3 };

        var attrX64 = new InlineAsmAttribute(bytecode, Architecture.X64);
        var attrX86 = new InlineAsmAttribute(bytecode, Architecture.X86);
        var attrArm64 = new InlineAsmAttribute(bytecode, Architecture.Arm64);
        var attrArm = new InlineAsmAttribute(bytecode, Architecture.Arm);

        Assert.Equal(Architecture.X64, attrX64.Architecture);
        Assert.Equal(Architecture.X86, attrX86.Architecture);
        Assert.Equal(Architecture.Arm64, attrArm64.Architecture);
        Assert.Equal(Architecture.Arm, attrArm.Architecture);
    }

    [Fact]
    public void InlineAsmAttribute_AllPlatforms_Work()
    {
        var bytecode = new byte[] { 0xC3 };

        var attrAny = new InlineAsmAttribute(bytecode, Architecture.X64, Platform.Any);
        var attrWin = new InlineAsmAttribute(bytecode, Architecture.X64, Platform.Windows);
        var attrLinux = new InlineAsmAttribute(bytecode, Architecture.X64, Platform.Linux);
        var attrOSX = new InlineAsmAttribute(bytecode, Architecture.X64, Platform.OSX);

        Assert.Equal(Platform.Any, attrAny.Platform);
        Assert.Equal(Platform.Windows, attrWin.Platform);
        Assert.Equal(Platform.Linux, attrLinux.Platform);
        Assert.Equal(Platform.OSX, attrOSX.Platform);
    }

    [Fact]
    public void InlineAsmAttribute_EmptyBytecode_Works()
    {
        var bytecode = Array.Empty<byte>();
        var attr = new InlineAsmAttribute(bytecode);

        Assert.Empty(attr.Bytecode);
    }
}
