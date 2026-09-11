using System.IO;

namespace GameGarage.Tools;

public sealed class DriveVerifier : VerifierBase
{
    public DriveVerifier(AppendToLogAction log, EventHandler<double> progress, CancellationToken cancel,
        IWindowsUserInteraction? userInteraction = null)
        : base("ChkDsk.exe", true, log, progress, cancel, IsMultiphase: true, userInteraction: userInteraction) { }

    public override VerifierResult RunVerifier()
    {
        if (ToolWrapper.CancelToken.IsCancellationRequested) return VerifierResult.Cancelled;
        ReportProgress(0);
        string systemDrive = (Path.GetPathRoot(Environment.SystemDirectory) ?? "").TrimEnd(Path.DirectorySeparatorChar);
        List<string> drives = [];
        if (systemDrive.Length > 0) drives.Add(systemDrive);
        foreach (var drive in DriveInfo.GetDrives())
        {
            ToolWrapper.CancelToken.ThrowIfCancellationRequested();
            if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable &&
                new[] { "NTFS", "FAT", "FAT32", "exFAT" }.Contains(drive.DriveFormat, StringComparer.OrdinalIgnoreCase))
            {
                string name = drive.Name.TrimEnd(Path.DirectorySeparatorChar);
                if (!drives.Contains(name, StringComparer.OrdinalIgnoreCase)) drives.Add(name);
            }
        }
        List<VerifierResult> results = [];
        for (int index = 0; index < drives.Count; index++)
        {
            if (ToolWrapper.CancelToken.IsCancellationRequested) return VerifierResult.Cancelled;
            results.Add(VerifyDrive(drives[index], 100.0 * index / drives.Count, 100.0 / drives.Count));
        }
        return Finish(WindowsOutputParsers.Aggregate(results));
    }

    public VerifierResult VerifyDrive(string DriveName, double CurrentPercent, double PortionPercent)
    {
        var result = WindowsOutputParsers.Chkdsk(
            RunCapturedProcess([DriveName], "N", false, CurrentPercent, PortionPercent, true), false);
        if (result == VerifierResult.IssuesFound && !ToolWrapper.CancelToken.IsCancellationRequested &&
            UserInteraction.Confirm(DiagnosticsText.Format("DriveTitle", DriveName), DiagnosticsText.Format("RepairDrive", DriveName)))
        {
            result = WindowsOutputParsers.Chkdsk(
                RunCapturedProcess([DriveName, "/F"], "Y", true, CurrentPercent, PortionPercent, true), true);
            UserInteraction.Notify(DiagnosticsText.Format("DriveTitle", DriveName), DiagnosticsText.Get("Result_" + result), ToDiagnosticStatus(result));
        }
        AppendToLog(DiagnosticsText.Format("DriveResult", DriveName, DiagnosticsText.Get("Result_" + result)), true, result != VerifierResult.Scanned, false);
        return ToolWrapper.CancelToken.IsCancellationRequested ? VerifierResult.Cancelled : result;
    }
}
