// Benchmark adapter: unchanged 2024 hot loops; caller supplies the deterministic kernel choice.
// See ../legacy/RAMTest.2024.cs for pristine source.
using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using StabilityTest;
internal static class LegacyKernel {
const int BlockSize = 1024 * 1024 * 1024;
        internal static bool TestBlock(IntPtr memBlock, Int64 ExpectedValue, Int64 XORMask, KernelKind kind)
        {
            // Test the block
            unsafe
            {
                Debug.Assert(((BlockSize % 8) == 0), "BlockSize must be a multiple of 8 bytes.");
                Int64* memBlockPtr = (Int64*)memBlock.ToPointer();

                const int UnrollSize = 4;
                Debug.Assert(((BlockSize % (8 * UnrollSize)) == 0), $"BlockSize must be a multiple of {(8 * UnrollSize)} bytes.");

                // Randomize the code paths to test different CPU features
                bool AllowAVX = (kind == KernelKind.Avx2);
                bool AllowSSE = (kind == KernelKind.Sse41);
                bool AllowUnrolled = (kind == KernelKind.Unrolled);

                if (AllowAVX && Avx2.IsSupported)
                {
                    Vector256<long> ExpectedValue256 = Vector256.Create(ExpectedValue);
                    Vector256<long> XORMask256 = Vector256.Create(XORMask);

                    for (int i = 0; i < (BlockSize / 8); i += UnrollSize)
                    {
                        // Load
                        Vector256<long> ReadValue0123 = Avx.LoadVector256(&memBlockPtr[i + 0]);

                        // Compare
                        Vector256<long> ComparisonMask0123 = Avx2.CompareEqual(ReadValue0123, ExpectedValue256);
                        int Mask0123 = Avx.MoveMask(ComparisonMask0123.AsDouble());

                        if (Mask0123 != 0xF)
                        {
                            return false;
                        }

                        // XOR the value
                        // Note: we are making the CPU do work here on purpose.
                        // Note: this helps ensure that data is stable on a round-trip path through the CPU.
                        // Note: for example, if the CPU was overclocked this could help detect instability.
                        Vector256<long> NewValue0123 = Avx2.Xor(ReadValue0123, XORMask256);

                        // Store
                        Avx.Store(&memBlockPtr[i + 0], NewValue0123);
                    }
                }
                else if (AllowSSE && Sse41.IsSupported)
                {
                    Vector128<long> ExpectedValue128 = Vector128.Create(ExpectedValue);
                    Vector128<long> XORMask128 = Vector128.Create(XORMask);

                    for (int i = 0; i < (BlockSize / 8); i += UnrollSize)
                    {
                        // Load
                        Vector128<long> ReadValue01 = Sse2.LoadVector128(&memBlockPtr[i + 0]);
                        Vector128<long> ReadValue23 = Sse2.LoadVector128(&memBlockPtr[i + 2]);

                        // Compare
                        Vector128<long> ComparisonMask01 = Sse41.CompareEqual(ReadValue01, ExpectedValue128);
                        Vector128<long> ComparisonMask23 = Sse41.CompareEqual(ReadValue23, ExpectedValue128);

                        int Mask01 = Sse2.MoveMask(ComparisonMask01.AsDouble());
                        int Mask23 = Sse2.MoveMask(ComparisonMask23.AsDouble());

                        if ((Mask01 != 0x3) ||
                            (Mask23 != 0x3))
                        {
                            return false;
                        }

                        // XOR the value
                        // Note: we are making the CPU do work here on purpose.
                        // Note: this helps ensure that data is stable on a round-trip path through the CPU.
                        // Note: for example, if the CPU was overclocked this could help detect instability.
                        Vector128<long> NewValue01 = Sse2.Xor(ReadValue01, XORMask128);
                        Vector128<long> NewValue23 = Sse2.Xor(ReadValue23, XORMask128);

                        // Store
                        Sse2.Store(&memBlockPtr[i + 0], NewValue01);
                        Sse2.Store(&memBlockPtr[i + 2], NewValue23);
                    }
                }
                else if (AllowUnrolled)
                {
                    for (int i = 0; i < (BlockSize / 8); i += UnrollSize)
                    {
                        // Load
                        Int64 ReadValue0 = memBlockPtr[i + 0];
                        Int64 ReadValue1 = memBlockPtr[i + 1];
                        Int64 ReadValue2 = memBlockPtr[i + 2];
                        Int64 ReadValue3 = memBlockPtr[i + 3];

                        // Compare
                        if (ReadValue0 != ExpectedValue ||
                            ReadValue1 != ExpectedValue ||
                            ReadValue2 != ExpectedValue ||
                            ReadValue3 != ExpectedValue)
                        {
                            return false;
                        }

                        // XOR the value
                        // Note: we are making the CPU do work here on purpose.
                        // Note: this helps ensure that data is stable on a round-trip path through the CPU.
                        // Note: for example, if the CPU was overclocked this could help detect instability.
                        Int64 NewValue0 = (ReadValue0 ^ XORMask);
                        Int64 NewValue1 = (ReadValue1 ^ XORMask);
                        Int64 NewValue2 = (ReadValue2 ^ XORMask);
                        Int64 NewValue3 = (ReadValue3 ^ XORMask);

                        // Store
                        memBlockPtr[i + 0] = NewValue0;
                        memBlockPtr[i + 1] = NewValue1;
                        memBlockPtr[i + 2] = NewValue2;
                        memBlockPtr[i + 3] = NewValue3;
                    }
                }
                else
                {
                    for (int i = 0; i < (BlockSize / 8); i++)
                    {
                        Int64 ReadValue = memBlockPtr[i];

                        if (ReadValue != ExpectedValue)
                        {
                            return false;
                        }

                        // XOR the value
                        // Note: we are making the CPU do work here on purpose.
                        // Note: this helps ensure that data is stable on a round-trip path through the CPU.
                        // Note: for example, if the CPU was overclocked this could help detect instability.
                        Int64 NewValue = (ReadValue ^ XORMask);

                        memBlockPtr[i] = NewValue;
                    }
                }
            }

            return true;
        }

}