using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using StabilityTest;
int mib=2048,threads=2,runs=5;
string output="artifacts/ram-benchmark.json";
for(int i=0;i<args.Length;i+=2) {
    if(i+1>=args.Length)throw new ArgumentException("Expected --mib N --threads N --runs N --output PATH.");
    switch(args[i]) {
        case "--mib":mib=int.Parse(args[i+1],CultureInfo.InvariantCulture);break;
        case "--threads":threads=int.Parse(args[i+1],CultureInfo.InvariantCulture);break;
        case "--runs":runs=int.Parse(args[i+1],CultureInfo.InvariantCulture);break;
        case "--output":output=args[i+1];break;
        default:throw new ArgumentException($"Unknown option {args[i]}");
    }
}
if(mib<1024 || mib>4096 || mib%1024!=0 || threads<1 || threads>4 || runs<3 || runs>15)
    throw new ArgumentException("Bounds: 1024..4096 MiB in 1024 MiB steps; 1..4 threads; 3..15 paired runs.");
ulong bytes=(ulong)mib*1024*1024;
if(RAMStats.GetSnapshot().AvailablePhysicalBytes<bytes+4UL*1024*1024*1024)throw new InvalidOperationException("Requires buffer size plus 4 GiB free.");
var options=new ScanOptions(10,0,threads,12345){MaximumBytes=bytes};
Console.WriteLine($"Bounded benchmark: {mib} MiB, {threads} workers, 10 updates, .NET {Environment.Version}, {RuntimeInformation.ProcessArchitecture}.");
Console.WriteLine($"Fastest kernel: {RamKernel.Fastest}; includes allocation, commitment, scan, workers and cleanup.");
Console.WriteLine("Warming both algorithms...");
RunLegacy();RunCandidate();
var legacy=new double[runs];var candidate=new double[runs];
for(int i=0;i<runs;i++) {
    if(i%2==0){legacy[i]=RunLegacy();candidate[i]=RunCandidate();}
    else{candidate[i]=RunCandidate();legacy[i]=RunLegacy();}
    Console.WriteLine($"Pair {i+1}: legacy={legacy[i]:F3}s candidate={candidate[i]:F3}s ratio={candidate[i]/legacy[i]:F3}");
}
double Median(double[] values)=>values.Order().ElementAt(values.Length/2);
double legacyMedian=Median(legacy),candidateMedian=Median(candidate),ratio=candidateMedian/legacyMedian;
var report=new {
    measuredUtc=DateTimeOffset.UtcNow,algorithmVersion=ScanAlgorithm.Version,runtime=Environment.Version.ToString(),architecture=RuntimeInformation.ProcessArchitecture.ToString(),
    processorCount=Environment.ProcessorCount,fastestKernel=RamKernel.Fastest.ToString(),memoryMiB=mib,threads,passes=10,seed=options.Seed,pairedRuns=runs,
    legacySeconds=legacy,candidateSeconds=candidate,legacyMedianSeconds=legacyMedian,candidateMedianSeconds=candidateMedian,ratio,threshold=1.10,passed=ratio<=1.10,
    methodology="Original 2024 hot loops, fixed-seed original per-block random path distribution. Same bounded allocation/runtime/threads/bytes/update count. Candidate additionally verifies final pattern. Both include allocation/touch/scan/free; order alternates."
};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Median ratio: {ratio:F3}; gate {(ratio<=1.10?"PASS":"FAIL")}. Saved {output}.");
return ratio<=1.10?0:1;
double RunCandidate() {
    var timer=Stopwatch.StartNew();
    var result=RAMTest.Run(options,new WindowsRamAllocator(),CancellationToken.None);
    timer.Stop();
    if(result.Status!=ScanStatus.Passed || result.TestedBytes!=bytes)throw new Exception($"Candidate failed: {result}");
    return timer.Elapsed.TotalSeconds;
}
double RunLegacy() {
    var timer=Stopwatch.StartNew();var blocks=new List<RamBlock>();
    try {
        for(ulong start=0;start<bytes;start+=1024UL*1024*1024) {
            nint address=VirtualAllocUtilities.Alloc(1024U*1024*1024);
            blocks.Add(new(address,1024*1024*1024,start/8));
            for(int offset=0;offset<1024*1024*1024;offset+=Environment.SystemPageSize)Marshal.WriteByte(address,offset,0);
        }
        using var pool=new UtilityThreadPool<bool>(Math.Min(threads,blocks.Count),ThreadPriority.Lowest);
        var random=new Random(options.Seed);
        ulong generator=(uint)options.Seed,expected=0;
        for(int pass=0;pass<10;pass++) {
            ulong next=pass%2==0?RAMTest.NextPattern(ref generator):~expected,mask=expected^next;
            var tasks=new List<Task<bool>>();
            foreach(var block in blocks) {
                bool avx=(random.Next()&1)==0,sse=(random.Next()&1)==0,unrolled=(random.Next()&1)==0;
                var kind=avx&&RamKernel.IsSupported(KernelKind.Avx2)?KernelKind.Avx2
                    :sse&&RamKernel.IsSupported(KernelKind.Sse41)?KernelKind.Sse41
                    :unrolled?KernelKind.Unrolled:KernelKind.Scalar;
                long stageExpected=(long)expected,stageMask=(long)mask;
                tasks.Add(pool.AddTask(()=>LegacyKernel.TestBlock(block.Address,stageExpected,stageMask,kind)));
            }
            if(Task.WhenAll(tasks).GetAwaiter().GetResult().Any(passed=>!passed))throw new Exception("Legacy mismatch.");
            expected=next;
        }
    }
    finally{foreach(var block in blocks)VirtualAllocUtilities.Free(block.Address);}
    timer.Stop();return timer.Elapsed.TotalSeconds;
}
