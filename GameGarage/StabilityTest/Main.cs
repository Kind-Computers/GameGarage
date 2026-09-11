// Copyright Kind Computers. Licensed under the MIT License.
using System.Globalization;

namespace StabilityTest;

internal static class Program
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    internal static int Main(string[] args)
    {
        if (args.Length == 1 && (args[0] == "/?" || args[0] == "--help" || args[0] == "/Help"))
        {
            PrintHelp();
            return 0;
        }
        if (!TryParse(args, out var parsed, out string error))
        {
            Console.Error.WriteLine(error);
            PrintHelp();
            return (int)ScanStatus.Error;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var allocator = new WindowsRamAllocator();
            MemorySnapshot memory = allocator.Snapshot();
            ulong reserve = parsed.ReserveIsPercent
                ? (ulong)(memory.TotalPhysicalBytes * (parsed.Reserve / 100m))
                : checked((ulong)parsed.Reserve * GiB);
            var options = new ScanOptions(parsed.Passes, reserve, parsed.Threads, parsed.Seed);
            Console.WriteLine(WorkerText.Get("Title"));
            Console.WriteLine(WorkerText.Format("Algorithm", ScanAlgorithm.Version));
            Console.WriteLine(WorkerText.Format("Passes", options.Passes));
            Console.WriteLine(WorkerText.Format("Reserve", reserve));
            Console.WriteLine(WorkerText.Format("Threads", options.Threads));
            Console.WriteLine(WorkerText.Format("Seed", options.Seed));
            Console.WriteLine(WorkerText.Get("Patterns"));
            ScanResult result = RAMTest.Run(options, allocator, cancellation.Token,
                value => Console.WriteLine(WorkerText.Format("Progress", value.ToString("0.0", CultureInfo.InvariantCulture))),
                Console.WriteLine);
            PrintResult(result);
            return (int)result.Status;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(WorkerText.Format("RunError", ex.Message));
            Console.WriteLine($"Result: {ScanStatus.Error}");
            return (int)ScanStatus.Error;
        }
        finally { Console.CancelKeyPress -= cancelHandler; }
    }

    internal readonly record struct ParsedOptions(int Passes, decimal Reserve, bool ReserveIsPercent, int Threads, int Seed);

    internal static bool TryParse(string[] args, out ParsedOptions options, out string error)
    {
        int passes = 10, threads = Math.Max(Environment.ProcessorCount / 2, 1), seed = Random.Shared.Next();
        decimal reserve = 1;
        bool reservePercent = false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        options = default;
        error = "";
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (key.Equals("/Background", StringComparison.OrdinalIgnoreCase))
            {
                error = WorkerText.Get("BackgroundDeferred");
                return false;
            }
            if (!seen.Add(key) || i + 1 >= args.Length)
            {
                error = WorkerText.Format("DuplicateOrMissingOption", key);
                return false;
            }
            string value = args[++i];
            switch (key.ToUpperInvariant())
            {
                case "/PASSES":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out passes) || passes < 1)
                    { error = WorkerText.Get("PassesInvalid"); return false; }
                    break;
                case "/SEED":
                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out seed))
                    { error = WorkerText.Get("SeedInvalid"); return false; }
                    break;
                case "/RESERVEGB":
                    reservePercent = value.EndsWith('%');
                    string reserveValue = reservePercent ? value[..^1] : value;
                    if (!decimal.TryParse(reserveValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out reserve)
                        || reserve < 0 || (reservePercent ? reserve > 100 : reserve != decimal.Truncate(reserve) || reserve > int.MaxValue))
                    { error = WorkerText.Get("ReserveInvalid"); return false; }
                    break;
                case "/THREADS":
                    if (value.EndsWith('%'))
                    {
                        if (!decimal.TryParse(value[..^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal percent)
                            || percent <= 0 || percent > 100)
                        { error = WorkerText.Get("ThreadsPercentInvalid"); return false; }
                        threads = Math.Max(1, (int)(Environment.ProcessorCount * percent / 100));
                    }
                    else if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out threads) || threads < 1)
                    { error = WorkerText.Get("ThreadsInvalid"); return false; }
                    threads = Math.Min(threads, Environment.ProcessorCount);
                    break;
                default:
                    error = WorkerText.Format("UnknownOption", key);
                    return false;
            }
        }
        options = new(passes, reserve, reservePercent, threads, seed);
        return true;
    }

    private static void PrintResult(ScanResult result)
    {
        Console.WriteLine(WorkerText.Format("AllocatedBytes", result.AllocatedBytes));
        Console.WriteLine(WorkerText.Format("TestedBytes", result.TestedBytes));
        Console.WriteLine(WorkerText.Format("CompletedPasses", result.CompletedPasses));
        Console.WriteLine(WorkerText.Format("Elapsed", result.Elapsed));
        for (int i = 0; i < result.KernelBytes.Length; i++)
            Console.WriteLine(WorkerText.Format("KernelCoverage", (KernelKind)i, result.KernelBytes[i]));
        if (result.Mismatch is { } failure)
            Console.WriteLine(WorkerText.Format("Mismatch", failure.ByteOffset, failure.Expected, failure.Actual, failure.Kernel)
                + (failure.Expected == failure.Actual ? WorkerText.Get("ComparisonFailure") : ""));
        if (result.Detail != null) Console.WriteLine(result.Detail);
        // This terminal marker and enum identifiers are an invariant machine protocol.
        Console.WriteLine($"Result: {result.Status}");
    }

    private static void PrintHelp() => Console.WriteLine(WorkerText.Get("Help"));
}
