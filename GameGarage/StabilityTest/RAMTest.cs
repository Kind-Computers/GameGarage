// Copyright Kind Computers. Licensed under the MIT License.
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StabilityTest;

internal static class RAMTest
{
    internal const int ChunkBytes = 1024 * 1024;
    internal const int StripeBytes = 64 * 1024;

    internal static ScanResult Run(ScanOptions options, IRamAllocator allocator, CancellationToken cancellation,
        Action<double>? progress = null, Action<string>? log = null)
    {
        var timer = Stopwatch.StartNew();
        var blocks = new List<RamBlock>();
        ScanResult? result = null;
        ulong allocated = 0;
        string? cleanupError = null;
        try
        {
            Validate(options);
            cancellation.ThrowIfCancellationRequested();
            ulong nextWord = 0;
            // Allocation happens once. The pool never grows after testing begins.
            while (!options.MaximumBytes.HasValue || allocated < options.MaximumBytes.Value)
            {
                cancellation.ThrowIfCancellationRequested();
                MemorySnapshot memory = allocator.Snapshot();
                ulong remaining = options.MaximumBytes.HasValue ? options.MaximumBytes.Value - allocated : ulong.MaxValue;
                int bytes = (int)Math.Min((ulong)options.BlockBytes, remaining);
                bytes -= bytes % sizeof(ulong);
                if (bytes == 0 || memory.AvailablePhysicalBytes < options.ReserveBytes ||
                    memory.AvailablePhysicalBytes - options.ReserveBytes < (ulong)bytes ||
                    memory.AvailableCommitBytes < (ulong)bytes + 64UL * 1024 * 1024)
                    break;

                RamBlock block = allocator.Allocate(bytes, nextWord);
                // Own the allocation before touching it so every failure path can release it.
                blocks.Add(block);
                allocated += (ulong)bytes;
                nextWord += (ulong)bytes / sizeof(ulong);
                for (int offset = 0; offset < bytes; offset += Environment.SystemPageSize)
                {
                    if (offset % ChunkBytes == 0) cancellation.ThrowIfCancellationRequested();
                    Marshal.WriteByte(block.Address, offset, 0);
                }
                log?.Invoke(WorkerText.Format("AllocatedBytes", allocated));
            }

            if (blocks.Count == 0)
                result = new(ScanStatus.Inconclusive, 0, 0, 0, timer.Elapsed, options.Seed, new ulong[4],
                    Detail: WorkerText.Get("InsufficientMemory"));
            else
                result = RunBlocks(options, blocks, cancellation, progress,
                    () => allocator.Snapshot().AvailablePhysicalBytes >= options.ReserveBytes);
        }
        catch (OperationCanceledException)
        {
            result = new(ScanStatus.Cancelled, allocated, 0, 0, timer.Elapsed, options.Seed, new ulong[4]);
        }
        catch (Exception ex)
        {
            result = new(ScanStatus.Error, allocated, 0, 0, timer.Elapsed, options.Seed, new ulong[4], Detail: ex.Message);
        }
        finally
        {
            foreach (RamBlock block in blocks)
            {
                try { allocator.Free(block); }
                catch (Exception ex) { cleanupError ??= ex.Message; }
            }
        }
        timer.Stop();
        result ??= new(ScanStatus.Error, allocated, 0, 0, timer.Elapsed, options.Seed, new ulong[4], Detail: WorkerText.Get("MissingResult"));
        return result with
        {
            Elapsed = timer.Elapsed,
            Status = cleanupError == null ? result.Status : ScanStatus.Error,
            Detail = cleanupError == null ? result.Detail : WorkerText.Format("CleanupFailed", cleanupError)
        };
    }

