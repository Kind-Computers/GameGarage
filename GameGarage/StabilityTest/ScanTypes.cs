// Copyright Kind Computers. Licensed under the MIT License.
namespace StabilityTest;

internal enum ScanStatus { Passed = 0, Issues = 1, Error = 2, Cancelled = 3, Inconclusive = 4 }
internal enum KernelKind { Scalar, Unrolled, Sse41, Avx2 }
internal enum ScanMode { Initialize, Update, Verify }

internal sealed record ScanOptions(int Passes, ulong ReserveBytes, int Threads, int Seed)
{
    // Internal bounds allow tests/benchmarks to use the production scan on owned buffers.
    internal ulong? MaximumBytes { get; init; }
    internal int BlockBytes { get; init; } = 1024 * 1024 * 1024;
}

internal readonly record struct MemorySnapshot(ulong TotalPhysicalBytes, ulong AvailablePhysicalBytes, ulong AvailableCommitBytes);
internal readonly record struct RamBlock(nint Address, int Bytes, ulong FirstWord);
internal readonly record struct Mismatch(ulong ByteOffset, ulong Expected, ulong Actual, KernelKind Kernel);
internal sealed record ScanResult(ScanStatus Status, ulong AllocatedBytes, ulong TestedBytes, int CompletedPasses,
    TimeSpan Elapsed, int Seed, ulong[] KernelBytes, Mismatch? Mismatch = null, string? Detail = null)
{
    internal string AlgorithmVersion => ScanAlgorithm.Version;
}

internal interface IRamAllocator
{
    MemorySnapshot Snapshot();
    RamBlock Allocate(int bytes, ulong firstWord);
    void Free(RamBlock block);
}
