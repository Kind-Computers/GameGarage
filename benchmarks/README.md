# RAM scan performance comparison

Run from the repository root:

```powershell
dotnet run --project benchmarks/GameGarage.RamBenchmarks/GameGarage.RamBenchmarks.csproj -c Release -- --mib 2048 --threads 2 --runs 5 --output artifacts/ram-benchmark.json
```

This opt-in benchmark allocates only its requested buffers. It accepts 1–4 GiB in 1 GiB steps, 1–4 workers and 3–15 paired runs, and requires at least 4 GiB additional available physical memory before starting. It never invokes the worker's broad default scan. Close other memory-heavy workloads before measuring.

Both algorithms run in the same managed .NET process with the same byte count, thread count, ten update passes and a fixed seed. The benchmark warms both, alternates execution order and compares their median **end-to-end** times. Timing includes native allocation, page commitment, worker creation, scanning, joining and freeing. The candidate calls the production `RAMTest.Run` with an internal allocation ceiling; it includes its additional final readback and production SIMD stripes.

The legacy adapter retains the original 2024 scalar, unrolled scalar, SSE4.1 and AVX2 hot loops. Its caller uses a fixed-seed version of the original three random path-selection flags, with the same resulting distribution. It applies the same deterministic uniform seed/complement sequence as the candidate, without the candidate's address-dependent values. For controlled memory ownership it replaces the original changing allocation pool with a fixed pool, and both sides use the same corrected worker pool. It therefore compares the scan algorithm, not the original busy-wait worker implementation or memory-growth heuristics.

The recorded algorithm identifier is `xor-address-v1`: stable logical word index XOR a SplitMix64-derived seeded value, alternating with its complement; ten default updates followed by final readback. Logs and new benchmark reports carry this identifier. The existing measured JSON files have been annotated with the same identifier; their sample values are unchanged.

The production worker admits its memory pool once and does not grow it during testing. It checks the requested physical-memory reserve before each scan stage. A later shortage stops the run as **Inconclusive**, then releases the complete pool; it does not silently remove unverified blocks and continue toward a passing result. Cancellation is checked within bounded chunks. These tests do not promise a permanently reserved amount of system memory between resource checks.

The full original source is preserved at [legacy/RAMTest.2024.cs](legacy/RAMTest.2024.cs), SHA-256 `5d5f75b21cc1c0f65c7fa93727c4f8ff5b2be7942f22f63e9201e1928787ca8c`. The adapter changes only visibility and explicit kernel selection within the extracted hot-loop method.

[The measured preview baseline](initial.json) used .NET 10.0.12 x64, AVX2, 2 GiB, two workers, ten updates, one warm-up per algorithm and five paired samples. Median legacy time was 0.8522162 seconds; candidate time was 0.8878836 seconds, a ratio of 1.04185 (4.19% slower). It passed the 1.10 maximum ratio. This is evidence for that machine and workload, not a universal speed guarantee. Allocation/cache state, CPU, memory bandwidth, scheduling and concurrent applications affect results.

The final implementation was also measured on three bounded cases on the same .NET 10.0.12 x64/AVX2 machine. Each case used ten updates, one warm-up per algorithm and five alternating paired samples:

| Owned memory | Workers | Legacy median | Candidate median | Overhead | Gate |
|---|---:|---:|---:|---:|---|
| [2 GiB](windows-x64-2gib-1thread.json) | 1 | 0.9477224 s | 0.9566734 s | 0.94% | Pass |
| [2 GiB](windows-x64-2gib-2threads.json) | 2 | 0.8589020 s | 0.8708709 s | 1.39% | Pass |
| [4 GiB](windows-x64-4gib-4threads.json) | 4 | 1.6686500 s | 1.7450316 s | 4.58% | Pass |

These samples establish the gate only for these measured workloads; they do not establish performance on every Windows PC, every memory size or a non-AVX2 machine. The earlier measurement remains recorded in `initial.json` for transparency.

A ratio over 1.10 makes the benchmark exit nonzero. Keep performance runs out of normal correctness CI. Changes to the successful hot path require a new measurement; failed comparisons must not be omitted when recording an optimization investigation.
