namespace GameGarage.Tools;

public sealed class StabilityVerifier : VerifierBase
{
    public StabilityVerifier(AppendToLogAction log, EventHandler<double> progress, CancellationToken cancel,
        IWindowsUserInteraction? userInteraction = null)
        : base("StabilityTest.exe", false, log, progress, cancel, userInteraction: userInteraction) { }

    public override VerifierResult RunVerifier()
    {
        var result = WindowsOutputParsers.Stability(RunCapturedProcess([], "", false));
        if (result == VerifierResult.IssuesFound && !ToolWrapper.CancelToken.IsCancellationRequested &&
            UserInteraction.Confirm(DiagnosticsText.Get("StabilityTitle"), DiagnosticsText.Get("StabilityIssues")))
            UserInteraction.RequestExit();
        return Finish(result);
    }
}
