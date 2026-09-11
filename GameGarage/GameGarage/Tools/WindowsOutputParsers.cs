using static GameGarage.Tools.VerifierBase;

namespace GameGarage.Tools;

// These parsers accept captured output; they never launch Windows tools.
internal sealed record ToolRunResult(int ExitCode, IReadOnlyList<string> Output,
    IReadOnlyList<string> StandardError, bool Cancelled = false, bool LaunchFailed = false)
{
    public bool Has(string text) => Output.Any(line => line.Contains(text, StringComparison.OrdinalIgnoreCase));
    public bool HasErrors => StandardError.Any(line => !string.IsNullOrWhiteSpace(line));
}

internal static class WindowsOutputParsers
{
    public static VerifierResult Sfc(ToolRunResult run, bool repair)
    {
        if (run.Cancelled) return VerifierResult.Cancelled;
        if (run.LaunchFailed || run.ExitCode != 0 ||
            run.Has("could not perform the requested operation") ||
            run.Has("could not start the repair service") || run.Has("access is denied"))
            return VerifierResult.Error;
        if (run.Has("unable to fix")) return repair ? VerifierResult.Not_Repaired : VerifierResult.IssuesFound;
        if (run.Has("after the next reboot")) return VerifierResult.Repair_Scheduled_On_Reboot;
        if (repair && run.Has("successfully repaired"))
            return run.HasErrors ? VerifierResult.Inconclusive : VerifierResult.Repaired;
        if (run.Has("found integrity violations") || run.Has("found corrupt files"))
            return VerifierResult.IssuesFound;
        if (run.Has("Windows Resource Protection did not find any integrity violations"))
            return run.HasErrors ? VerifierResult.Inconclusive : VerifierResult.Scanned;
        return VerifierResult.Inconclusive;
    }

    public static VerifierResult Dism(ToolRunResult run, bool repair)
    {
        if (run.Cancelled) return VerifierResult.Cancelled;
        if (run.LaunchFailed || (run.ExitCode != 0 && run.ExitCode != 3010)) return VerifierResult.Error;
        if (run.Has("component store cannot be repaired")) return VerifierResult.Not_Repaired;
        if (run.Has("component store is repairable")) return VerifierResult.IssuesFound;
        if (run.HasErrors) return VerifierResult.Inconclusive;
        if (repair && run.Has("The restore operation completed successfully"))
            return run.ExitCode == 3010 ? VerifierResult.Repair_Scheduled_On_Reboot : VerifierResult.Repaired;
        if (!repair && run.ExitCode == 0 && run.Has("No component store corruption detected"))
            return VerifierResult.Scanned;
        return VerifierResult.Inconclusive;
    }

    public static VerifierResult Chkdsk(ToolRunResult run, bool repair)
    {
        if (run.Cancelled) return VerifierResult.Cancelled;
        if (run.LaunchFailed) return VerifierResult.Error;
        if (run.Has("CHKDSK is not available")) return VerifierResult.Unsupported;
        if (run.ExitCode < 0 || run.ExitCode > 3) return VerifierResult.Error;
        if (repair && (run.Has("will be checked the next time the system restarts") ||
            run.Has("will be checked on the next restart")))
            return run.HasErrors ? VerifierResult.Inconclusive : VerifierResult.Repair_Scheduled_On_Reboot;
        if (run.Has("Cannot open volume") || run.Has("Cannot lock current drive") ||
            run.Has("Access Denied") || run.Has("access is denied") || run.Has("Unable to determine"))
            return VerifierResult.Error;
        if (run.Has("Errors found") || run.Has("Windows found problems with the file system") ||
            run.Has("corrupt") || run.Has("errors were found"))
        {
            if (repair && run.ExitCode is 0 or 1 && run.Has("Windows has made corrections to the file system"))
                return run.HasErrors ? VerifierResult.Inconclusive : VerifierResult.Repaired;
            return repair ? VerifierResult.Not_Repaired : VerifierResult.IssuesFound;
        }
        if (run.HasErrors) return VerifierResult.Inconclusive;
        if (repair && run.ExitCode is 0 or 1 && run.Has("Windows has made corrections to the file system"))
            return VerifierResult.Repaired;
        if (run.ExitCode == 0 &&
            (run.Has("Windows has scanned the file system and found no problems") ||
             run.Has("Windows has checked the file system and found no problems")))
            return VerifierResult.Scanned;
        return run.ExitCode == 3 ? VerifierResult.Error : VerifierResult.Inconclusive;
    }

