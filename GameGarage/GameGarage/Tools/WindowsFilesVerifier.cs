using GameGarage.Utilities;
using System.Text;

namespace GameGarage.Tools;

public sealed class WindowsFilesVerifier : VerifierBase
{
    public WindowsFilesVerifier(AppendToLogAction log, EventHandler<double> progress, CancellationToken cancel,
        IWindowsUserInteraction? userInteraction = null)
        : base("SFC.exe", true, log, progress, cancel,
            SystemUtilities.WindowsVersion >= 11 ? Encoding.Unicode : null, userInteraction: userInteraction) { }

    public override VerifierResult RunVerifier()
    {
        var result = WindowsOutputParsers.Sfc(RunCapturedProcess(["/VERIFYONLY"], "", false), false);
        if (result == VerifierResult.IssuesFound && !ToolWrapper.CancelToken.IsCancellationRequested &&
            UserInteraction.Confirm(DiagnosticsText.Get("SystemFilesTitle"), DiagnosticsText.Get("RepairSystemFiles")))
        {
            if (ToolWrapper.CancelToken.IsCancellationRequested) return Finish(VerifierResult.Cancelled);
            result = WindowsOutputParsers.Sfc(RunCapturedProcess(["/SCANNOW"], "", true), true);
            UserInteraction.Notify(DiagnosticsText.Get("SystemFilesTitle"), DiagnosticsText.Get("Result_" + result), ToDiagnosticStatus(result));
        }
        return Finish(result);
    }
}
