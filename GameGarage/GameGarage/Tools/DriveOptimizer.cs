using System.IO;

namespace GameGarage.Tools;

public sealed class DriveOptimizer : VerifierBase
{
    public DriveOptimizer(AppendToLogAction log, EventHandler<double> progress, CancellationToken cancel,
        IWindowsUserInteraction? userInteraction = null)
        : base("Defrag.exe", true, log, progress, cancel, IsMultiphase: true, userInteraction: userInteraction) { }

    public override VerifierResult RunVerifier()
    {
        if (ToolWrapper.CancelToken.IsCancellationRequested) return VerifierResult.Cancelled;
        ReportProgress(0);
        List<string> drives = [];
        foreach (var drive in DriveInfo.GetDrives())
        {
            ToolWrapper.CancelToken.ThrowIfCancellationRequested();
            if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable &&
                new[] { "FAT", "FAT32", "NTFS", "REFS" }.Contains(drive.DriveFormat, StringComparer.OrdinalIgnoreCase))
                drives.Add(drive.Name.TrimEnd(Path.DirectorySeparatorChar));
        }
        List<VerifierResult> results = [];
        for (int index = 0; index < drives.Count; index++)
        {
            if (ToolWrapper.CancelToken.IsCancellationRequested) return VerifierResult.Cancelled;
            var run = RunCapturedProcess([drives[index], "/O", "/U", "/V"], "", false,
                100.0 * index / drives.Count, 100.0 / drives.Count);
            var result = WindowsOutputParsers.Defrag(run);
            results.Add(result);
            AppendToLog(DiagnosticsText.Format("DriveResult", drives[index], DiagnosticsText.Get("Result_" + result)), true, result != VerifierResult.Scanned, false);
        }
        return Finish(WindowsOutputParsers.Aggregate(results));
    }
}