    public static VerifierResult Defrag(ToolRunResult run)
    {
        if (run.Cancelled) return VerifierResult.Cancelled;
        if (run.LaunchFailed || run.ExitCode != 0) return VerifierResult.Error;
        return !run.HasErrors && run.Has("The operation completed successfully")
            ? VerifierResult.Scanned : VerifierResult.Inconclusive;
    }

    public static VerifierResult Stability(ToolRunResult run)
    {
        if (run.Cancelled || (!run.LaunchFailed && run.ExitCode == 3)) return VerifierResult.Cancelled;
        if (run.LaunchFailed) return VerifierResult.Error;
        return run.ExitCode switch
        {
            // The worker has a defined exit protocol, but empty/partial output is still insufficient evidence.
            0 when run.Output.LastOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() == "Result: Passed" && !run.HasErrors => VerifierResult.Scanned,
            0 => VerifierResult.Inconclusive,
            1 => VerifierResult.IssuesFound,
            4 => VerifierResult.Inconclusive,
            _ => VerifierResult.Error
        };
    }

    public static (VerifierResult Result, List<DeviceDriverInfo> Drivers) Drivers(ToolRunResult run)
    {
        List<DeviceDriverInfo> unsigned = [];
        if (run.Cancelled) return (VerifierResult.Cancelled, unsigned);
        if (run.LaunchFailed || run.ExitCode != 0) return (VerifierResult.Error, unsigned);
        DeviceDriverInfo current = new();
        HashSet<string> fields = new(StringComparer.OrdinalIgnoreCase);
        int records = 0;
        bool malformed = run.HasErrors;
        void Flush()
        {
            if (fields.Count == 0) return;
            if (fields.Count != 4 || !current.IsValid()) malformed = true;
            else
            {
                records++;
                if (current.IsSigned == false) unsigned.Add(current);
            }
            current = new();
            fields.Clear();
        }
        foreach (string line in run.Output)
        {
            if (string.IsNullOrWhiteSpace(line)) { Flush(); continue; }
            int separator = line.IndexOf(':');
            if (separator < 0) { malformed = true; continue; }
            string key = line[..separator].Replace(" ", "").Trim();
            string value = line[(separator + 1)..].Trim();
            if (key.Equals("DeviceName", StringComparison.OrdinalIgnoreCase) && fields.Contains(key)) Flush();
            if (!fields.Add(key)) malformed = true;
            switch (key.ToUpperInvariant())
            {
                case "DEVICENAME": current.DeviceName = value; break;
                case "INFNAME": current.InfName = value; break;
                case "MANUFACTURER": current.Manufacturer = value; break;
                case "ISSIGNED":
                    if (bool.TryParse(value, out bool signed)) current.IsSigned = signed;
                    else malformed = true;
                    break;
                default: malformed = true; break;
            }
        }
        Flush();
        if (unsigned.Count > 0) return (VerifierResult.IssuesFound, unsigned);
        if (malformed || records == 0) return (VerifierResult.Inconclusive, unsigned);
        return (VerifierResult.Scanned, unsigned);
    }

    public static VerifierResult Aggregate(IEnumerable<VerifierResult> results)
    {
        var items = results.ToList();
        if (items.Count == 0) return VerifierResult.Unsupported;
        foreach (var status in new[] { VerifierResult.Cancelled, VerifierResult.Error,
            VerifierResult.Not_Repaired, VerifierResult.IssuesFound, VerifierResult.Inconclusive })
            if (items.Contains(status)) return status;
        if (items.Contains(VerifierResult.Unsupported))
            return items.All(x => x == VerifierResult.Unsupported) ? VerifierResult.Unsupported : VerifierResult.Inconclusive;
        if (items.Contains(VerifierResult.Repair_Scheduled_On_Reboot)) return VerifierResult.Repair_Scheduled_On_Reboot;
        if (items.Contains(VerifierResult.Repaired)) return VerifierResult.Repaired;
        return VerifierResult.Scanned;
    }
}