    // The same production engine is called by tiny-buffer tests and bounded benchmarks.
    internal static ScanResult RunBlocks(ScanOptions options, IReadOnlyList<RamBlock> blocks, CancellationToken cancellation,
        Action<double>? progress = null, Func<bool>? memoryAvailable = null,
        Action<int, IReadOnlyList<RamBlock>>? afterUpdate = null)
    {
        var timer = Stopwatch.StartNew();
        ulong allocated = 0, tested = 0;
        int completed = 0;
        var coverage = new ulong[4];
        foreach (RamBlock block in blocks) allocated += (ulong)block.Bytes;
        try
        {
            Validate(options);
            if (blocks.Count == 0)
                return Result(ScanStatus.Inconclusive, WorkerText.Get("NoMemoryTested"));
            foreach (RamBlock block in blocks)
                if (block.Address == 0 || block.Bytes <= 0 || block.Bytes % sizeof(ulong) != 0)
                    throw new ArgumentException(WorkerText.Get("InvalidBlock"));

            using var pool = new UtilityThreadPool<BlockResult>(Math.Min(options.Threads, blocks.Count), ThreadPriority.Lowest);
            ulong generator = unchecked((uint)options.Seed);
            ulong pattern = 0, nextPattern = 0;
            int stages = checked(options.Passes + 1);
            for (int stage = 0; stage < stages; stage++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (memoryAvailable != null && !memoryAvailable())
                    return Result(ScanStatus.Inconclusive, WorkerText.Get("MemoryPressure"));

                ScanMode mode = stage == 0 ? ScanMode.Initialize : stage == options.Passes ? ScanMode.Verify : ScanMode.Update;
                if (mode != ScanMode.Verify)
                    nextPattern = stage % 2 == 0 ? NextPattern(ref generator) : ~pattern;
                ulong mask = pattern ^ nextPattern;
                var tasks = new Task<BlockResult>[blocks.Count];
                for (int i = 0; i < blocks.Count; i++)
                {
                    RamBlock block = blocks[i];
                    ulong stagePattern = pattern, stageMask = mask, initialPattern = nextPattern;
                    tasks[i] = pool.AddTask(() => ScanBlock(block, mode, stagePattern, stageMask, initialPattern, cancellation));
                }
                // Always join all workers before returning or releasing native memory.
                BlockResult[] results = Task.WhenAll(tasks).GetAwaiter().GetResult();
                Mismatch? failure = null;
                for (int i = 0; i < results.Length; i++)
                {
                    for (int k = 0; k < coverage.Length; k++) coverage[k] += results[i].Coverage[k];
                    failure ??= results[i].Mismatch;
                    if (mode == ScanMode.Verify && results[i].Mismatch == null) tested += (ulong)blocks[i].Bytes;
                }
                if (failure != null)
                    return Result(ScanStatus.Issues, failure: failure);
                if (mode != ScanMode.Verify)
                {
                    completed++;
                    pattern = nextPattern;
                    afterUpdate?.Invoke(completed, blocks);
                }
                if (stage < stages - 1) progress?.Invoke(100.0 * (stage + 1) / stages);
            }
            cancellation.ThrowIfCancellationRequested();
            progress?.Invoke(100);
            return Result(ScanStatus.Passed);
        }
        catch (OperationCanceledException) { return Result(ScanStatus.Cancelled); }
        catch (Exception ex) { return Result(ScanStatus.Error, ex.Message); }

        ScanResult Result(ScanStatus status, string? detail = null, Mismatch? failure = null) =>
            new(status, allocated, tested, completed, timer.Elapsed, options.Seed, coverage, failure, detail);
    }

    private sealed record BlockResult(ulong[] Coverage, Mismatch? Mismatch);

    private static BlockResult ScanBlock(RamBlock block, ScanMode mode, ulong pattern, ulong mask,
        ulong initialPattern, CancellationToken cancellation)
    {
        var coverage = new ulong[4];
        int offset = 0;
        KernelKind fastest = RamKernel.Fastest;
        foreach (KernelKind kernel in RamKernel.Supported)
        {
            if (kernel == fastest) continue;
            int size = Math.Min(StripeBytes, block.Bytes - offset);
            if (size == 0) break;
            cancellation.ThrowIfCancellationRequested();
            Mismatch? failure = RamKernel.Scan(block.Address + offset, size, block.FirstWord + (ulong)offset / 8,
                mode, pattern, mask, initialPattern, kernel);
            if (failure != null) return new(coverage, failure);
            coverage[(int)kernel] += (ulong)size;
            offset += size;
        }
        while (offset < block.Bytes)
        {
            cancellation.ThrowIfCancellationRequested();
            int size = Math.Min(ChunkBytes, block.Bytes - offset);
            Mismatch? failure = RamKernel.Scan(block.Address + offset, size, block.FirstWord + (ulong)offset / 8,
                mode, pattern, mask, initialPattern, fastest);
            if (failure != null) return new(coverage, failure);
            coverage[(int)fastest] += (ulong)size;
            offset += size;
        }
        return new(coverage, null);
    }

    internal static ulong NextPattern(ref ulong state)
    {
        // SplitMix64: a documented, fixed sequence independent of runtime Random implementations.
        unchecked
        {
            ulong z = (state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    private static void Validate(ScanOptions options)
    {
        if (options.Passes < 1 || options.Passes == int.MaxValue || options.Threads < 1 ||
            options.BlockBytes < 8 || options.BlockBytes % 8 != 0)
            throw new ArgumentOutOfRangeException(nameof(options), WorkerText.Get("InvalidLimits"));
    }
}
