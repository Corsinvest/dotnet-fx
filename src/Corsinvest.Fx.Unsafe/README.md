# Corsinvest.Fx.Unsafe

**Inline Assembly for C# via Source Generators - Execute raw machine code safely**

[![NuGet](https://img.shields.io/nuget/v/Corsinvest.Fx.Unsafe)](https://www.nuget.org/packages/Corsinvest.Fx.Unsafe/)
[![Downloads](https://img.shields.io/nuget/dt/Corsinvest.Fx.Unsafe)](https://www.nuget.org/packages/Corsinvest.Fx.Unsafe/)

⚠️ **WARNING**: This package executes raw machine code. Use only when necessary for performance-critical sections or hardware-level operations. Improper use can cause crashes and security vulnerabilities.

---

## ⚠️ EXPERIMENTAL PACKAGE

**Status**: Alpha release (v0.1.0-alpha)

This package is **experimental** and:
- ❌ **NOT production-ready** - API may change significantly
- ❌ **No stability guarantees** - Breaking changes expected
- ❌ **May be deprecated** - Could be removed in future versions
- ⚠️ **Niche use cases** - Targets <0.1% of C# developers

**Use only if:**
- ✅ You understand the risks and limitations
- ✅ You have a specific, critical requirement
- ✅ You're willing to adapt to API changes

**Feedback welcome**: Help shape the API at [Issues](https://github.com/Corsinvest/dotnet-fx/issues)

---

## Overview

`Corsinvest.Fx.Unsafe` allows you to write inline assembly in C# using the `[InlineAsm]` attribute. A source generator automatically creates the implementation that allocates executable memory, copies your bytecode, and invokes it with proper marshalling.

**Features:**
- ✅ **Inline assembly attribute** - Declare methods with machine code bytes
- ✅ **Source generator** - Automatic implementation of `partial` methods
- ✅ **Cross-platform memory** - Executable memory allocation (Windows/Linux/macOS)
- ✅ **Multi-architecture** - Support for X64, X86, ARM64 with automatic selection
- ✅ **Type-safe** - Proper marshalling between C# and native code
- ✅ **W^X compliance** - Optional memory protection (write XOR execute)

## Installation

```bash
dotnet add package Corsinvest.Fx.Unsafe
```

**Note**: Your project must enable unsafe code:

```xml
<PropertyGroup>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

## Quick Start

### Basic Inline Assembly

```csharp
using Corsinvest.Fx.Unsafe.Asm;

public static partial class MathOps
{
    // X64 bytecode: xor eax, eax; ret (returns 0)
    [InlineAsm([0x31, 0xC0, 0xC3], Architecture.X64)]
    public static partial int ReturnZero();

    // X64 bytecode: mov rax, rcx; add rax, rdx; ret
    [InlineAsm([0x48, 0x89, 0xC8, 0x48, 0x01, 0xD0, 0xC3], Architecture.X64)]
    public static partial long Add(long a, long b);
}

// Usage
var zero = MathOps.ReturnZero();  // 0
var sum = MathOps.Add(5, 3);      // 8
```

## How It Works

1. **You write**: A `partial` method decorated with `[InlineAsm]` and bytecode
2. **Generator generates**: Implementation that:
   - Allocates executable memory cross-platform
   - Copies your bytecode into it
   - Creates a delegate with proper calling convention
   - Marshals parameters and return values
   - Manages memory lifetime
3. **You call**: The method like any normal C# method

## Usage Examples

### Multi-Architecture Support

Provide multiple implementations for different architectures:

```csharp
public static partial class MultiArch
{
    // X64 implementation: mov rax, rcx; imul rax, rdx; ret
    [InlineAsm([0x48, 0x89, 0xC8, 0x48, 0x0F, 0xAF, 0xC2, 0xC3],
               Architecture.X64, Platform.Any)]

    // ARM64 implementation: mul x0, x0, x1; ret
    [InlineAsm([0x00, 0x7C, 0x01, 0x9B, 0xC0, 0x03, 0x5F, 0xD6],
               Architecture.Arm64, Platform.Any)]

    public static partial long Multiply(long a, long b);
}

// Automatically uses the correct implementation for the current architecture
var result = MultiArch.Multiply(6, 7);  // 42
```

### SIMD Operations

Write custom SIMD code directly:

```csharp
public static partial class Simd
{
    // SSE2: Add 4 floats in parallel
    // movups xmm0, [rcx]; movups xmm1, [rdx]; addps xmm0, xmm1; movups [r8], xmm0; ret
    [InlineAsm([0x0F, 0x10, 0x01,           // movups xmm0, [rcx]
                0x0F, 0x10, 0x0A,           // movups xmm1, [rdx]
                0x0F, 0x58, 0xC1,           // addps xmm0, xmm1
                0x41, 0x0F, 0x11, 0x00,     // movups [r8], xmm0
                0xC3],                       // ret
               Architecture.X64, Platform.Any)]
    public static unsafe partial void AddFloat4(float* a, float* b, float* result);
}

// Usage
unsafe
{
    var a = stackalloc float[4] { 1.0f, 2.0f, 3.0f, 4.0f };
    var b = stackalloc float[4] { 5.0f, 6.0f, 7.0f, 8.0f };
    var result = stackalloc float[4];

    Simd.AddFloat4(a, b, result);

    // result = [6.0f, 8.0f, 10.0f, 12.0f]
}
```

### Platform-Specific Code

Target specific operating systems:

```csharp
public static partial class PlatformSpecific
{
    // Windows system call
    [InlineAsm([/* ... */], Architecture.X64, Platform.Windows)]

    // Linux system call (different ABI)
    [InlineAsm([/* ... */], Architecture.X64, Platform.Linux)]

    public static partial long GetProcessId();
}
```

### Custom CPU Instructions

Access CPU-specific instructions:

```csharp
public static partial class CpuOps
{
    // CPUID instruction: mov eax, ecx; cpuid; mov [rdx], eax; mov [r8], ebx; ret
    [InlineAsm([0x89, 0xC8,              // mov eax, ecx
                0x0F, 0xA2,              // cpuid
                0x89, 0x02,              // mov [rdx], eax
                0x41, 0x89, 0x18,        // mov [r8], ebx
                0xC3],                   // ret
               Architecture.X64)]
    public static unsafe partial void Cpuid(int function, int* eax, int* ebx);

    // RDTSC: Read timestamp counter
    [InlineAsm([0x0F, 0x31,              // rdtsc
                0x48, 0xC1, 0xE2, 0x20,  // shl rdx, 32
                0x48, 0x09, 0xD0,        // or rax, rdx
                0xC3],                   // ret
               Architecture.X64)]
    public static partial ulong Rdtsc();
}
```

## Low-Level Memory API

### NativeMemoryHelper

For advanced scenarios, directly use the memory allocation API:

```csharp
using Corsinvest.Fx.Unsafe.Asm;

unsafe
{
    // Allocate executable memory
    byte[] bytecode = [0x31, 0xC0, 0xC3];  // xor eax, eax; ret
    void* memory = NativeMemoryHelper.AllocateExecutable((nuint)bytecode.Length);

    try
    {
        // Copy bytecode
        fixed (byte* src = bytecode)
        {
            Buffer.MemoryCopy(src, memory, bytecode.Length, bytecode.Length);
        }

        // Optional: Make memory read-execute only (W^X compliance)
        NativeMemoryHelper.MakeReadExecute(memory, (nuint)bytecode.Length);

        // Create and invoke delegate
        var func = Marshal.GetDelegateForFunctionPointer<Func<int>>(
            new IntPtr(memory)
        );
        int result = func();  // 0
    }
    finally
    {
        // Always free
        NativeMemoryHelper.Free(memory);
    }
}
```

### Cross-Platform Memory Allocation

The `NativeMemoryHelper` handles platform differences automatically:

**Windows:**
- Uses `VirtualAlloc` with `PAGE_EXECUTE_READWRITE`
- Uses `VirtualProtect` for W^X

**Linux/macOS:**
- Uses `mmap` with `PROT_READ | PROT_WRITE | PROT_EXEC`
- Uses `mprotect` for W^X
- Handles `MAP_ANONYMOUS` (Linux) vs `MAP_ANON` (macOS)
- Tracks allocation sizes for `munmap`

## API Reference

### Attributes

```csharp
[InlineAsm(byte[] bytecode,
           Architecture architecture = Architecture.X64,
           Platform platform = Platform.Any)]
```

**Parameters:**
- `bytecode` - Machine code bytes to execute
- `architecture` - Target CPU architecture (X86, X64, Arm, Arm64)
- `platform` - Target OS platform (Any, Windows, Linux, OSX)

**Usage rules:**
- Method must be `partial`
- Method must be `static`
- Can specify multiple `[InlineAsm]` attributes for different architectures
- Generator selects the correct implementation at runtime

### Enums

```csharp
public enum Architecture
{
    X86,    // 32-bit Intel/AMD
    X64,    // 64-bit Intel/AMD
    Arm,    // 32-bit ARM
    Arm64   // 64-bit ARM (Apple Silicon, etc.)
}

public enum Platform
{
    Any,      // Works on all platforms
    Windows,  // Windows-specific
    Linux,    // Linux-specific
    OSX       // macOS-specific
}
```

### NativeMemoryHelper

```csharp
// Allocate executable memory
void* AllocateExecutable(nuint size);

// Free allocated memory
void Free(void* ptr);

// Make memory read-execute only (W^X compliance)
void MakeReadExecute(void* ptr, nuint size);
```

## Calling Conventions

The generator uses platform-specific calling conventions:

**Windows X64** - Microsoft x64 calling convention:
- Args: `RCX`, `RDX`, `R8`, `R9`, then stack
- Return: `RAX` (integers), `XMM0` (floats)

**Linux/macOS X64** - System V AMD64 ABI:
- Args: `RDI`, `RSI`, `RDX`, `RCX`, `R8`, `R9`, then stack
- Return: `RAX` (integers), `XMM0` (floats)

**ARM64** - AAPCS64:
- Args: `X0-X7`, `V0-V7` (FP/SIMD)
- Return: `X0` (integers), `V0` (floats)

## Best Practices

### 1. Testing

```csharp
// ✅ Good - Test on all target platforms
[Fact]
public void InlineAsm_Works_OnAllPlatforms()
{
    var result = MathOps.Add(5, 3);
    Assert.Equal(8, result);
}

// ✅ Good - Provide fallback C# implementation
public static long AddFallback(long a, long b) => a + b;
```

### 2. Error Handling

```csharp
// ✅ Good - Validate inputs before calling assembly
public static long SafeAdd(long a, long b)
{
    if (a > 0 && b > long.MaxValue - a)
        throw new OverflowException();

    return Add(a, b);
}
```

### 3. Documentation

```csharp
// ✅ Good - Document assembly code
/// <summary>
/// Adds two 64-bit integers using inline assembly.
/// </summary>
/// <remarks>
/// X64 Assembly:
///   mov rax, rcx    ; Load first arg into rax
///   add rax, rdx    ; Add second arg
///   ret             ; Return (result in rax)
/// </remarks>
[InlineAsm([0x48, 0x89, 0xC8, 0x48, 0x01, 0xD0, 0xC3], Architecture.X64)]
public static partial long Add(long a, long b);
```

### 4. W^X Compliance

```csharp
// ✅ Good - Use MakeReadExecute after writing bytecode
void* memory = NativeMemoryHelper.AllocateExecutable(size);
// ... copy bytecode ...
NativeMemoryHelper.MakeReadExecute(memory, size);  // Now read-only + executable
```

## Performance Considerations

**When to use inline assembly:**
- ✅ Custom SIMD operations not available in .NET intrinsics
- ✅ Hardware-specific instructions (CPUID, RDTSC, etc.)
- ✅ Micro-optimizations in hot paths (after profiling!)
- ✅ Interop with specific binary formats

**When NOT to use:**
- ❌ General arithmetic - C# JIT is very good
- ❌ Before profiling - "premature optimization is the root of all evil"
- ❌ Portable code - limits deployment platforms
- ❌ Maintainability is important - assembly is hard to read/maintain

## Safety Considerations

⚠️ **This package is UNSAFE by design:**

- ❌ **Crashes** - Invalid bytecode crashes the process
- ❌ **Security** - Arbitrary code execution if bytecode is from untrusted source
- ❌ **Platform-specific** - Code may not work on all architectures
- ❌ **Undefined behavior** - Wrong calling convention = corruption
- ❌ **No validation** - Generator doesn't verify bytecode validity

**Use only when:**
- ✅ You understand assembly language
- ✅ You've profiled and identified the bottleneck
- ✅ You have comprehensive tests on all target platforms
- ✅ Performance is absolutely critical

## Platform Support

| Platform | Architecture | Status |
|----------|--------------|--------|
| Windows | X64 | ✅ Tested |
| Windows | X86 | ✅ Supported |
| Linux | X64 | ✅ Tested |
| Linux | ARM64 | ✅ Supported |
| macOS | X64 | ✅ Tested |
| macOS | ARM64 (Apple Silicon) | ✅ Supported |

## Limitations

- ❌ No inline assembly syntax - must provide raw bytecode
- ❌ No disassembler - you must write/verify bytes manually
- ❌ Limited to function calls - can't inline into C# methods
- ❌ No access to C# locals - must pass everything as parameters
- ⚠️ Memory leaks if not freed properly
- ⚠️ W^X policy on some platforms (iOS, some Linux distros)

## Related Packages

- [System.Runtime.Intrinsics](https://docs.microsoft.com/dotnet/api/system.runtime.intrinsics) - .NET SIMD intrinsics (prefer this when possible)
- [System.Runtime.CompilerServices.Unsafe](https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/) - Unsafe helpers

## Tools for Writing Assembly

**Assemblers:**
- [nasm](https://www.nasm.us/) - Netwide Assembler
- [yasm](http://yasm.tortall.net/) - Modular assembler
- Online: [defuse.ca/online-x86-assembler](https://defuse.ca/online-x86-assembler.htm)
- Online: [shell-storm.org/online/Online-Assembler-and-Disassembler](https://shell-storm.org/online/Online-Assembler-and-Disassembler/) - Multi-architecture assembler/disassembler

**Disassemblers:**
- [objdump](https://sourceware.org/binutils/docs/binutils/objdump.html) - Part of binutils
- Online: [onlinedisassembler.com](https://onlinedisassembler.com/)
- Online: [shell-storm.org](https://shell-storm.org/online/Online-Assembler-and-Disassembler/) - Supports X86, X64, ARM, ARM64, MIPS, PowerPC

**Workflow:**
1. Write assembly in assembler
2. Extract bytecode
3. Add to `[InlineAsm]` attribute
4. Test on target platform

## Examples

See [tests/Corsinvest.Fx.Unsafe.Tests](../../tests/Corsinvest.Fx.Unsafe.Tests/) for more examples:
- [InlineAsmTests.cs](../../tests/Corsinvest.Fx.Unsafe.Tests/InlineAsmTests.cs) - Basic inline assembly
- [AttributeTests.cs](../../tests/Corsinvest.Fx.Unsafe.Tests/AttributeTests.cs) - Attribute usage
- [NativeMemoryHelperTests.cs](../../tests/Corsinvest.Fx.Unsafe.Tests/NativeMemoryHelperTests.cs) - Low-level memory

## Inspiration

- **C/C++** - Inline assembly (`__asm`, `asm`)
- **Rust** - `asm!` macro
- **Zig** - Inline assembly syntax
- **Go** - Assembly functions

## License

MIT License - see [LICENSE](../../LICENSE) for details

## Support

📖 [Documentation](https://github.com/Corsinvest/dotnet-fx)
🐛 [Issues](https://github.com/Corsinvest/dotnet-fx/issues)
💬 [Discussions](https://github.com/Corsinvest/dotnet-fx/discussions)
