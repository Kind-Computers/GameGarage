using System.IO;
using GameGarage.Core;
using GameGarage.Utilities;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GameGarage.Tools;

public delegate void AppendToLogAction(string Line, bool IsNewLine, bool LastLineWasNewLine, bool Hightlight, bool IsRepair);
public delegate void ParseOutputAction(string Line, bool IsNewLine);

public abstract class VerifierBase : IDiagnosticCheck
{
    protected readonly Progress<double> TaskProgress = new();
    protected readonly AppendToLogAction AppendToLogCallback;
    protected readonly ProcessWrapper ToolWrapper;
    protected readonly IWindowsUserInteraction UserInteraction;
    protected readonly bool IsMultiphase;
    private bool LastLineWasNewLine = true;
    private bool LastProcessCompletedWithoutCancellation;

    // Deterministic adapter fixtures can supply a transcript without launching system tools.
    internal Func<List<string>, string, ParseOutputAction, ParseOutputAction, int>? ProcessExecutor { get; set; }
    private readonly object LogLock = new();

    protected VerifierBase(string Filename, bool UseSystemDirectory, AppendToLogAction AppendToLogDelegate,
        EventHandler<double> UpdateProgressDelegate, CancellationToken CancelToken,
        Encoding? StandardOutputEncoding = null, bool IsMultiphase = false,
        IWindowsUserInteraction? userInteraction = null)
    {
        AppendToLogCallback = AppendToLogDelegate;
        TaskProgress.ProgressChanged += UpdateProgressDelegate;
        TaskProgress.ProgressChanged += (_, percent) => ProgressChanged?.Invoke(this, new(Id, percent));
        ToolWrapper = new(Filename, UseSystemDirectory, CancelToken, StandardOutputEncoding);
        this.IsMultiphase = IsMultiphase;
        UserInteraction = userInteraction ?? new NonInteractiveUserInteraction();
    }

    public string Id => Path.GetFileNameWithoutExtension(ToolWrapper.Filename).ToLowerInvariant();
    public event EventHandler<DiagnosticProgress>? ProgressChanged;
    public VerifierResult LastVerifierResult { get; private set; } = VerifierResult.Inconclusive;
    public DiagnosticResult LastResult { get; private set; } = new(DiagnosticStatus.Inconclusive);
    protected int? LastExitCode { get; private set; }
    public abstract VerifierResult RunVerifier();

    public DiagnosticResult Run()
    {
        LastExitCode = null;
        LastProcessCompletedWithoutCancellation = false;
        try
        {
            LastVerifierResult = ToolWrapper.CancelToken.IsCancellationRequested ? VerifierResult.Cancelled : RunVerifier();
        }
        catch (OperationCanceledException)
        {
            LastVerifierResult = VerifierResult.Cancelled;
        }
        catch (Exception error)
        {
            AppendToLog(DiagnosticsText.Format("CheckError", error.Message), true, true, false);
            LastVerifierResult = VerifierResult.Error;
        }
        LastVerifierResult = ApplyCancellation(LastVerifierResult);
        LastResult = new(ToDiagnosticStatus(LastVerifierResult), ExitCode: LastExitCode);
        return LastResult;
    }

    public void AddProgressListener(EventHandler<double> listener) => TaskProgress.ProgressChanged += listener;
    protected void ReportProgress(double percentage) => ((IProgress<double>)TaskProgress).Report(Math.Clamp(percentage, 0, 100));

    protected void ReportLineProgress(string line, double current = 0, double portion = 100, bool totalOnly = false)
    {
        var matches = Regex.Matches(line, @"(?:(?<label>\w+):\s*)?(?<percent>\d+(?:\.\d+)?)%");
        foreach (Match match in matches)
        {
            if (totalOnly && !match.Groups["label"].Value.Equals("Total", StringComparison.OrdinalIgnoreCase))
                continue;
            if (double.TryParse(match.Groups["percent"].Value, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out double percent) && percent is >= 0 and <= 100)
                ReportProgress(current + portion * percent / 100);
        }
    }

