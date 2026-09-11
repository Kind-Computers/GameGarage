using System.Runtime.InteropServices;
using StabilityTest;
int count = 0;
void Test(string name, Action body) { body(); Console.WriteLine($"PASS {name}"); count++; }
void Check(bool value, string message) { if (!value) throw new Exception(message); }
Test("CLI valid options and invalid options do not scan", () => {
    Check(StabilityTest.Program.TryParse(["/Passes","1","/Seed","-123","/Threads","25%","/ReserveGB","10%"],out _,out _), "valid options");
    foreach(var args in new[] {new[]{"/Background"},new[]{"/Passes"},new[]{"/Passes","0"},new[]{"/Threads","0%"},new[]{"/Seed","x"},new[]{"/ReserveGB","101%"},new[]{"/Passes","2","/Passes","3"}})
        Check(!StabilityTest.Program.TryParse(args,out _,out _), "invalid options accepted");
});
foreach(var kind in RamKernel.Supported) {
    Test($"{kind}: scalar oracle, complement, unaligned vector load and tail guards", () => {
        using var buffer = new OwnedBuffer(168);
        nint address=buffer.Address+8; ulong first=ulong.MaxValue-30, seed=0xF0123456789ABCDEUL;
        Marshal.WriteInt64(buffer.Address,1234); Marshal.WriteInt64(buffer.Address,160,5678);
        Check(RamKernel.Scan(address,152,first,ScanMode.Initialize,0,0,seed,kind)==null,"initialize");
        for(int i=0;i<19;i++) Check((ulong)Marshal.ReadInt64(address,i*8)==(unchecked(first+(ulong)i)^seed),"oracle mismatch");
        Check(RamKernel.Scan(address,152,first,ScanMode.Update,seed,ulong.MaxValue,0,kind)==null,"complement");
        Check(RamKernel.Scan(address,152,first,ScanMode.Verify,~seed,0,0,kind)==null,"verify");
        Check(Marshal.ReadInt64(buffer.Address)==1234 && Marshal.ReadInt64(buffer.Address,160)==5678,"guard overwritten");
    });
    Test($"{kind}: each SIMD lane and tail reports exact offset and values", () => {
        for(int lane=0;lane<9;lane++) {
            using var buffer=new OwnedBuffer(72);
            RamKernel.Scan(buffer.Address,72,4096,ScanMode.Initialize,0,0,789,kind);
            ulong before=(ulong)Marshal.ReadInt64(buffer.Address,lane*8);
            Marshal.WriteInt64(buffer.Address,lane*8,(long)(before^0x100));
            var failure=RamKernel.Scan(buffer.Address,72,4096,ScanMode.Verify,789,0,0,kind);
            Check(failure?.ByteOffset==(4096UL+(ulong)lane)*8 && failure?.Expected==before && failure?.Actual==(before^0x100),"wrong evidence");
        }
    });
    Test($"{kind}: copied neighbor is detected", () => {
        using var b=new OwnedBuffer(64);
        RamKernel.Scan(b.Address,64,0,ScanMode.Initialize,0,0,321,kind);
        Marshal.WriteInt64(b.Address,8,Marshal.ReadInt64(b.Address));
        Check(RamKernel.Scan(b.Address,64,0,ScanMode.Verify,321,0,0,kind)?.ByteOffset==8,"neighbor accepted");
    });
}
Test("one update verifies final pattern and distinct block identities", () => {
    using var a=new OwnedBuffer(64); using var b=new OwnedBuffer(64);
    var r=RAMTest.RunBlocks(new(1,0,2,7),[new(a.Address,64,0),new(b.Address,64,8)],default);
    Check(r.Status==ScanStatus.Passed && r.TestedBytes==128 && r.CompletedPasses==1,"one pass");
    Check(Marshal.ReadInt64(a.Address)!=Marshal.ReadInt64(b.Address),"block pattern repeated");
});
Test("error injected after last update fails final readback", () => {
    using var b=new OwnedBuffer(64);
    var r=RAMTest.RunBlocks(new(10,0,1,7),[new(b.Address,64,0)],default,afterUpdate:(pass,blocks)=>{
        if(pass==10) Marshal.WriteInt64(blocks[0].Address,56,Marshal.ReadInt64(blocks[0].Address,56)^1);
    });
    Check(r.Status==ScanStatus.Issues && r.Mismatch?.ByteOffset==56 && r.TestedBytes==0,"final write unchecked");
});
Test("mid-scan cancellation keeps completed count and cannot pass", () => {
    using var b=new OwnedBuffer(64); using var c=new CancellationTokenSource();
    var r=RAMTest.RunBlocks(new(10,0,1,7),[new(b.Address,64,0)],c.Token,afterUpdate:(_,_)=>c.Cancel());
    Check(r.Status==ScanStatus.Cancelled && r.CompletedPasses==1 && r.TestedBytes==0,"cancel passed");
});
Test("memory pressure is inconclusive", () => {
    using var b=new OwnedBuffer(64);
    Check(RAMTest.RunBlocks(new(2,1,1,7),[new(b.Address,64,0)],default,memoryAvailable:()=>false).Status==ScanStatus.Inconclusive,"pressure passed");
});
Test("initial pool stays bounded and all allocations are freed", () => {
    var a=new FakeAllocator(256);
    var r=RAMTest.Run(new(2,128,2,7){BlockBytes=64},a,default);
    Check(r.Status==ScanStatus.Passed && r.AllocatedBytes==128 && a.Allocations==2 && a.Frees==2,"allocation lifetime");
});
Test("empty scan is inconclusive", () => {
    var a=new FakeAllocator(32);
    Check(RAMTest.Run(new(2,0,1,7){BlockBytes=64},a,default).Status==ScanStatus.Inconclusive && a.Allocations==0,"empty passed");
});
Test("allocation failure frees prior blocks", () => {
    var a=new FakeAllocator(256){FailAllocationNumber=2};
    Check(RAMTest.Run(new(2,0,1,7){BlockBytes=64,MaximumBytes=128},a,default).Status==ScanStatus.Error && a.Frees==1,"leaked allocation");
});
Test("cleanup failure remains an error and other frees continue", () => {
    var a=new FakeAllocator(256){FailFirstFree=true};
    Check(RAMTest.Run(new(2,0,1,7){BlockBytes=64,MaximumBytes=128},a,default).Status==ScanStatus.Error && a.Frees==2,"cleanup failure");
});
Test("pre-cancellation allocates nothing", () => {
    using var c=new CancellationTokenSource(); c.Cancel(); var a=new FakeAllocator(256);
    Check(RAMTest.Run(new(2,0,1,7){BlockBytes=64},a,c.Token).Status==ScanStatus.Cancelled && a.Allocations==0,"pre-cancel");
});
Test("worker exception completes task and worker remains usable", () => {
    using var pool=new UtilityThreadPool<int>(1,ThreadPriority.Lowest);
    try { pool.AddTask(()=>throw new InvalidOperationException("injected")).GetAwaiter().GetResult(); throw new Exception("swallowed"); }
    catch(InvalidOperationException) {}
    Check(pool.AddTask(()=>42).GetAwaiter().GetResult()==42,"worker stopped");
});
Test("production stripes cover all supported kernels", () => {
    using var b=new OwnedBuffer(1024*1024);
    var r=RAMTest.RunBlocks(new(2,0,1,7),[new(b.Address,1024*1024,0)],default);
    Check(r.Status==ScanStatus.Passed && r.TestedBytes==1024*1024,"stripe result");
    foreach(var k in RamKernel.Supported) Check(r.KernelBytes[(int)k]>0,"missing kernel");
});
Test("worker resources fall back to English and preserve invariant protocol values", () => {
    var priorUi=System.Globalization.CultureInfo.CurrentUICulture;
    var priorCulture=System.Globalization.CultureInfo.CurrentCulture;
    try {
        System.Globalization.CultureInfo.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
        System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
        Check(WorkerText.Get("Title")=="Game Garage RAM conformance sweep","neutral English fallback");
        Check(WorkerText.Format("Progress",1.5.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture))=="Testing memory: 1.5%","progress decimal changed");
        Check(StabilityTest.Program.TryParse(["/Threads","12.5%","/ReserveGB","25.5%","/Seed","-7"],out var parsed,out _)
            && parsed.Seed==-7 && parsed.Reserve==25.5m,"invariant CLI numbers");
        Check(!StabilityTest.Program.TryParse(["/Passes","0"],out _,out var error)
            && error==WorkerText.Get("PassesInvalid"),"localized validation");
    }
    finally {
        System.Globalization.CultureInfo.CurrentUICulture=priorUi;
        System.Globalization.CultureInfo.CurrentCulture=priorCulture;
    }
});
Test("scan reports its stable algorithm identifier", () => {
    using var b=new OwnedBuffer(64);
    var r=RAMTest.RunBlocks(new(1,0,1,7),[new(b.Address,64,0)],default);
    Check(r.AlgorithmVersion=="xor-address-v1" && r.Status==ScanStatus.Passed,"algorithm metadata");
});
Console.WriteLine($"{count} RAM checks passed. Only bounded owned buffers were used.");
internal sealed unsafe class OwnedBuffer : IDisposable {
    internal nint Address {get;}
    internal OwnedBuffer(int bytes) {Address=(nint)NativeMemory.AllocZeroed((nuint)bytes);if(Address==0)throw new OutOfMemoryException();}
    public void Dispose()=>NativeMemory.Free((void*)Address);
}
internal sealed unsafe class FakeAllocator(ulong available):IRamAllocator {
    ulong used; internal int Allocations,Frees,FailAllocationNumber; internal bool FailFirstFree;
    public MemorySnapshot Snapshot()=>new(available,available-used,1024UL*1024*1024);
    public RamBlock Allocate(int bytes,ulong firstWord) {
        Allocations++;if(Allocations==FailAllocationNumber)throw new OutOfMemoryException("injected");
        nint p=(nint)NativeMemory.AllocZeroed((nuint)bytes);if(p==0)throw new OutOfMemoryException();used+=(ulong)bytes;return new(p,bytes,firstWord);
    }
    public void Free(RamBlock block) {
        NativeMemory.Free((void*)block.Address);used-=(ulong)block.Bytes;Frees++;
        if(FailFirstFree && Frees==1)throw new InvalidOperationException("injected");
    }
}
