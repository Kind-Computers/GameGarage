namespace GameGarage.Tools;

public sealed class WindowsImageVerifier : VerifierBase
{
    public WindowsImageVerifier(AppendToLogAction log, EventHandler<double> progress, CancellationToken cancel,
        IWindowsUserInteraction? userInteraction = null)
        : base("DISM.exe", true, log, progress, cancel, userInteraction: userInteraction) { }

    public override VerifierResult RunVerifier()
    {
        ToolRunResult run = RunDism(false, null);
        var result = WindowsOutputParsers.Dism(run, false);
        if (result != VerifierResult.IssuesFound || ToolWrapper.CancelToken.IsCancellationRequested ||
            !UserInteraction.Confirm(DiagnosticsText.Get("WindowsImageTitle"), DiagnosticsText.Get("RepairWindowsImage")))
            return Finish(result);

        run = RunDism(true, null);
        while (!ToolWrapper.CancelToken.IsCancellationRequested && run.ExitCode == unchecked((int)0x800F081F))
        {
            if (!UserInteraction.Confirm(DiagnosticsText.Get("WindowsImageTitle"), DiagnosticsText.Get("SelectImageSource")))
                break;
            var source = UserInteraction.SelectWindowsImage();
            if (source is null || ToolWrapper.CancelToken.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(source.Value.Filename) || source.Value.Index < 1) break;
            run = RunDism(true, source);
        }
        result = WindowsOutputParsers.Dism(run, true);
        UserInteraction.Notify(DiagnosticsText.Get("WindowsImageTitle"), DiagnosticsText.Get("Result_" + result), ToDiagnosticStatus(result));
        return Finish(result);
    }

    private ToolRunResult RunDism(bool repair, (string Filename, int Index)? source)
    {
        List<string> args = ["/English", "/Online", "/Cleanup-Image", repair ? "/RestoreHealth" : "/ScanHealth"];
        if (source is not null) args.Add($"/Source:WIM:{source.Value.Filename}:{source.Value.Index}");
        return RunCapturedProcess(args, "", repair);
    }
}
