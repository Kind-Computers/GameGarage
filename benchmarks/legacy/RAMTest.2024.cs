
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace StabilityTest
{
    internal class RAMTest(int NumPasses, int NumReserveGB, int NumThreads, CancellationToken CancelToken)
    {
        const int BlockSize = (1 * 1024 * (1024 * 1024)); // 1 GB

        public bool RunRAMTest()
        {
            List<IntPtr> memBlocks = [];

            try
            {
                Random rnd = new();
                Int64 ExpectedValue = 0;

                // Update the UI
                Console.WriteLine();
                Console.WriteLine("Preparing to allocate memory...");

                // Allocate memory in 1 GB blocks until there is NumReserveGB left
                memBlocks.AddRange(AllocMemTestBlocks(ExpectedValue, ShowProgress: true));

                Console.WriteLine();
                Console.WriteLine($"Testing stability with {NumThreads} threads...");

                // Test RAM using low-priority threads
                using (UtilityThreadPool<bool> TestPool = new(NumThreads, ThreadPriority.Lowest))
                {
                    // Test all blocks X times
                    for (Int64 i = 0; ((i < NumPasses) && (CancelToken.IsCancellationRequested == false)); i++)
                    {
                        // Add and remove blocks as needed
                        ManageBlocks(ref memBlocks, ExpectedValue);

                        // Randomly flip the sign to ensure all mask bits are random
                        bool FlipMaskSign = ((rnd.Next() & 1) == 0);

                        // Generate a random XOR mask
                        Int64 XORMask = (FlipMaskSign ? rnd.NextInt64() : (-rnd.NextInt64()));

                        /*
                        Debug.WriteLine($"Flip Mask Sign: {FlipMaskSign}");
                        Debug.WriteLine($"Expected Value: {ConvertToBinary(ExpectedValue)}");
                        Debug.WriteLine($"XOR Mask: {ConvertToBinary(XORMask)}");
                        //*/

                        double CurrentPercentage = (((double)i / NumPasses) * 100.0);
                        double PortionPercentage = ((1.0 / NumPasses) * 100.0);

                        if (!TestBlockList(memBlocks, ExpectedValue, XORMask, CurrentPercentage, PortionPercentage, TestPool))
                        {
                            return false;
                        }

                        ExpectedValue = (ExpectedValue ^ XORMask);
                    }

                    // Update the UI
                    if (CancelToken.IsCancellationRequested == false)
                    {
                        Console.WriteLine($"Testing {memBlocks.Count} GB: 100%          ");
                    }
                    else
                    {
                        Console.WriteLine("Testing cancelled by request.          ");
                    }

                    Console.WriteLine();
                }
            }
            finally
            {
                // Free all blocks
                foreach (IntPtr memBlock in memBlocks)
                {
                    VirtualAllocUtilities.Free(memBlock);
                }
            }

            return true;
        }

        bool TestBlockList(List<IntPtr> memBlocks, Int64 ExpectedValue, Int64 XORMask, double CurrentPercentage, double PortionPercentage, UtilityThreadPool<bool> TestPool)
        {
            bool AllBlocksOk = true;

            List<Task<bool>> TestResults = [];
            foreach (IntPtr memBlock in memBlocks)
            {
                TestResults.Add(TestPool.AddTask(() =>
                {
                    if (CancelToken.IsCancellationRequested == false)
                    {
                        return TestBlock(memBlock, ExpectedValue, XORMask);
                    }
                    else
                    {
                        return true;
                    }
                }));
            }

            // Get all test results
            int NumResultsColleced = 0;
            int LastTotalPercentage = -1;
            foreach (Task<bool> TestResult in TestResults)
            {
                if (TestResult.Result == false)
                {
                    AllBlocksOk = false;
                }

                // Update the UI
                NumResultsColleced++;
                double Percentage = ((double)NumResultsColleced / TestResults.Count());
                double CurrentPortionPercentage = (PortionPercentage * Percentage);
                int CurrentTotalPercentage = (int)Math.Ceiling(CurrentPercentage + CurrentPortionPercentage);
                if (CurrentTotalPercentage != LastTotalPercentage)
                {
                    Console.Write($"Testing {memBlocks.Count} GB: {CurrentTotalPercentage}%          \r");
                    LastTotalPercentage = CurrentTotalPercentage;
                }
            }

            return AllBlocksOk;
        }

        static bool TestBlock(IntPtr memBlock, Int64 ExpectedValue, Int64 XORMask)
        {
            // Test the block
            unsafe
            {
                Debug.Assert(((BlockSize % 8) == 0), "BlockSize must be a multiple of 8 bytes.");
                Int64* memBlockPtr = (Int64*)memBlock.ToPointer();

                const int UnrollSize = 4;
                Debug.Assert(((BlockSize % (8 * UnrollSize)) == 0), $"BlockSize must be a multiple of {(8 * UnrollSize)} bytes.");

                // Randomize the code paths to test different CPU features
                bool AllowAVX = ((Random.Shared.Next() & 1) == 0);
                bool AllowSSE = ((Random.Shared.Next() & 1) == 0);
                bool AllowUnrolled = ((Random.Shared.Next() & 1) == 0);

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

        void ManageBlocks(ref List<IntPtr> memBlocks, Int64 ExpectedValue)
        {
            // Add blocks if possible
            List<IntPtr> AllocatedBlocks = AllocMemTestBlocks(ExpectedValue, ShowProgress: false);

            if (AllocatedBlocks.Count > 0)
            {
                // Update the UI
                //OutputDelegate($"Allocated {AllocatedBlocks.Count} blocks of {BlockSize / (1024 * 1024)} MB each.", true);
                //Debug.WriteLine($"Allocated {AllocatedBlocks.Count} blocks of {BlockSize / (1024 * 1024)} MB each.");

                memBlocks.AddRange(AllocatedBlocks);
            }
            else
            {
                // Remove blocks as necessary
                while ((IsEnoughMemoryAvailable(WantToAllocate: false) == false) &&
                    (memBlocks.Count() > 0))
                {
                    VirtualAllocUtilities.Free(memBlocks[0]);
                    memBlocks.RemoveAt(0);

                    //OutputDelegate($"Freed block of {BlockSize} bytes due to memory pressure.", true);
                    //Debug.WriteLine($"Freed block due to memory pressure.");
                }

                /*
                // TODO: REMOVE ME!!!
                Random rnd = new();
                if (((rnd.Next() & 1) == 0) &&
                    (memBlocks.Count() > 0))
                {
                    VirtualAllocWrapper.Free(memBlocks[0]);
                    memBlocks.RemoveAt(0);

                    Debug.WriteLine($"Freed block due to memory pressure.");
                }
                //*/
            }
        }

        List<IntPtr> AllocMemTestBlocks(Int64 ExpectedValue, bool ShowProgress)
        {
            List<IntPtr> memBlocks = [];

            int TotalGBToAllocate = (RAMStats.GetAvailablePhysicalGB() - NumReserveGB);

            // Allocate memory in 1 GB blocks until there is NumReserveGB left
            while ((IsEnoughMemoryAvailable(WantToAllocate: true) == true) &&
                   (CancelToken.IsCancellationRequested == false))
            {
                // Allocate 1 GB of memory
                IntPtr memBlock = VirtualAllocUtilities.Alloc(BlockSize);

                // Fill the block with the expected value to allocate physical memory
                FillVirtualAllocBlock(memBlock, ExpectedValue);

                memBlocks.Add(memBlock);

                // Update the UI
                if (ShowProgress)
                {
                    // Update the total in case more memory became available
                    TotalGBToAllocate = Math.Max(TotalGBToAllocate, memBlocks.Count());

                    Console.Write($"Allocated {(int)Math.Ceiling(((double)memBlocks.Count() / TotalGBToAllocate) * 100.0)}% of {TotalGBToAllocate} GB          \r");
                }
            }

            // Update the UI
            if (ShowProgress)
            {
                if (CancelToken.IsCancellationRequested == false)
                {
                    // Note: we don't use TotalGBToAllocate because we may have allocated less due to memory pressure
                    Console.WriteLine($"Allocated 100% of {memBlocks.Count()} GB          ");
                }
                else
                {
                    Console.WriteLine("Memory allocation cancelled by request.          ");
                }
            }

            return memBlocks;
        }

        static void FillVirtualAllocBlock(IntPtr memBlock, Int64 ExpectedValue)
        {
            // VirtualAlloc() guarantees zeroed memory, so we can use a shortcut here
            if (ExpectedValue == 0)
            {
                // Touch the pages to allocate physical memory
                int NumPagesPerBlock = (BlockSize / Environment.SystemPageSize);

                for (int i = 0; i < NumPagesPerBlock; i++)
                {
                    Marshal.WriteByte(memBlock, (i * Environment.SystemPageSize), 0);
                }
            }
            else
            {
                // Write the expected value to allocate physical memory
                unsafe
                {
                    Int64* memBlockPtr = (Int64*)memBlock.ToPointer();

                    for (int i = 0; i < (BlockSize / 8); i++)
                    {
                        memBlockPtr[i] = ExpectedValue;
                    }
                }
            }
        }

        bool IsEnoughMemoryAvailable(bool WantToAllocate)
        {
            if (WantToAllocate)
            {
                return ((RAMStats.GetAvailablePhysicalGB() > NumReserveGB) &&
                        (RAMStats.GetAvailableVirtualGB() > 1));
            }
            else
            {
                return ((RAMStats.GetAvailablePhysicalGB() >= NumReserveGB) &&
                        (RAMStats.GetAvailableVirtualGB() >= 1));
            }
        }

        static string ConvertToBinary(Int64 Value)
        {
            return Convert.ToString(Value, 2).PadLeft(64, '0');
        }

        private readonly int NumPasses = NumPasses;
        private readonly int NumReserveGB = NumReserveGB;
        private readonly int NumThreads = NumThreads;
        private readonly CancellationToken CancelToken = CancelToken;
    }
}
