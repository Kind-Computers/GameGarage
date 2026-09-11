// Copyright Kind Computers. Licensed under the MIT License.
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StabilityTest;

internal static class VirtualAllocUtilities
{
    internal static nint Alloc(nuint bytes)
    {
        nint address = VirtualAlloc(0, bytes, 0x3000, 0x04);
        if (address == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), WorkerText.Get("AllocationFailed"));
        return address;
    }

    internal static void Free(nint address)
    {
        if (!VirtualFree(address, 0, 0x8000))
            throw new Win32Exception(Marshal.GetLastWin32Error(), WorkerText.Get("FreeFailed"));
    }

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protection);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(nint address, nuint size, uint freeType);
}

internal sealed class WindowsRamAllocator : IRamAllocator
{
    public MemorySnapshot Snapshot() => RAMStats.GetSnapshot();
    public RamBlock Allocate(int bytes, ulong firstWord) => new(VirtualAllocUtilities.Alloc((nuint)bytes), bytes, firstWord);
    public void Free(RamBlock block) => VirtualAllocUtilities.Free(block.Address);
}