    private protected ToolRunResult RunCapturedProcess(List<string> args, string input, bool isRepair,
        double current = 0, double portion = 100, bool totalOnly = false)
    {
        LastProcessCompletedWithoutCancellation = false;
        List<string> output = [];
        List<string> errors = [];
        if (ToolWrapper.CancelToken.IsCancellationRequested)
            return new(-1, output, errors, Cancelled: true);
        AppendToLog("", true, false, isRepair);
        AppendToLog(DiagnosticsText.Format("Running", ToolWrapper.Filename, string.Join(" ", args)), true, true, isRepair);
        var timer = Stopwatch.StartNew();
        int exitCode;
        bool launchFailed = false;
        bool processCompleted = false;
        try
        {
            if (!IsMultiphase) ReportProgress(0);
            ParseOutputAction onOutput = (line, newline) =>
            {
                output.Add(line);
                ReportLineProgress(line, current, portion, totalOnly);
                AppendToLog(line, newline, false, isRepair);
            };
            ParseOutputAction onError = (line, newline) =>
            {
                errors.Add(line);
                AppendToLog(DiagnosticsText.Format("StandardError", line), newline, true, isRepair);
            };
            exitCode = ProcessExecutor is { } execute
                ? execute(args, input, onOutput, onError)
                : ToolWrapper.RunProcess(args, input, onOutput, onError);
            processCompleted = true;
        }
        catch (OperationCanceledException)
        {
            exitCode = -1;
        }
        catch (Exception error)
        {
            exitCode = -1;
            launchFailed = true;
            AppendToLog(DiagnosticsText.Format("ProcessError", ToolWrapper.Filename, error.Message), true, true, isRepair);
        }
        LastExitCode = exitCode;
        timer.Stop();
        bool cancelled = ToolWrapper.CancelToken.IsCancellationRequested;
        LastProcessCompletedWithoutCancellation = processCompleted && !cancelled;
        AppendToLog(DiagnosticsText.Format(cancelled ? "ProcessCancelled" : "ProcessExit", ToolWrapper.Filename, exitCode), true, false, isRepair);
        AppendToLog(DiagnosticsText.Format("ProcessTime", ToolWrapper.Filename, timer.Elapsed), true, false, isRepair);
        return new(exitCode, output, errors, cancelled, launchFailed);
    }

    protected void AppendToLog(string Line, bool IsNewLine, bool Highlight, bool IsRepair)
    {
        lock (LogLock)
        {
            AppendToLogCallback(Line, IsNewLine, LastLineWasNewLine, Highlight, IsRepair);
            LastLineWasNewLine = IsNewLine;
        }
        Debug.WriteLine($"{ToolWrapper.Filename}: {Line}");
    }

    protected VerifierResult Finish(VerifierResult result)
    {
        result = ApplyCancellation(result);
        if (result is VerifierResult.Scanned or VerifierResult.Repaired or VerifierResult.Repair_Scheduled_On_Reboot)
            ReportProgress(100);
        return result;
    }

    private VerifierResult ApplyCancellation(VerifierResult result)
    {
        // Closing a post-scan findings dialog cancels the remaining session, but must not
        // erase evidence from the child that already completed. Interrupted children do
        // not qualify, even if they emitted a finding before cancellation.
        bool completedFinding = LastProcessCompletedWithoutCancellation &&
            result is VerifierResult.IssuesFound or VerifierResult.Not_Repaired;
        return ToolWrapper.CancelToken.IsCancellationRequested && !completedFinding
            ? VerifierResult.Cancelled : result;
    }

    public static DiagnosticStatus ToDiagnosticStatus(VerifierResult result) => result switch
    {
        VerifierResult.Scanned => DiagnosticStatus.Passed,
        VerifierResult.Cancelled => DiagnosticStatus.Cancelled,
        VerifierResult.Repaired => DiagnosticStatus.Repaired,
        VerifierResult.Repair_Scheduled_On_Reboot => DiagnosticStatus.RepairScheduled,
        VerifierResult.Not_Repaired or VerifierResult.IssuesFound => DiagnosticStatus.IssuesFound,
        VerifierResult.Error => DiagnosticStatus.Error,
        VerifierResult.Unsupported => DiagnosticStatus.Unsupported,
        _ => DiagnosticStatus.Inconclusive
    };

    // Preserve existing numeric values for the WPF adapter.
    public enum VerifierResult
    {
        Scanned, Cancelled, Repaired, Repair_Scheduled_On_Reboot, Not_Repaired,
        Error, Inconclusive, Unsupported, IssuesFound
    }
}
