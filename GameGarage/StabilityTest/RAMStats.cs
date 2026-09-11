// Copyright Kind Computers. Licensed under the MIT License.
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StabilityTest;

internal static class RAMStats
{
    internal static MemorySnapshot GetSnapshot()
    {
        var stats = new MemoryStatus();
        if (!GlobalMemoryStatusEx(stats))
            throw new Win32Exception(Marshal.GetLastWin32Error(), WorkerText.Get("MemoryQueryFailed"));
        // ullAvailPageFile is available commit, not the size of virtual address space.
        return new(stats.TotalPhysical, stats.AvailablePhysical, stats.AvailablePageFile);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus buffer);

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatus
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatus>();
        public uint Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile;
        public ulong TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
}
