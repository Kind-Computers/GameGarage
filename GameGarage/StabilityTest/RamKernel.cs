// Copyright Kind Computers. Licensed under the MIT License.
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace StabilityTest;

internal static class RamKernel
{
    internal static readonly KernelKind[] Supported = Enum.GetValues<KernelKind>().Where(IsSupported).ToArray();
    internal static KernelKind Fastest => Avx2.IsSupported ? KernelKind.Avx2 : Sse41.IsSupported ? KernelKind.Sse41 : KernelKind.Unrolled;

    internal static bool IsSupported(KernelKind kind) => kind switch
    {
        KernelKind.Avx2 => Avx2.IsSupported,
        KernelKind.Sse41 => Sse41.IsSupported,
        _ => true
    };

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static unsafe Mismatch? Scan(nint address, int bytes, ulong firstWord, ScanMode mode,
        ulong pattern, ulong mask, ulong initialPattern, KernelKind kind)
    {
        if (address == 0 || bytes < 0 || bytes % 8 != 0 || !IsSupported(kind))
            throw new ArgumentException(WorkerText.Get("InvalidKernelRange"));
        ulong* pointer = (ulong*)address;
        int words = bytes / 8, i = 0;
        if (kind == KernelKind.Avx2)
        {
            var index = Vector256.Create(firstWord, unchecked(firstWord + 1), unchecked(firstWord + 2), unchecked(firstWord + 3));
            var stride = Vector256.Create(4UL);
            var expectedMask = Vector256.Create(pattern);
            var xorMask = Vector256.Create(mask);
            var seedMask = Vector256.Create(initialPattern);
            for (; i <= words - 4; i += 4)
            {
                var value = Avx.LoadVector256(pointer + i);
                var expected = mode == ScanMode.Initialize ? Vector256<ulong>.Zero : Avx2.Xor(index, expectedMask);
                var equal = Avx2.CompareEqual(value.AsInt64(), expected.AsInt64());
                if (Avx.MoveMask(equal.AsDouble()) != 0xF) return FindFailure(i, value.GetElement(0), value.GetElement(1), value.GetElement(2), value.GetElement(3));
                if (mode != ScanMode.Verify)
                    Avx.Store(pointer + i, Avx2.Xor(value, mode == ScanMode.Initialize ? Avx2.Xor(index, seedMask) : xorMask));
                index = Avx2.Add(index, stride);
            }
        }
        else if (kind == KernelKind.Sse41)
        {
            var index01 = Vector128.Create(firstWord, unchecked(firstWord + 1));
            var index23 = Vector128.Create(unchecked(firstWord + 2), unchecked(firstWord + 3));
            var stride = Vector128.Create(4UL);
            var expectedMask = Vector128.Create(pattern);
            var xorMask = Vector128.Create(mask);
            var seedMask = Vector128.Create(initialPattern);
            for (; i <= words - 4; i += 4)
            {
                var value01 = Sse2.LoadVector128(pointer + i);
                var value23 = Sse2.LoadVector128(pointer + i + 2);
                var expected01 = mode == ScanMode.Initialize ? Vector128<ulong>.Zero : Sse2.Xor(index01, expectedMask);
                var expected23 = mode == ScanMode.Initialize ? Vector128<ulong>.Zero : Sse2.Xor(index23, expectedMask);
                if (Sse2.MoveMask(Sse41.CompareEqual(value01.AsInt64(), expected01.AsInt64()).AsDouble()) != 0x3 ||
                    Sse2.MoveMask(Sse41.CompareEqual(value23.AsInt64(), expected23.AsInt64()).AsDouble()) != 0x3)
                    return FindFailure(i, value01.GetElement(0), value01.GetElement(1), value23.GetElement(0), value23.GetElement(1));
                if (mode != ScanMode.Verify)
                {
                    Sse2.Store(pointer + i, Sse2.Xor(value01, mode == ScanMode.Initialize ? Sse2.Xor(index01, seedMask) : xorMask));
                    Sse2.Store(pointer + i + 2, Sse2.Xor(value23, mode == ScanMode.Initialize ? Sse2.Xor(index23, seedMask) : xorMask));
                }
                index01 = Sse2.Add(index01, stride);
                index23 = Sse2.Add(index23, stride);
            }
        }
        else if (kind == KernelKind.Unrolled)
        {
            for (; i <= words - 4; i += 4)
            {
                ulong a = pointer[i], b = pointer[i + 1], c = pointer[i + 2], d = pointer[i + 3];
                ulong ea = mode == ScanMode.Initialize ? 0 : unchecked(firstWord + (ulong)i) ^ pattern;
                ulong eb = mode == ScanMode.Initialize ? 0 : unchecked(firstWord + (ulong)i + 1) ^ pattern;
                ulong ec = mode == ScanMode.Initialize ? 0 : unchecked(firstWord + (ulong)i + 2) ^ pattern;
                ulong ed = mode == ScanMode.Initialize ? 0 : unchecked(firstWord + (ulong)i + 3) ^ pattern;
                if (a != ea || b != eb || c != ec || d != ed) return FindFailure(i, a, b, c, d);
                if (mode != ScanMode.Verify)
                {
                    pointer[i] = a ^ (mode == ScanMode.Initialize ? unchecked(firstWord + (ulong)i) ^ initialPattern : mask);
                    pointer[i + 1] = b ^ (mode == ScanMode.Initialize ? unchecked(firstWord + (ulong)i + 1) ^ initialPattern : mask);
                    pointer[i + 2] = c ^ (mode == ScanMode.Initialize ? unchecked(firstWord + (ulong)i + 2) ^ initialPattern : mask);
                    pointer[i + 3] = d ^ (mode == ScanMode.Initialize ? unchecked(firstWord + (ulong)i + 3) ^ initialPattern : mask);
                }
            }
        }
        for (; i < words; i++)
        {
            ulong value = pointer[i];
            ulong expected = mode == ScanMode.Initialize ? 0 : unchecked(firstWord + (ulong)i) ^ pattern;
            if (value != expected) return new(unchecked((firstWord + (ulong)i) * 8), expected, value, kind);
            if (mode != ScanMode.Verify)
                pointer[i] = value ^ (mode == ScanMode.Initialize ? unchecked(firstWord + (ulong)i) ^ initialPattern : mask);
        }
        return null;

        Mismatch? FindFailure(int start, ulong a, ulong b, ulong c, ulong d)
        {
            for (int j = start; j < start + 4; j++)
            {
                ulong expected = mode == ScanMode.Initialize ? 0 : unchecked(firstWord + (ulong)j) ^ pattern;
                ulong actual = (j - start) switch { 0 => a, 1 => b, 2 => c, _ => d };
                if (actual != expected) return new(unchecked((firstWord + (ulong)j) * 8), expected, actual, kind);
            }
            // Preserve the values from the failing load; rereading memory could lose a transient error.
            // A SIMD comparison failure remains an issue even if scalar lane comparisons agree.
            ulong reference = mode == ScanMode.Initialize ? 0 : unchecked(firstWord + (ulong)start) ^ pattern;
            return new(unchecked((firstWord + (ulong)start) * 8), reference, a, kind);
        }
    }
}
