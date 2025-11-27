using Corsinvest.Fx.Unsafe.Asm;
using System.Runtime.InteropServices;

namespace Corsinvest.Fx.Unsafe.Tests;

public static partial class MathOps
{
    // xor eax, eax; ret
    [InlineAsm([0x31, 0xC0, 0xC3], Architecture.X64)]
    public static unsafe partial int ReturnZero();

    // mov rax, rcx; add rax, rdx; ret
    [InlineAsm([0x48, 0x89, 0xC8, 0x48, 0x01, 0xD0, 0xC3], Architecture.X64, Platform.Any)]
    public static unsafe partial long Add(long a, long b);
}

public static partial class MultiArch
{
    // x64: mov rax, rcx; imul rax, rdx; ret
    [InlineAsm([0x48, 0x89, 0xC8, 0x48, 0x0F, 0xAF, 0xC2, 0xC3],
               Architecture.X64, Platform.Any)]

    // ARM64: mul x0, x0, x1; ret
    [InlineAsm([0x00, 0x7C, 0x01, 0x9B, 0xC0, 0x03, 0x5F, 0xD6],
               Architecture.Arm64, Platform.Any)]

    public static unsafe partial long Multiply(long a, long b);
}

public static partial class Simd
{
    // SSE2: movups xmm0, [rcx]; movups xmm1, [rdx]; addps xmm0, xmm1; movups [r8], xmm0; ret
    [InlineAsm([0x0F, 0x10, 0x01, 0x0F, 0x10, 0x0A, 0x0F, 0x58, 0xC1,
                0x41, 0x0F, 0x11, 0x00, 0xC3],
               Architecture.X64, Platform.Any)]
    public static unsafe partial void AddFloat4(float* a, float* b, float* result);
}

public class InlineAsmTests
{
    [Fact]
    public void InlineAsm_ReturnZero_Works()
    {
        var result = MathOps.ReturnZero();
        Assert.Equal(0, result);
    }

    [Fact]
    public void InlineAsm_Add_Works()
    {
        var result = MathOps.Add(5, 3);
        Assert.Equal(8, result);
    }

    [Fact]
    public void InlineAsm_MultiArch_Multiply_Works()
    {
        var result = MultiArch.Multiply(6, 7);
        Assert.Equal(42, result);
    }

    [Fact]
    public unsafe void InlineAsm_Simd_AddFloat4_Works()
    {
        var a = stackalloc float[4] { 1.0f, 2.0f, 3.0f, 4.0f };
        var b = stackalloc float[4] { 5.0f, 6.0f, 7.0f, 8.0f };
        var result = stackalloc float[4];

        Simd.AddFloat4(a, b, result);

        Assert.Equal(6.0f, result[0]);
        Assert.Equal(8.0f, result[1]);
        Assert.Equal(10.0f, result[2]);
        Assert.Equal(12.0f, result[3]);
    }
}
