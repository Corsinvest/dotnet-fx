using Corsinvest.Fx.Unsafe.Asm;

namespace Corsinvest.Fx.Unsafe.Tests;

public class NativeMemoryHelperTests
{
    [Fact]
    public unsafe void AllocateExecutable_AllocatesMemory()
    {
        var size = (nuint)64;
        var ptr = NativeMemoryHelper.AllocateExecutable(size);

        Assert.True(ptr != null);

        // Write some test data
        var span = new Span<byte>(ptr, (int)size);
        span[0] = 0x42;
        Assert.Equal(0x42, span[0]);

        // Cleanup
        NativeMemoryHelper.Free(ptr);
    }

    [Fact]
    public unsafe void AllocateExecutable_WithZeroSize_HandlesCorrectly()
    {
        // Allocating zero bytes might return a valid pointer or null depending on the OS,
        // but it should not throw. The important part is that Free() can handle it.
        var ptr = NativeMemoryHelper.AllocateExecutable(0);

        // Freeing the potentially valid or null pointer should not throw.
        NativeMemoryHelper.Free(ptr);
    }

    [Fact]
    public unsafe void Free_WithNullPointer_DoesNotThrow()
    {
        // Should not throw when freeing null
        NativeMemoryHelper.Free(null);
    }

    [Fact]
    public unsafe void MakeReadExecute_ChangesProtection()
    {
        var size = (nuint)64;
        var ptr = NativeMemoryHelper.AllocateExecutable(size);

        try
        {
            // Write test bytecode (simple ret instruction)
            var span = new Span<byte>(ptr, (int)size);
            span[0] = 0xC3; // ret

            // Make read-execute (W^X compliance)
            NativeMemoryHelper.MakeReadExecute(ptr, size);

            // Memory should still be readable
            Assert.Equal(0xC3, span[0]);
        }
        finally
        {
            NativeMemoryHelper.Free(ptr); 
        }
    }

    [Fact]
    public unsafe void Allocate_Write_MakeExecutable_And_Execute_Works()
    {
        // This test demonstrates the full lifecycle: allocate, write, protect, execute.
        // Bytecode for a function that returns 42.
        // mov eax, 42
        // ret
        byte[] bytecode = [0xB8, 0x2A, 0x00, 0x00, 0x00, 0xC3];
        var size = (nuint)bytecode.Length;

        var ptr = NativeMemoryHelper.AllocateExecutable(size);
        Assert.True(ptr != null);

        try
        {
            // Write the bytecode to the allocated memory
            var span = new Span<byte>(ptr, (int)size);
            bytecode.CopyTo(span);

            // Change memory protection to Read/Execute
            NativeMemoryHelper.MakeReadExecute(ptr, size);

            // Create a function pointer and invoke the code
            var func = (delegate* unmanaged<int>)ptr;
            var result = func();

            // Verify the result
            Assert.Equal(42, result);
        }
        finally
        {
            // Clean up the allocated memory
            NativeMemoryHelper.Free(ptr);
        }
    }
}
