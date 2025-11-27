using System.Runtime.InteropServices;

namespace Corsinvest.Fx.Unsafe.Asm;

public static unsafe class NativeMemoryHelper
{
    /// <summary>
    /// Allocates executable memory cross-platform.
    /// </summary>
    /// <param name="size">Size in bytes to allocate</param>
    /// <returns>Pointer to executable memory</returns>
    public static void* AllocateExecutable(nuint size)
    {
        if (size == 0)
        {
            return null;
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? AllocateWindows(size)
            : AllocateUnix(size);
    }

    /// <summary>
    /// Frees executable memory allocated by AllocateExecutable.
    /// </summary>
    /// <param name="ptr">Pointer to free</param>
    public static void Free(void* ptr)
    {
        if (ptr == null) { return; }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FreeWindows(ptr);
        }
        else
        {
            FreeUnix(ptr);
        }
    }

    #region Windows Implementation

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr lpAddress,
                                              UIntPtr dwSize,
                                              AllocationType flAllocationType,
                                              MemoryProtection flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, FreeType dwFreeType);

    [Flags]
    private enum AllocationType : uint
    {
        Commit = 0x1000,
        Reserve = 0x2000
    }

    private enum MemoryProtection : uint
    {
        ExecuteReadWrite = 0x40
    }

    [Flags]
    private enum FreeType : uint
    {
        Decommit = 0x4000,
        Release = 0x8000
    }

    private static void* AllocateWindows(nuint size)
    {
        var ptr = VirtualAlloc(IntPtr.Zero,
                               (UIntPtr)size,
                               AllocationType.Commit | AllocationType.Reserve,
                               MemoryProtection.ExecuteReadWrite);

        if (ptr == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"VirtualAlloc failed with error code: {error}");
        }

        return ptr.ToPointer();
    }

    private static void FreeWindows(void* ptr)
    {
        if (!VirtualFree(new IntPtr(ptr), UIntPtr.Zero, FreeType.Release))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"VirtualFree failed with error code: {error}");
        }
    }

    #endregion

    #region Unix Implementation (Linux/macOS)

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr mmap(IntPtr addr,
                                      UIntPtr length,
                                      int prot,
                                      int flags,
                                      int fd,
                                      IntPtr offset);

    [DllImport("libc", SetLastError = true)]
    private static extern int munmap(IntPtr addr, UIntPtr length);

    private const int PROT_READ = 0x1;
    private const int PROT_WRITE = 0x2;
    private const int PROT_EXEC = 0x4;
    private const int MAP_PRIVATE = 0x02;
    private const int MAP_ANONYMOUS = 0x20;
    private const int MAP_ANON = 0x1000; // macOS uses this instead sometimes

    // Track allocated sizes for munmap (Unix requires size parameter)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, nuint> _allocations = new();

    private static void* AllocateUnix(nuint size)
    {
        // Try MAP_ANONYMOUS first (Linux standard)
        var ptr = mmap(IntPtr.Zero,
                       (UIntPtr)size,
                       PROT_READ | PROT_WRITE | PROT_EXEC,
                       MAP_PRIVATE | MAP_ANONYMOUS,
                       -1,
                       IntPtr.Zero);

        // If MAP_ANONYMOUS failed, try MAP_ANON (macOS)
        if (ptr == new IntPtr(-1))
        {
            ptr = mmap(IntPtr.Zero,
                       (UIntPtr)size,
                       PROT_READ | PROT_WRITE | PROT_EXEC,
                       MAP_PRIVATE | MAP_ANON,
                       -1,
                       IntPtr.Zero);
        }

        if (ptr == new IntPtr(-1))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"mmap failed with error code: {error}");
        }

        // Store allocation size for later deallocation
        _allocations[ptr] = size;

        return ptr.ToPointer();
    }

    private static void FreeUnix(void* ptr)
    {
        var address = new IntPtr(ptr);

        if (!_allocations.TryRemove(address, out var size))
        {
            // If size not tracked, we can't safely free
            throw new InvalidOperationException("Cannot free memory: allocation size not tracked");
        }

        if (munmap(address, (UIntPtr)size) != 0)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"munmap failed with error code: {error}");
        }
    }

    #endregion

    #region Optional: W^X (Write XOR Execute) Support

    /// <summary>
    /// Changes memory protection to read-only + execute (for W^X compliance).
    /// Call this after writing bytecode to make memory non-writable.
    /// </summary>
    public static void MakeReadExecute(void* ptr, nuint size)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MakeReadExecuteWindows(ptr, size);
        }
        else
        {
            MakeReadExecuteUnix(ptr, size);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr lpAddress,
                                              UIntPtr dwSize,
                                              MemoryProtection flNewProtect,
                                              out uint lpflOldProtect);

    private enum MemoryProtectionRX : uint
    {
        ExecuteRead = 0x20
    }

    private static void MakeReadExecuteWindows(void* ptr, nuint size)
    {
        if (!VirtualProtect(new IntPtr(ptr),
                            (UIntPtr)size,
                            (MemoryProtection)0x20, // EXECUTE_READ
                            out _))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"VirtualProtect failed with error code: {error}");
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int mprotect(IntPtr addr, UIntPtr len, int prot);

    private static void MakeReadExecuteUnix(void* ptr, nuint size)
    {
        if (mprotect(new IntPtr(ptr), (UIntPtr)size, PROT_READ | PROT_EXEC) != 0)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"mprotect failed with error code: {error}");
        }
    }

    #endregion
}
